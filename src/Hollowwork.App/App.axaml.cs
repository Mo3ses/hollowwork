using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hollowwork.App.Views;

namespace Hollowwork.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            var initialPath = args.FirstOrDefault(argument => !argument.StartsWith('-'));
            desktop.MainWindow = new MainWindow(initialPath, args.Contains("--backups"));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
