using System.IO;
using System.Windows;
using sZIP.Application;

namespace sZIP.App;

internal static class StartupSmokeTest
{
    internal static void Run(App app, string reportPath)
    {
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow? main = null;
        UpdateDialog? update = null;
        GitHubUpdateService? service = null;

        void Finish(Exception? error)
        {
            try
            {
                update?.Close();
                main?.AllowExit();
                main?.Close();
                service?.Dispose();
                File.WriteAllText(reportPath, error is null
                    ? "PASS: main window and update window rendered."
                    : "FAIL: " + error);
            }
            finally
            {
                app.Shutdown(error is null ? 0 : 1);
            }
        }

        try
        {
            main = new MainWindow();
            Configure(main);
            app.MainWindow = main;
            main.ContentRendered += (_, _) =>
            {
                try
                {
                    var current = typeof(App).Assembly.GetName().Version!;
                    var version = new ReleaseVersion(current.Major, current.Minor, current.Build);
                    service = new GitHubUpdateService(version);
                    var available = new AvailableUpdate(version, "smoke-test", "sZIP",
                        "Startup check", new Uri("https://example.invalid/"), string.Empty, null);
                    update = new UpdateDialog(service, available) { Owner = main };
                    Configure(update);
                    update.ContentRendered += (_, _) => Finish(null);
                    update.Show();
                }
                catch (Exception error) { Finish(error); }
            };
            main.Show();
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Configure(Window window)
    {
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.Opacity = 0;
    }
}
