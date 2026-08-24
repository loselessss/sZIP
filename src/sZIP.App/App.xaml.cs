using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.Win32;
using sZIP.Application;
using Forms = System.Windows.Forms;

namespace sZIP.App;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _automaticArchiveExtractionMenuItem;
    private Forms.ToolStripMenuItem? _startWithWindowsMenuItem;
    private Forms.ToolStripMenuItem? _shellIntegrationMenuItem;
    private Icon? _applicationIcon;
    private bool _isExiting;
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private CancellationTokenSource? _instanceListenerCancellation;
    private DispatcherTimer? _commandDebounceTimer;
    private DispatcherTimer? _updatePollTimer;
    private GitHubUpdateService? _updateService;
    private readonly UpdateCheckSchedule _updateSchedule = new();
    private bool _isProcessingCommands;
    private bool _isCheckingForUpdates;

    private const string MutexName = @"Local\sZIP.Singleton.v1";
    private const string ShowWindowEventName = @"Local\sZIP.ShowWindow.v1";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        DiagnosticLog.Write("application.start");

        if (e.Args.Any(argument => string.Equals(
                argument, "--register-modern-shell", StringComparison.OrdinalIgnoreCase)))
        {
            ShellIntegration.SetModernContextMenuEnabled(true);
            Shutdown();
            return;
        }
        if (e.Args.Any(argument => string.Equals(
                argument, "--unregister-modern-shell", StringComparison.OrdinalIgnoreCase)))
        {
            ShellIntegration.SetModernContextMenuEnabled(false);
            Shutdown();
            return;
        }

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            QueueCommand(e.Args);
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }

            Shutdown();
            return;
        }

        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        _instanceListenerCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ListenForShowWindow(_instanceListenerCancellation.Token));

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.AutomaticArchiveExtractionEnabledChanged += MainWindow_AutomaticArchiveExtractionEnabledChanged;
        _mainWindow.HiddenToTray += MainWindow_HiddenToTray;
        _mainWindow.UpdateCheckRequested += (_, _) => _ = CheckForUpdatesAsync(manual: true);

        _commandDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _commandDebounceTimer.Tick += (_, _) =>
        {
            _commandDebounceTimer.Stop();
            ProcessQueuedCommands();
        };

        CreateTrayIcon();
        RefreshShellIntegrationRegistration();
        InitializeUpdates();
        _mainWindow.IsAutomaticArchiveExtractionEnabled = UserSettings.Default.AutomaticArchiveExtractionEnabled;
        UpdateTrayState();
        if (!e.Args.Any(argument => string.Equals(argument, "--tray", StringComparison.OrdinalIgnoreCase)))
        {
            _mainWindow.Show();
        }

        if (e.Args.Length > 0 && !e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase))
        {
            QueueCommand(e.Args);
            ScheduleQueuedCommandProcessing();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DiagnosticLog.Write("application.exit");
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _instanceListenerCancellation?.Cancel();
        _commandDebounceTimer?.Stop();
        _updatePollTimer?.Stop();
        _updateService?.Dispose();
        _showWindowEvent?.Set();
        _trayIcon?.Dispose();
        _applicationIcon?.Dispose();
        _showWindowEvent?.Dispose();
        _instanceListenerCancellation?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(ApplySystemTheme));

    private void ApplySystemTheme()
    {
        var light = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
        }

        var colors = light
            ? new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#F4F6FA", ["SurfaceBrush"] = "#FFFFFF",
                ["SurfaceHoverBrush"] = "#F1F5FB", ["SubtleSurfaceBrush"] = "#F8FAFD",
                ["BorderBrush"] = "#D9E0EA", ["DividerBrush"] = "#E9EDF3",
                ["ProgressTrackBrush"] = "#DFE5ED", ["TextBrush"] = "#182230",
                ["MutedTextBrush"] = "#647185", ["AccentBrush"] = "#356AE6",
                ["AccentHoverBrush"] = "#285BCB", ["AccentSoftBrush"] = "#E8EFFF",
                ["DangerBrush"] = "#C63C3C"
            }
            : new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#11161E", ["SurfaceBrush"] = "#1B222C",
                ["SurfaceHoverBrush"] = "#252E3A", ["SubtleSurfaceBrush"] = "#202833",
                ["BorderBrush"] = "#343E4C", ["DividerBrush"] = "#2C3541",
                ["ProgressTrackBrush"] = "#313B49", ["TextBrush"] = "#EDF2F7",
                ["MutedTextBrush"] = "#9DA9B8", ["AccentBrush"] = "#78A5FF",
                ["AccentHoverBrush"] = "#91B6FF", ["AccentSoftBrush"] = "#243454",
                ["DangerBrush"] = "#FF9292"
            };
        foreach (var pair in colors)
        {
            Resources[pair.Key] = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(pair.Value));
        }
    }

    private void CreateTrayIcon()
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        _applicationIcon = executablePath is null
            ? (Icon)SystemIcons.Application.Clone()
            : Icon.ExtractAssociatedIcon(executablePath) ?? (Icon)SystemIcons.Application.Clone();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open sZIP", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());

        _automaticArchiveExtractionMenuItem = new Forms.ToolStripMenuItem("Automatic Archive Extraction")
        {
            CheckOnClick = false
        };
        _automaticArchiveExtractionMenuItem.Click += (_, _) =>
        {
            if (_mainWindow is not null)
            {
                _mainWindow.IsAutomaticArchiveExtractionEnabled = !_mainWindow.IsAutomaticArchiveExtractionEnabled;
            }
        };
        menu.Items.Add(_automaticArchiveExtractionMenuItem);
        _startWithWindowsMenuItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = false
        };
        _startWithWindowsMenuItem.Click += (_, _) => ToggleStartWithWindows();
        menu.Items.Add(_startWithWindowsMenuItem);
        _shellIntegrationMenuItem = new Forms.ToolStripMenuItem("Explorer menu integration")
        {
            CheckOnClick = false
        };
        _shellIntegrationMenuItem.Click += (_, _) => ToggleShellIntegration();
        menu.Items.Add(_shellIntegrationMenuItem);
        menu.Items.Add("Check for Updates", null, (_, _) =>
            Dispatcher.BeginInvoke(new Action(() => _ = CheckForUpdatesAsync(manual: true))));
        menu.Items.Add("Open Diagnostic Log Folder", null, (_, _) => OpenDiagnosticLogFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "sZIP",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        UpdateStartupState();
        UpdateShellIntegrationState();
    }

    private void MainWindow_AutomaticArchiveExtractionEnabledChanged(object? sender, EventArgs e)
    {
        if (_mainWindow is null)
        {
            return;
        }

        UserSettings.Default.AutomaticArchiveExtractionEnabled = _mainWindow.IsAutomaticArchiveExtractionEnabled;
        try
        {
            UserSettings.Default.Save();
        }
        catch
        {
            // Do not stop the current automatic archive extraction task if saving settings fails.
        }

        UpdateTrayState();
    }

    private void MainWindow_HiddenToTray(object? sender, EventArgs e)
    {
        if (_trayIcon is null || UserSettings.Default.TrayHintShown)
        {
            return;
        }

        _trayIcon.ShowBalloonTip(
            2500,
            "sZIP is running in the tray",
            "Automatic archive extraction will keep running.",
            Forms.ToolTipIcon.Info);
        UserSettings.Default.TrayHintShown = true;
        try
        {
            UserSettings.Default.Save();
        }
        catch
        {
        }
    }

    private void UpdateTrayState()
    {
        if (_mainWindow is null)
        {
            return;
        }

        var enabled = _mainWindow.IsAutomaticArchiveExtractionEnabled;
        if (_automaticArchiveExtractionMenuItem is not null)
        {
            _automaticArchiveExtractionMenuItem.Checked = enabled;
            _automaticArchiveExtractionMenuItem.Text = enabled
                ? "Automatic Archive Extraction: On"
                : "Automatic Archive Extraction: Off";
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Text = enabled
                ? "sZIP - Automatic Archive Extraction On"
                : "sZIP - Automatic Archive Extraction Off";
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void ToggleStartWithWindows()
    {
        try
        {
            StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);
            UpdateStartupState();
        }
        catch (Exception exception)
        {
            _trayIcon?.ShowBalloonTip(
                3000,
                "Startup Setting Failed",
                exception.Message,
                Forms.ToolTipIcon.Error);
        }
    }

    private void UpdateStartupState()
    {
        if (_startWithWindowsMenuItem is not null)
        {
            _startWithWindowsMenuItem.Checked = StartupRegistration.IsEnabled;
        }
    }

    private void ListenForShowWindow(CancellationToken cancellationToken)
    {
        if (_showWindowEvent is null)
        {
            return;
        }

        var handles = new[] { _showWindowEvent, cancellationToken.WaitHandle };
        while (!cancellationToken.IsCancellationRequested)
        {
            if (WaitHandle.WaitAny(handles) != 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(ShowMainWindow));
            Dispatcher.BeginInvoke(new Action(ScheduleQueuedCommandProcessing));
        }
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
        }

        if (_mainWindow is not null)
        {
            _mainWindow.AllowExit();
            _mainWindow.Close();
        }

        Shutdown();
    }

    private void ToggleShellIntegration()
    {
        try
        {
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("Could not determine the executable path.");
            ShellIntegration.SetEnabled(!ShellIntegration.IsEnabled, executablePath);
            UpdateShellIntegrationState();
        }
        catch (Exception exception)
        {
            _trayIcon?.ShowBalloonTip(3000, "Explorer Menu Setup Failed", exception.Message, Forms.ToolTipIcon.Error);
        }
    }

    private void UpdateShellIntegrationState()
    {
        if (_shellIntegrationMenuItem is not null)
        {
            _shellIntegrationMenuItem.Checked = ShellIntegration.IsEnabled;
        }
    }

    private static void RefreshShellIntegrationRegistration()
    {
        if (!ShellIntegration.IsEnabled)
        {
            return;
        }

        try
        {
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (executablePath is not null)
            {
                ShellIntegration.SetEnabled(true, executablePath);
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("shell-integration.refresh.failed", exception);
        }
    }

    private void OpenDiagnosticLogFolder()
    {
        try
        {
            Directory.CreateDirectory(DiagnosticLog.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", DiagnosticLog.LogDirectory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("diagnostic-folder.open.failed", exception);
        }
    }

    private void InitializeUpdates()
    {
        var versionText = typeof(App).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
        if (!ReleaseVersion.TryParseTag(versionText, out var currentVersion))
        {
            DiagnosticLog.Write("update.version.invalid");
            return;
        }

        _updateService = new GitHubUpdateService(currentVersion,
            releaseNotesLanguage: CultureInfo.CurrentUICulture.Name);
        foreach (var removed in _updateService.CleanupDownloads())
        {
            DiagnosticLog.Write("update.download.removed " + Path.GetFileName(removed));
        }

        _updatePollTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _updatePollTimer.Tick += (_, _) => _ = CheckForUpdatesAsync(manual: false);
        _updatePollTimer.Start();

        var initialTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        initialTimer.Tick += (_, _) =>
        {
            initialTimer.Stop();
            _ = CheckForUpdatesAsync(manual: false);
        };
        initialTimer.Start();
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateService is null || _isCheckingForUpdates)
        {
            return;
        }
        if (!manual && !_updateSchedule.IsDue(
                UserSettings.Default.LastUpdateCheckUtc, DateTimeOffset.UtcNow))
        {
            return;
        }

        _isCheckingForUpdates = true;
        try
        {
            var update = await _updateService.CheckAsync();
            UserSettings.Default.LastUpdateCheckUtc =
                UpdateCheckSchedule.MarkChecked(DateTimeOffset.UtcNow);
            TrySaveSettings();

            if (update is null)
            {
                if (manual)
                {
                    System.Windows.MessageBox.Show(_mainWindow, "You are using the latest version.",
                        "sZIP Update", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }
            if (!manual && UpdateCheckSchedule.IsSkipped(
                    UserSettings.Default.SkippedUpdateVersion, update.Version))
            {
                return;
            }

            var dialog = new UpdateDialog(_updateService, update) { Owner = _mainWindow };
            var install = dialog.ShowDialog();
            if (dialog.SkipVersionRequested)
            {
                UserSettings.Default.SkippedUpdateVersion = update.Version.ToString();
                TrySaveSettings();
            }
            if (install == true && dialog.InstallerPath is not null)
            {
                try
                {
                    DiagnosticLog.Write("update.installer.launch " + Path.GetFileName(dialog.InstallerPath));
                    GitHubUpdateService.LaunchInstaller(dialog.InstallerPath);
                    ExitApplication();
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Write("update.installer.launch.failed", exception);
                    System.Windows.MessageBox.Show(_mainWindow,
                        "Could not run the installer.\n\n" + dialog.InstallerPath + "\n\n" + exception.Message,
                        "Update Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("update.check.failed", exception);
            if (manual)
            {
                System.Windows.MessageBox.Show(_mainWindow, exception.Message, "Update Check Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private static void TrySaveSettings()
    {
        try
        {
            UserSettings.Default.Save();
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("settings.save.failed", exception);
        }
    }

    private static string CommandQueuePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sZIP",
        "commands");

    private static void QueueCommand(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(CommandQueuePath);
            File.WriteAllLines(
                Path.Combine(CommandQueuePath, Guid.NewGuid().ToString("N") + ".cmd"),
                arguments);
        }
        catch
        {
        }
    }

    private void ScheduleQueuedCommandProcessing()
    {
        if (_commandDebounceTimer is null)
        {
            return;
        }

        _commandDebounceTimer.Stop();
        _commandDebounceTimer.Start();
    }

    private async void ProcessQueuedCommands()
    {
        if (_isProcessingCommands || _mainWindow is null || !Directory.Exists(CommandQueuePath))
        {
            return;
        }

        _isProcessingCommands = true;
        try
        {
            var commands = new List<IReadOnlyList<string>>();
            foreach (var commandPath in Directory.EnumerateFiles(CommandQueuePath, "*.cmd")
                         .OrderBy(path => File.GetCreationTimeUtc(path)))
            {
                try
                {
                    commands.Add(File.ReadAllLines(commandPath));
                    File.Delete(commandPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            var batch = ShellCommandBatch.Create(commands);
            if (batch.CompressionPaths.Count > 0)
            {
                await _mainWindow.HandleCommandLineAsync(
                    new[] { "--compress" }.Concat(batch.CompressionPaths).ToArray());
            }

            if (batch.DirectExtractionPaths.Count > 0)
            {
                await _mainWindow.HandleCommandLineAsync(
                    new[] { "--extract-direct" }.Concat(batch.DirectExtractionPaths).ToArray());
            }

            if (batch.SmartExtractionPaths.Count > 0)
            {
                await _mainWindow.HandleCommandLineAsync(
                    new[] { "--extract-smart" }.Concat(batch.SmartExtractionPaths).ToArray());
            }

            foreach (var command in batch.OtherCommands)
            {
                await _mainWindow.HandleCommandLineAsync(command);
            }
        }
        finally
        {
            _isProcessingCommands = false;
            if (Directory.Exists(CommandQueuePath)
                && Directory.EnumerateFiles(CommandQueuePath, "*.cmd").Any())
            {
                ScheduleQueuedCommandProcessing();
            }
        }
    }
}
