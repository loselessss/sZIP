using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace sZIP.App;

public partial class SettingsWindow : Window
{
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
        ConfigureDeploymentUi(PackageDeployment.IsPackaged);
        Closing += (_, e) => { if (_changingShell) e.Cancel = true; };
        Closed += (_, _) => _closed = true;
        UpdateShellControls();
    }

    private void UpdateShellControls()
    {
        SettingsFields.IsEnabled = !_changingShell;
        SaveButton.IsEnabled = !_changingShell;
        CancelButton.IsEnabled = !_changingShell;
    }

    internal void ConfigureDeploymentUi(bool packaged)
    {
        StartupCheckBox.Visibility = packaged ? Visibility.Collapsed : Visibility.Visible;
        ShellIntegrationCheckBox.Visibility = packaged ? Visibility.Collapsed : Visibility.Visible;
        MsixSettingsPanel.Visibility = packaged ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenStartupSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }); }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, Localization.T("Settings"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (_changingShell) return;
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
            if (!PackageDeployment.IsPackaged && (StartupCheckBox.IsChecked == true) != oldStartup)
            {
                startupChanged = true;
                StartupRegistration.SetEnabled(StartupCheckBox.IsChecked == true);
            }
            if (!PackageDeployment.IsPackaged && (ShellIntegrationCheckBox.IsChecked == true) != oldShell)
            {
                shellChanged = true;
                var enabled = ShellIntegrationCheckBox.IsChecked == true;
                var executable = executablePath ?? throw new InvalidOperationException(Localization.T("ExecutablePathError"));
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
                DiagnosticLog.Write("shell-integration.settings " + shellResult.MessageKey + " " + shellResult.Details);
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
