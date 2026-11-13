using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hollowwork.Core.Backup;

namespace Hollowwork.App.ViewModels;

public sealed class BackupWindowViewModel : INotifyPropertyChanged
{
    private readonly string? _target;
    private readonly string _backupRoot;
    private BackupEntry? _selected;
    private string _status = "SELECT A BACKUP";
    private string _restoreLabel = "RESTORE";
    private bool _armed;

    public BackupWindowViewModel(string? target, string? targetKind, string backupRoot)
    {
        _target = target;
        _backupRoot = backupRoot;
        TargetLabel = target is null
            ? "NO ARCHIVE OPEN · RESTORE DISABLED"
            : $"{targetKind?.ToString().ToUpperInvariant()} · {target}";
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BackupEntry> Entries { get; } = [];

    public string TargetLabel { get; }

    public BackupEntry? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            _selected = value;
            _armed = false;
            RestoreLabel = "RESTORE";
            OnPropertyChanged();
            Status = value is null ? "SELECT A BACKUP" : value.Display;
            OnPropertyChanged(nameof(CanRestore));
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public string RestoreLabel
    {
        get => _restoreLabel;
        private set
        {
            _restoreLabel = value;
            OnPropertyChanged();
        }
    }

    public bool CanRestore => _selected is not null
        && _target is not null
        && _selected.Kind != "index-only";

    public void Refresh()
    {
        Entries.Clear();
        foreach (var entry in BackupCatalog.List(_backupRoot))
        {
            Entries.Add(entry);
        }

        if (Entries.Count == 0)
        {
            Status = "NO BACKUPS FOUND";
        }
    }

    public async Task<bool> RestoreAsync()
    {
        if (_selected is null || _target is null || _selected.Kind == "index-only")
        {
            return false;
        }

        if (!_armed)
        {
            _armed = true;
            RestoreLabel = "CONFIRM RESTORE";
            Status = "CLICK AGAIN TO OVERWRITE THE ARCHIVE";
            return false;
        }

        try
        {
            Status = "RESTORING…";
            var snapshot = await Task.Run(() => BackupCatalog.Restore(_selected, _target));
            Status = string.IsNullOrEmpty(snapshot)
                ? "RESTORED"
                : $"RESTORED · PREVIOUS STATE IN {snapshot}";
            _armed = false;
            RestoreLabel = "RESTORE";
            return true;
        }
        catch (Exception exception)
        {
            Status = $"RESTORE FAILED · {exception.Message}";
            _armed = false;
            RestoreLabel = "RESTORE";
            return false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
