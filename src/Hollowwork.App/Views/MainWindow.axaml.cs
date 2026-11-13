using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Hollowwork.App.ViewModels;

namespace Hollowwork.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? initialPath)
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            Opened += async (_, _) => await _viewModel.OpenAsync(initialPath);
        }
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

    private async void OnPlanClicked(object? sender, RoutedEventArgs e) => await _viewModel.PlanAsync();

    private async void OnApplyClicked(object? sender, RoutedEventArgs e) => await _viewModel.ApplyAsync();
}
