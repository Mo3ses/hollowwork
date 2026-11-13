using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Hollowwork.App.ViewModels;

namespace Hollowwork.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow() : this(null, false, null)
    {
    }

    public MainWindow(string? initialPath, bool openBackups = false, string? initialFile = null)
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();

        Opened += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                await _viewModel.OpenAsync(initialPath);
            }

            if (!string.IsNullOrWhiteSpace(initialFile))
            {
                await _viewModel.SelectFileAsync(initialFile);
            }

            if (openBackups)
            {
                await ShowBackupsAsync();
            }
        };
    }

    private async void OnOpenGgpkClicked(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Content.ggpk",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GGPK archive") { Patterns = ["*.ggpk"] }],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await _viewModel.OpenAsync(path);
        }
    }

    private async void OnOpenBundlesClicked(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Bundles2 folder",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await _viewModel.OpenAsync(path);
        }
    }

    private async void OnDirectorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DirectoryList.SelectedItem is string directory)
        {
            FileList.SelectedItem = null;
            await _viewModel.SelectDirectoryAsync(directory);
        }
    }

    private async void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is ArchiveFileItem item)
        {
            await _viewModel.SelectFileAsync(item.Path);
        }
    }

    private async void OnBackupClicked(object? sender, RoutedEventArgs e) => await ShowBackupsAsync();

    private void OnArchiveModeClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.SetMode(WorkspaceMode.Archive);

    private void OnPatchesModeClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.SetMode(WorkspaceMode.Patches);

    private void OnSettingsModeClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.SetMode(WorkspaceMode.Settings);

    private void OnOpenDataFolderClicked(object? sender, RoutedEventArgs e) => OpenPath(AppPaths.DataRoot);

    private static void OpenPath(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "explorer.exe" : "xdg-open",
            Arguments = path,
            UseShellExecute = true,
        });
    }

    private async Task ShowBackupsAsync()
    {
        var inputPath = _viewModel.CurrentInputPath;
        var window = new BackupWindow(_viewModel.RestoreTarget, _viewModel.TargetKind);
        var restored = await window.ShowDialog<bool>(this);

        if (restored && inputPath is not null)
        {
            _viewModel.CloseArchive();
            await _viewModel.OpenAsync(inputPath);
        }
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedPath;
        var top = TopLevel.GetTopLevel(this);
        if (selected is null || top is null)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export file",
            SuggestedFileName = Path.GetFileName(selected),
            DefaultExtension = Path.GetExtension(selected).TrimStart('.'),
        });

        if (file?.TryGetLocalPath() is { } destination)
        {
            await _viewModel.ExportAsync(destination);
        }
    }

    private async void OnPlanClicked(object? sender, RoutedEventArgs e) => await _viewModel.PlanAsync();

    private async void OnApplyClicked(object? sender, RoutedEventArgs e) => await _viewModel.ApplyAsync();
}
