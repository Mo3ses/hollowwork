using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Hollowwork.App.ViewModels;

namespace Hollowwork.App.Views;

public partial class CompactWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public CompactWindow() : this(null)
    {
    }

    public CompactWindow(string? initialPath)
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            Opened += async (_, _) => await _viewModel.OpenAsync(initialPath);
        }
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
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
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Bundles2 folder",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder)
        {
            await _viewModel.OpenAsync(folder);
        }
    }

    private async void OnBackupClicked(object? sender, RoutedEventArgs e)
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

    private void OnPatchesTabClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.SelectPatchTab();

    private async void OnFlipTabClicked(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectFlipTab();
        await _viewModel.Flip.ActivateAsync();
    }

    private async void OnFlipRefreshClicked(object? sender, RoutedEventArgs e) =>
        await _viewModel.Flip.RefreshAsync();

    private async void OnPlanClicked(object? sender, RoutedEventArgs e) => await _viewModel.PlanAsync();

    private async void OnApplyClicked(object? sender, RoutedEventArgs e) => await _viewModel.ApplyAsync();
}
