using Avalonia.Controls;
using Avalonia.Interactivity;
using Hollowwork.App.ViewModels;

namespace Hollowwork.App.Views;

public partial class AutomationView : UserControl
{
    public AutomationView()
    {
        InitializeComponent();
    }

    private void OnStartStopClicked(object? sender, RoutedEventArgs e) =>
        (DataContext as LiveViewModel)?.Toggle();
}
