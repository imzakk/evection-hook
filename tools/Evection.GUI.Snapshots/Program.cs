// Usage: dotnet run --project tools/Evection.GUI.Snapshots -- <output-dir> [game-name-for-detail-pages]
// Uses a throwaway settings folder, so your real Evection Hook settings are never touched.
using System.Diagnostics;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Evection.GUI;
using Evection.GUI.Services;
using Evection.GUI.ViewModels;
using Evection.GUI.Views;

var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "snapshots");
var gameName = args.Length > 1 ? args[1] : null;
Directory.CreateDirectory(outDir);
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(Path.GetTempPath(), "evection-snapshots-" + Environment.ProcessId));

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

CoverView.SkipAnimations = true;
var services = ((App)Application.Current!).Services;
var main = new MainWindowViewModel(services);
var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
window.Show();

void Pump(int ms = 400)
{
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < ms)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(16);
    }
}

void WaitFor(Func<bool> condition, int timeoutMs = 120_000)
{
    var sw = Stopwatch.StartNew();
    while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        Pump(50);
}

void Shot(string name)
{
    Pump();
    var frame = window.CaptureRenderedFrame();
    var path = Path.Combine(outDir, name + ".png");
    frame?.Save(path);
    Console.WriteLine("  " + path);
}

Shot("01-cover");
main.Screen = new SignInViewModel(main);
Shot("02-sign-in");

var shell = main.Shell();
main.Screen = shell;
WaitFor(() => services.Library.HasScanned);
Shot("03-home");

shell.GoGamesCommand.Execute(null);
Shot("04-games-grid");
shell.Games.GridView = false;
Shot("05-games-list");
shell.Games.GridView = true;

var game = services.Library.Games.FirstOrDefault(g => gameName != null && g.Name.Contains(gameName, StringComparison.OrdinalIgnoreCase))
           ?? services.Library.Games.FirstOrDefault(g => g.EvectionInstalled)
           ?? services.Library.Games.FirstOrDefault();
if (game != null)
{
    shell.OpenGame(game);
    var page = (GamePageViewModel)shell.CurrentPage;
    Shot("06-game-overview");
    page.SelectedTab = 1; Shot("07-game-mods");
    page.SelectedTab = 2; Shot("08-game-mod-settings");
    page.SelectedTab = 3; Pump(1600); Shot("09-game-logs");
    page.SelectedTab = 4; Shot("10-game-advanced");
    page.SelectedTab = 0;

    shell.GoCodeBrowserCommand.Execute(null);
    Shot("11-code-browser-empty");
    shell.CodeBrowser.SelectGame(game);
    WaitFor(() => shell.CodeBrowser.IsLoaded || !shell.CodeBrowser.IsLoading);
    shell.CodeBrowser.Query = "Movement";
    Pump(1500);
    shell.CodeBrowser.SelectedResult = shell.CodeBrowser.Results.FirstOrDefault();
    WaitFor(() => shell.CodeBrowser.Source.Length > 0, 30_000);
    shell.CodeBrowser.DetailTab = 1;
    Shot("12-code-browser");
}

shell.GoSettingsCommand.Execute(null);
for (var i = 0; i < 5; i++)
{
    shell.Settings.SelectedTab = i;
    Shot($"13-settings-{i}-{new[] { "general", "appearance", "account", "advanced", "about" }[i]}");
}

shell.GoHomeCommand.Execute(null);
_ = shell.ConfirmAsync("remove evection hook from ULTRAKILL?", "your mods are kept.", "remove", danger: true);
Shot("14-dialog");
shell.CancelDialog();
services.Toasts.Show("Hello Mod off", ToastKind.Info, "undo", () => { });
Shot("15-toast");

App.ApplyTheme(AppTheme.Light);
Shot("16-home-light");
shell.GoSettingsCommand.Execute("0");
Shot("17-settings-light");

Console.WriteLine("Done.");
