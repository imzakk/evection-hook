using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Evection.GUI.Services;
using Evection.GUI.ViewModels;
using Evection.GUI.Views;

namespace Evection.GUI;

public sealed class App : Application
{
    public AppServices Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = new AppServices();
        ApplyTheme(Services.Settings.Current.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(Services),
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(AppTheme theme)
    {
        if (Current == null)
            return;
        Current.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark,
        };
    }
}
