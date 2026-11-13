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
            string? initialPath = null;
            string? initialFile = null;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--file" && i + 1 < args.Length)
                {
                    initialFile = args[++i];
                }
                else if (!args[i].StartsWith('-') && initialPath is null)
                {
                    initialPath = args[i];
                }
            }

            desktop.MainWindow = new CompactWindow(initialPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
