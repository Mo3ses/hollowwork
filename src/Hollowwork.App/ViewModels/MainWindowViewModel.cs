using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Hollowwork.App;
using Hollowwork.App.Settings;
using Hollowwork.Core.Archives;
using Hollowwork.Core.Backup;
using Hollowwork.Core.Oodle;
using Hollowwork.Core.Patches;
using Hollowwork.Core.Text;
using Hollowwork.Core.Textures;
using Hollowwork.Patches;

namespace Hollowwork.App.ViewModels;

public sealed record ArchiveFileItem(string Path, string Name);

public enum WorkspaceMode
{
    Archive,
    Patches,
    Settings,
}

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const int PreviewLimit = 200_000;

    private readonly AppSettings _settings;
    private IArchiveSession? _session;
    private WriteableBitmap? _previewImage;
    private byte[]? _selectedBytes;
    private string? _selectedPath;
    private PatchPlan? _plan;
    private string? _currentDirectory;
    private bool _applyArmed;
    private string _archiveLabel = "NO ARCHIVE OPEN · OPEN CONTENT.GGPK OR _.INDEX.BIN TO BEGIN";
    private string _kindLabel = "ARCHIVE —";
    private string _countLabel = "0 FILES";
    private string _statusLabel = "READY";
    private WorkspaceMode _mode = WorkspaceMode.Archive;
    private PatchItemViewModel? _selectedPatch;
    private string _previewHeader = "NO FILE SELECTED";
    private string _previewText = string.Empty;
    private string _planSummary = "0 FILES · 0 CHANGES";
    private string _backupNotice = "BACKUP BEFORE WRITE";
    private string _applyLabel = "APPLY";
    private bool _canApply;
    private bool _isBusy;
    private string _selectedGame = "poe1";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        _settings = SettingsStore.Load();
        _selectedGame = _settings.Game is "poe2" ? "poe2" : "poe1";
        LoadPatches();
    }

    public ObservableCollection<string> Directories { get; } = [];

    public ObservableCollection<ArchiveFileItem> Files { get; } = [];

    public ObservableCollection<PatchItemViewModel> Patches { get; } = [];

    public IReadOnlyList<string> Games { get; } = ["poe1", "poe2"];

    public string SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _selectedGame == value)
            {
                return;
            }

            _selectedGame = value;
            _settings.Game = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            LoadPatches();
        }
    }

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

    public string PlanSummary
    {
        get => _planSummary;
        private set => Set(ref _planSummary, value);
    }

    public string BackupNotice
    {
        get => _backupNotice;
        private set => Set(ref _backupNotice, value);
    }

    public string ApplyLabel
    {
        get => _applyLabel;
        private set => Set(ref _applyLabel, value);
    }

    public bool CanApply
    {
        get => _canApply;
        private set => Set(ref _canApply, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public WriteableBitmap? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (ReferenceEquals(_previewImage, value))
            {
                return;
            }

            _previewImage?.Dispose();
            _previewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => _previewImage is not null;

    public string? SelectedPath => _selectedPath;

    public bool CanExport => _selectedBytes is not null;

    public string SelectionLabel => $"{Patches.Count(patch => patch.IsSelected)} SELECTED";

    public WorkspaceMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsArchiveMode));
            OnPropertyChanged(nameof(IsPatchesMode));
            OnPropertyChanged(nameof(IsSettingsMode));
            OnPropertyChanged(nameof(ShowExplorer));
            OnPropertyChanged(nameof(ShowPreview));
            OnPropertyChanged(nameof(ShowPatches));
            OnPropertyChanged(nameof(ShowChronicle));
            OnPropertyChanged(nameof(ShowSettings));
            OnPropertyChanged(nameof(PatchColumn));
            OnPropertyChanged(nameof(PatchColumnSpan));
        }
    }

    public bool IsArchiveMode => _mode == WorkspaceMode.Archive;

    public bool IsPatchesMode => _mode == WorkspaceMode.Patches;

    public bool IsSettingsMode => _mode == WorkspaceMode.Settings;

    public bool ShowExplorer => _mode == WorkspaceMode.Archive;

    public bool ShowPreview => _mode == WorkspaceMode.Archive;

    public bool ShowPatches => _mode == WorkspaceMode.Archive;

    public bool ShowChronicle => _mode == WorkspaceMode.Patches;

    public PatchItemViewModel? SelectedPatch
    {
        get => _selectedPatch;
        set
        {
            if (ReferenceEquals(_selectedPatch, value))
            {
                return;
            }

            _selectedPatch = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPatch));
        }
    }

    public bool HasSelectedPatch => _selectedPatch is not null;

    public bool ShowSettings => _mode == WorkspaceMode.Settings;

    public int PatchColumn => _mode == WorkspaceMode.Patches ? 1 : 3;

    public int PatchColumnSpan => _mode == WorkspaceMode.Patches ? 3 : 1;

    public string OodlePathText => OodleLibrary.ResolvedPath ?? "not found — place liboo2corelinux64.so.9 in ~/.local/share/Hollowwork/oo2core";

    public string BackupRootText => AppPaths.BackupRoot;

    public string SettingsPathText => AppPaths.SettingsFile;

    public string ArchivePathText => CurrentInputPath ?? "not open";

    public void SetMode(WorkspaceMode mode) => Mode = mode;

    public string? CurrentInputPath { get; private set; }

    public ArchiveKind? TargetKind => _session?.Kind;

    public string? RestoreTarget => _session is null
        ? null
        : _session.Kind == ArchiveKind.Ggpk
            ? _session.FilePath
            : Path.GetDirectoryName(_session.FilePath);

    public void CloseArchive()
    {
        _session?.Dispose();
        _session = null;
    }

    public async Task OpenAsync(string path)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusLabel = "OPENING…";
        ResetPlan();
        PreviewHeader = "NO FILE SELECTED";
        PreviewText = string.Empty;
        PreviewImage = null;
        _selectedBytes = null;
        _selectedPath = null;
        OnPropertyChanged(nameof(CanExport));

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
            CurrentInputPath = path;
            _settings.LastArchive = path;
            SettingsStore.Save(_settings);
            OnPropertyChanged(nameof(CurrentInputPath));
            OnPropertyChanged(nameof(ArchivePathText));
            OnPropertyChanged(nameof(OodlePathText));

            ArchiveLabel = path;
            KindLabel = _session.Kind == ArchiveKind.Ggpk ? "GGPK" : "BUNDLES2";
            CountLabel = $"{paths.Count:N0} FILES";
            BackupNotice = _session.Kind == ArchiveKind.Ggpk
                ? "BACKUP · FULL ARCHIVE"
                : "BACKUP · INDEX + LIBGGPK3";
            StatusLabel = "READY";

            Directories.Clear();
            Files.Clear();
            _currentDirectory = null;

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

        _currentDirectory = directory;
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

            _selectedBytes = bytes;
            _selectedPath = path;
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(SelectedPath));
            PreviewImage = null;
            PreviewText = string.Empty;

            if (path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
                && TextureDecoder.TryDecodeDds(bytes, out var texture))
            {
                PreviewImage = ToBitmap(texture);
                PreviewHeader = $"{path} · {texture.Width}×{texture.Height} · {bytes.Length:N0} BYTES";
                return;
            }

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

    public async Task PlanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (_session is null)
        {
            StatusLabel = "OPEN AN ARCHIVE FIRST";
            return;
        }

        var selected = Patches
            .Where(patch => patch.IsSelected)
            .Select(patch => new PatchSelection(patch.Definition, patch.Values()))
            .ToList();
        if (selected.Count == 0)
        {
            StatusLabel = "SELECT AT LEAST ONE PATCH";
            return;
        }

        IsBusy = true;
        StatusLabel = "PLANNING…";
        ResetPlan();

        try
        {
            var plan = await Task.Run(() => new PatchPlanner().Plan(_session, selected));
            _plan = plan;

            PlanSummary = $"{plan.Files.Count:N0} FILES · {plan.TotalChanges:N0} CHANGES";
            PreviewHeader = $"PLAN · {plan.Files.Count:N0} FILES · {plan.TotalChanges:N0} CHANGES";
            PreviewText = BuildPlanText(plan);
            CanApply = !plan.IsEmpty;
            StatusLabel = plan.IsEmpty ? "NO CHANGES" : "PLAN READY · DRY RUN";
        }
        catch (Exception exception)
        {
            StatusLabel = $"PLAN FAILED · {exception.Message}";
            CanApply = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyAsync()
    {
        if (IsBusy || _session is null || _plan is null || _plan.IsEmpty)
        {
            return;
        }

        if (!_applyArmed)
        {
            _applyArmed = true;
            ApplyLabel = "CONFIRM APPLY";
            StatusLabel = "CLICK AGAIN TO WRITE";
            return;
        }

        IsBusy = true;
        StatusLabel = "BACKING UP…";

        try
        {
            string backupPath;
            if (_session.Kind == ArchiveKind.Ggpk)
            {
                backupPath = await Task.Run(() => ArchiveBackup.Create(_session.FilePath, AppPaths.BackupRoot).BackupPath);
            }
            else
            {
                var bundlesDirectory = Path.GetDirectoryName(_session.FilePath)
                    ?? throw new InvalidOperationException("Unable to resolve the Bundles2 directory.");
                backupPath = await Task.Run(() => BundleArchiveBackup.Create(bundlesDirectory, AppPaths.BackupRoot));
            }

            StatusLabel = "APPLYING…";
            var report = await Task.Run(() => new PatchApplier().Apply(_session, _plan));

            StatusLabel = $"APPLIED · {report.FilesWritten:N0} FILES · {report.TotalChanges:N0} CHANGES";
            PlanSummary = $"APPLIED · {report.FilesWritten:N0} FILES";
            PreviewHeader = "APPLIED";
            PreviewText = $"Backup: {backupPath}\n\n" +
                          string.Join('\n', report.Paths.Take(500));

            _plan = null;
            CanApply = false;
            _applyArmed = false;
            ApplyLabel = "APPLY";

            if (_currentDirectory is not null)
            {
                await SelectDirectoryAsync(_currentDirectory);
            }
        }
        catch (Exception exception)
        {
            StatusLabel = $"APPLY FAILED · {exception.Message}";
            _applyArmed = false;
            ApplyLabel = "APPLY";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportAsync(string destination)
    {
        if (_selectedBytes is null)
        {
            return;
        }

        await File.WriteAllBytesAsync(destination, _selectedBytes);
        StatusLabel = $"EXPORTED · {destination}";
    }

    public void Dispose()
    {
        PersistParameters();
        _session?.Dispose();
        _session = null;
    }

    private static WriteableBitmap ToBitmap(DecodedTexture texture)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(texture.Width, texture.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var buffer = bitmap.Lock();
        var rowBytes = texture.Width * 4;
        for (var y = 0; y < texture.Height; y++)
        {
            Marshal.Copy(texture.Bgra, y * rowBytes, IntPtr.Add(buffer.Address, y * buffer.RowBytes), rowBytes);
        }

        return bitmap;
    }

    private void LoadPatches()
    {
        Patches.Clear();
        _plan = null;
        CanApply = false;
        ApplyLabel = "APPLY";

        var directory = Path.Combine(AppContext.BaseDirectory, "patches", SelectedGame);
        if (!Directory.Exists(directory))
        {
            StatusLabel = $"PATCHES NOT FOUND · {directory}";
            return;
        }

        try
        {
            foreach (var definition in PatchLoader.LoadDirectory(directory)
                         .OrderByDescending(item => item.Parameters is { Count: > 0 })
                         .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var item = new PatchItemViewModel(definition);
                item.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(PatchItemViewModel.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectionLabel));
                        ResetPlan();
                    }
                };

                if (_settings.PatchParameters.TryGetValue(definition.Id, out var stored))
                {
                    foreach (var parameter in item.Parameters)
                    {
                        if (stored.TryGetValue(parameter.Key, out var value))
                        {
                            parameter.ApplyStored(value);
                        }
                    }
                }

                item.ParameterChanged += PersistParameters;
                Patches.Add(item);
            }

            OnPropertyChanged(nameof(SelectionLabel));
            SelectedPatch ??= Patches.FirstOrDefault();
        }
        catch (Exception exception)
        {
            StatusLabel = $"PATCH LOAD FAILED · {exception.Message}";
        }
    }

    private void PersistParameters()
    {
        _settings.PatchParameters.Clear();
        foreach (var patch in Patches.Where(patch => patch.Parameters.Count > 0))
        {
            _settings.PatchParameters[patch.Definition.Id] =
                patch.Parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Persisted);
        }

        SettingsStore.Save(_settings);
    }

    private void ResetPlan()
    {
        _plan = null;
        CanApply = false;
        _applyArmed = false;
        ApplyLabel = "APPLY";
        PlanSummary = "0 FILES · 0 CHANGES";
    }

    private static string BuildPlanText(PatchPlan plan)
    {
        var lines = new List<string>();
        foreach (var file in plan.Files)
        {
            lines.Add(file.Path);
            lines.Add("  " + string.Join(" · ", file.Operations
                .Where(operation => operation.Changes > 0)
                .Select(operation => $"{operation.Type}:{operation.Changes}")));
        }

        if (plan.Skipped.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"SKIPPED ({plan.Skipped.Count:N0})");
            lines.AddRange(plan.Skipped.Take(200).Select(skip => $"  {skip.Path} · {skip.Reason}"));
        }

        return string.Join('\n', lines);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
