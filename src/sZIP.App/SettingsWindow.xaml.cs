using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace sZIP.App;

public partial class SettingsWindow : Window
{
    private bool _changingShell;
    private bool _closed;
    private string _lastValidWatchIntervalText = "10";
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
        WatchIntervalTextBox.Text = Math.Max(1, settings.AutomaticArchiveExtractionWatchIntervalSeconds)
            .ToString(CultureInfo.InvariantCulture);
        DeleteSourceCheckBox.IsChecked = settings.AutomaticArchiveExtractionDeleteSourceArchive;
        StartupCheckBox.IsChecked = StartupRegistration.IsEnabled;
        ShellIntegrationCheckBox.IsChecked = settings.ExplorerIntegrationEnabled;
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

    private void PrivacyPolicy_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, Localization.T("PrivacyPolicy"),
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

    private void WatchIntervalStep_Click(object sender, RoutedEventArgs e) =>
        ChangeWatchInterval(int.Parse((string)((RepeatButton)sender).Tag, CultureInfo.InvariantCulture));

    private void ChangeWatchInterval(int step)
    {
        if (!TryParseWatchInterval(WatchIntervalTextBox.Text, out var seconds))
            seconds = Math.Max(1, UserSettings.Default.AutomaticArchiveExtractionWatchIntervalSeconds);
        WatchIntervalTextBox.Text = Math.Max(1L, Math.Min(int.MaxValue, (long)seconds + step))
            .ToString(CultureInfo.InvariantCulture);
    }

    private void WatchInterval_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Up && e.Key != Key.Down) return;
        ChangeWatchInterval(e.Key == Key.Up ? 1 : -1);
        e.Handled = true;
    }

    private void WatchInterval_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !TryParseWatchInterval(GetEditedWatchIntervalText(e.Text), out _);

    private string GetEditedWatchIntervalText(string text) => WatchIntervalTextBox.Text
        .Remove(WatchIntervalTextBox.SelectionStart, WatchIntervalTextBox.SelectionLength)
        .Insert(WatchIntervalTextBox.SelectionStart, text);

    private static bool TryParseWatchInterval(string text, out int seconds) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seconds) && seconds > 0;

    private void WatchInterval_TextChanged(object sender, TextChangedEventArgs e)
    {
        var field = (System.Windows.Controls.TextBox)sender;
        if (field.Text.Length == 0) return;
        if (TryParseWatchInterval(field.Text, out _))
        {
            _lastValidWatchIntervalText = field.Text;
            return;
        }
        var caret = field.CaretIndex;
        field.Text = _lastValidWatchIntervalText;
        field.CaretIndex = Math.Min(caret, field.Text.Length);
    }

    private void WatchInterval_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (WatchIntervalTextBox.Text.Length == 0) WatchIntervalTextBox.Text = _lastValidWatchIntervalText;
    }

    private void WatchInterval_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) as string;
        if (text is null || !TryParseWatchInterval(GetEditedWatchIntervalText(text), out _))
            e.CancelCommand();
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
        if (!TryParseWatchInterval(WatchIntervalTextBox.Text, out var watchInterval))
        {
            ShowValidation("InvalidWatchInterval");
            WatchIntervalTextBox.Focus();
            WatchIntervalTextBox.SelectAll();
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
        var oldExplorerIntegration = settings.ExplorerIntegrationEnabled;
        var oldFolder = settings.AutomaticArchiveExtractionFolder;
        var oldMaxMb = settings.AutomaticArchiveExtractionMaxArchiveMb;
        var oldWatchInterval = settings.AutomaticArchiveExtractionWatchIntervalSeconds;
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
            settings.ExplorerIntegrationEnabled = ShellIntegrationCheckBox.IsChecked == true;
            settings.AutomaticArchiveExtractionFolder = Path.GetFullPath(folder);
            settings.AutomaticArchiveExtractionMaxArchiveMb = maxMb;
            settings.AutomaticArchiveExtractionWatchIntervalSeconds = watchInterval;
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
            settings.ExplorerIntegrationEnabled = oldExplorerIntegration;
            settings.AutomaticArchiveExtractionFolder = oldFolder;
            settings.AutomaticArchiveExtractionMaxArchiveMb = oldMaxMb;
            settings.AutomaticArchiveExtractionWatchIntervalSeconds = oldWatchInterval;
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
