using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace sZIP.App;

public partial class SettingsWindow : Window
{
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
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
                ShellIntegration.SetEnabled(ShellIntegrationCheckBox.IsChecked == true,
                    executablePath ?? throw new InvalidOperationException(Localization.T("ExecutablePathError")));
            }
            settings.Language = (string)((ComboBoxItem)LanguageComboBox.SelectedItem).Tag;
            settings.AutomaticArchiveExtractionFolder = Path.GetFullPath(folder);
            settings.AutomaticArchiveExtractionMaxArchiveMb = maxMb;
            settings.AutomaticArchiveExtractionDeleteSourceArchive = DeleteSourceCheckBox.IsChecked == true;
            settings.Save();
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
                if (shellChanged && executablePath is not null) ShellIntegration.SetEnabled(oldShell, executablePath);
            }
            catch (Exception rollbackException)
            {
                DiagnosticLog.Write("settings.rollback.failed", rollbackException);
            }
            DiagnosticLog.Write("settings.save.failed", exception);
            System.Windows.MessageBox.Show(this, Localization.Error(exception.Message), Localization.T("SettingsSaveFailed"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowValidation(string key) => System.Windows.MessageBox.Show(this, Localization.T(key),
        Localization.T("Settings"), MessageBoxButton.OK, MessageBoxImage.Warning);
}
