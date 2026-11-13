using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hollowwork.Core.Archives;
using Hollowwork.Core.Oodle;
using Hollowwork.Core.Text;

namespace Hollowwork.App.ViewModels;

public sealed record ArchiveFileItem(string Path, string Name);

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const int PreviewLimit = 200_000;

    private IArchiveSession? _session;
    private string _archiveLabel = "NO ARCHIVE OPEN · OPEN CONTENT.GGPK OR _.INDEX.BIN TO BEGIN";
    private string _kindLabel = "ARCHIVE —";
    private string _countLabel = "0 FILES";
    private string _statusLabel = "OFFLINE · NO NETWORK ACCESS";
    private string _previewHeader = "NO FILE SELECTED";
    private string _previewText = string.Empty;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Directories { get; } = [];

    public ObservableCollection<ArchiveFileItem> Files { get; } = [];

    public string ArchiveLabel
    {
        get => _archiveLabel;
        private set => Set(ref _archiveLabel, value);
    }

    public string KindLabel
    {
        get => _kindLabel;
        private set => Set(ref _kindLabel, value);
    }

    public string CountLabel
    {
        get => _countLabel;
        private set => Set(ref _countLabel, value);
    }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => Set(ref _statusLabel, value);
    }

    public string PreviewHeader
    {
        get => _previewHeader;
        private set => Set(ref _previewHeader, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => Set(ref _previewText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public async Task OpenAsync(string path)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusLabel = "OPENING…";
        PreviewHeader = "NO FILE SELECTED";
        PreviewText = string.Empty;

        IArchiveSession? session = null;
        try
        {
            var oodlePath = Environment.GetEnvironmentVariable(OodleLibrary.LinuxEnvironmentVariable);
            var options = new ArchiveSessionOptions(oodlePath);

            session = await Task.Run(() => ArchiveSessionFactory.Open(path, options));
            var paths = await Task.Run(() => session.ListFiles(string.Empty, []));

            _session?.Dispose();
            _session = session;
            session = null;

            ArchiveLabel = path;
            KindLabel = _session.Kind == ArchiveKind.Ggpk ? "GGPK" : "BUNDLES2";
            CountLabel = $"{paths.Count:N0} FILES";
            StatusLabel = "READY · OFFLINE";

            Directories.Clear();
            Files.Clear();
            foreach (var directory in paths
                         .Select(item => item.Split('/')[0])
                         .Where(item => item.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                Directories.Add(directory);
            }
        }
        catch (OodleLibraryNotFoundException)
        {
            StatusLabel = "OODLE NOT FOUND";
            ArchiveLabel = "Set HOLLOWWORK_OODLE to liboo2corelinux64.so.9 and try again.";
        }
        catch (Exception exception)
        {
            StatusLabel = "OPEN FAILED";
            ArchiveLabel = exception.Message;
        }
        finally
        {
            session?.Dispose();
            IsBusy = false;
        }
    }

    public async Task SelectDirectoryAsync(string directory)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            var paths = await Task.Run(() => _session.ListFiles(directory, []));
            Files.Clear();
            foreach (var path in paths)
            {
                var name = path[(path.LastIndexOf('/') + 1)..];
                Files.Add(new ArchiveFileItem(path, name));
            }

            StatusLabel = $"{directory} · {paths.Count:N0} FILES";
        }
        catch (Exception exception)
        {
            StatusLabel = $"LIST FAILED · {exception.Message}";
        }
    }

    public async Task SelectFileAsync(string path)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            var bytes = await Task.Run(() =>
            {
                _session.TryRead(path, out var content);
                return content;
            });

            PreviewHeader = $"{path} · {bytes.Length:N0} BYTES";

            if (TextContent.TryDecode(bytes, out var text, out _))
            {
                PreviewText = text.Length > PreviewLimit
                    ? text[..PreviewLimit] + $"\n\n… truncated ({text.Length:N0} chars)"
                    : text;
            }
            else
            {
                var hex = Convert.ToHexStringLower(bytes.AsSpan(0, Math.Min(bytes.Length, 256)));
                PreviewText = $"[binary · {bytes.Length:N0} bytes]\n\n{hex}";
            }
        }
        catch (Exception exception)
        {
            PreviewHeader = path;
            PreviewText = $"Preview failed: {exception.Message}";
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
