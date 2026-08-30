using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace sZIP.App;

public partial class SettingsWindow : Window
{
    private bool _checkingShell;
    private bool _changingShell;
    private bool _closed;
    public bool HasSavedSettings { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();
        var settings = UserSettings.Default;
        LanguageComboBox.SelectedItem = LanguageComboBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => (string)item.Tag == settings.Language) ?? LanguageComboBox.Items[0];
        WatchFolderTextBox.Text = string.IsNullOrWhiteSpace(settings.AutomaticArchiveExtractionFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            : settings.AutomaticArchiveExtractionFolder;
        MaxArchiveMbTextBox.Text = settings.AutomaticArchiveExtractionMaxArchiveMb.ToString(CultureInfo.InvariantCulture);
        DeleteSourceCheckBox.IsChecked = settings.AutomaticArchiveExtractionDeleteSourceArchive;
        StartupCheckBox.IsChecked = StartupRegistration.IsEnabled;
        ShellIntegrationCheckBox.IsChecked = ShellIntegration.IsEnabled;
        ShellIntegrationCheckBox.Checked += (_, _) => UpdateShellControls();
        ShellIntegrationCheckBox.Unchecked += (_, _) => UpdateShellControls();
        Loaded += async (_, _) => await RefreshShellStatusAsync();
        Closing += (_, e) => { if (_changingShell) e.Cancel = true; };
        Closed += (_, _) => _closed = true;
        UpdateShellControls();
    }

    private void UpdateShellControls()
    {
        var busy = _checkingShell || _changingShell;
        SettingsFields.IsEnabled = !_changingShell;
        RepairShellButton.IsEnabled = !busy && ShellIntegrationCheckBox.IsChecked == true;
        RefreshShellButton.IsEnabled = !busy;
        SaveButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !_changingShell;
        ShellBusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowShellResult(ShellIntegrationResult result)
    {
        ShellStatusText.Text = Localization.T(result.MessageKey);
        ShellDetailsText.Text = result.Details;
        ShellDetailsExpander.Visibility = string.IsNullOrWhiteSpace(result.Details)
            ? Visibility.Collapsed : Visibility.Visible;
        ShellDetailsExpander.IsExpanded = false;
    }

    private async Task RefreshShellStatusAsync()
    {
        if (_checkingShell || _changingShell || _closed) return;
        _checkingShell = true;
        UpdateShellControls();
        ShellStatusText.Text = Localization.T("ShellChecking");
        try
        {
            var executable = Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException(Localization.T("ExecutablePathError"));
            var result = await Task.Run(() => ShellIntegration.GetStatus(executable));
            if (!_closed) ShowShellResult(result);
        }
        catch (Exception exception)
        {
            if (!_closed) ShowShellResult(new ShellIntegrationResult("ShellStatusCheckFailed", false, exception.Message));
        }
        finally
        {
            _checkingShell = false;
            if (!_closed) UpdateShellControls();
        }
    }

    private async void RefreshShell_Click(object sender, RoutedEventArgs e) => await RefreshShellStatusAsync();

    private async void RepairShell_Click(object sender, RoutedEventArgs e)
    {
        if (_checkingShell || _changingShell || ShellIntegrationCheckBox.IsChecked != true) return;
        _changingShell = true;
        UpdateShellControls();
        ShellStatusText.Text = Localization.T("ShellRepairing");
        ShellDetailsExpander.Visibility = Visibility.Collapsed;
        try
        {
            var executable = Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException(Localization.T("ExecutablePathError"));
            var result = await Task.Run(() =>
            {
                var registration = ShellIntegration.SetEnabled(true, executable, forceModernRepair: true);
                return registration.Success ? ShellIntegration.GetStatus(executable) : registration;
            });
            ShowShellResult(result);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("shell-integration.repair.failed", exception);
            ShowShellResult(new ShellIntegrationResult("ShellStatusFailed", false, exception.Message));
        }
        finally
        {
            _changingShell = false;
            UpdateShellControls();
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Localization.T("SelectWatchFolder"),
            SelectedPath = WatchFolderTextBox.Text,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            WatchFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_checkingShell || _changingShell) return;
        if (!int.TryParse(MaxArchiveMbTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxMb)
            || maxMb < 1 || maxMb > 10240)
        {
            ShowValidation("InvalidArchiveSize");
            MaxArchiveMbTextBox.Focus();
            return;
        }
        var folder = WatchFolderTextBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            ShowValidation("InvalidWatchFolder");
            WatchFolderTextBox.Focus();
            return;
        }

        var settings = UserSettings.Default;
        var oldLanguage = settings.Language;
        var oldFolder = settings.AutomaticArchiveExtractionFolder;
        var oldMaxMb = settings.AutomaticArchiveExtractionMaxArchiveMb;
        var oldDeleteSource = settings.AutomaticArchiveExtractionDeleteSourceArchive;
        var oldStartup = StartupRegistration.IsEnabled;
        var oldShell = ShellIntegration.IsEnabled;
        var startupChanged = false;
        var shellChanged = false;
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        ShellIntegrationResult? shellResult = null;
        _changingShell = true;
        UpdateShellControls();
        try
        {
            if ((StartupCheckBox.IsChecked == true) != oldStartup)
            {
                startupChanged = true;
                StartupRegistration.SetEnabled(StartupCheckBox.IsChecked == true);
            }
            if ((ShellIntegrationCheckBox.IsChecked == true) != oldShell)
            {
                shellChanged = true;
                var enabled = ShellIntegrationCheckBox.IsChecked == true;
                var executable = executablePath ?? throw new InvalidOperationException(Localization.T("ExecutablePathError"));
                ShellStatusText.Text = Localization.T("ShellApplying");
                shellResult = await Task.Run(() => ShellIntegration.SetEnabled(enabled, executable));
            }
            settings.Language = (string)((ComboBoxItem)LanguageComboBox.SelectedItem).Tag;
            settings.AutomaticArchiveExtractionFolder = Path.GetFullPath(folder);
            settings.AutomaticArchiveExtractionMaxArchiveMb = maxMb;
            settings.AutomaticArchiveExtractionDeleteSourceArchive = DeleteSourceCheckBox.IsChecked == true;
            settings.Save();
            HasSavedSettings = true;
            if (shellResult is not null && !shellResult.Success)
            {
                ShowShellResult(shellResult);
                System.Windows.MessageBox.Show(this, Localization.T("ShellSettingsSavedWithWarning"),
                    Localization.T("Settings"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _changingShell = false;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            settings.Language = oldLanguage;
            settings.AutomaticArchiveExtractionFolder = oldFolder;
            settings.AutomaticArchiveExtractionMaxArchiveMb = oldMaxMb;
            settings.AutomaticArchiveExtractionDeleteSourceArchive = oldDeleteSource;
            try
            {
                if (startupChanged) StartupRegistration.SetEnabled(oldStartup);
                if (shellChanged && executablePath is not null)
                    await Task.Run(() => ShellIntegration.SetEnabled(oldShell, executablePath));
            }
            catch (Exception rollbackException)
            {
                DiagnosticLog.Write("settings.rollback.failed", rollbackException);
            }
            DiagnosticLog.Write("settings.save.failed", exception);
            System.Windows.MessageBox.Show(this, Localization.Error(exception.Message), Localization.T("SettingsSaveFailed"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _changingShell = false;
            if (!_closed) UpdateShellControls();
        }
    }

    private void ShowValidation(string key) => System.Windows.MessageBox.Show(this, Localization.T(key),
        Localization.T("Settings"), MessageBoxButton.OK, MessageBoxImage.Warning);
}
