using Avalonia.Controls;
using Avalonia.Interactivity;
using Hollowwork.App.ViewModels;

namespace Hollowwork.App.Views;

public partial class LiveWindow : Window
{
    private readonly LiveViewModel _viewModel = new();

    public LiveWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnStartStopClicked(object? sender, RoutedEventArgs e) => _viewModel.Toggle();

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
