using Avalonia.Controls;
using Avalonia.Interactivity;
using Hollowwork.App.ViewModels;
using Hollowwork.Core.Archives;

namespace Hollowwork.App.Views;

public partial class BackupWindow : Window
{
    private readonly BackupWindowViewModel _viewModel;

    public BackupWindow() : this(null, null)
    {
    }

    public BackupWindow(string? target, ArchiveKind? kind)
    {
        InitializeComponent();
        _viewModel = new BackupWindowViewModel(target, kind?.ToString(), AppPaths.BackupRoot);
        DataContext = _viewModel;
    }

    private async void OnRestoreClicked(object? sender, RoutedEventArgs e)
    {
        if (await _viewModel.RestoreAsync())
        {
            Close(true);
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close(false);
}
