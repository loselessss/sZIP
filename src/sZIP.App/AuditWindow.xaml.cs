using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;

namespace sZIP.App;

public partial class AuditWindow : Window
{
    private readonly Func<string, Window, Task>? _retry;
    private bool _busy;

    public AuditWindow() : this(null) { }

    internal AuditWindow(Func<string, Window, Task>? retry)
    {
        _retry = retry;
        InitializeComponent();
        AuditPathText.Text = AutomaticArchiveExtractionAudit.AuditPath;
        Localization.Changed += Localization_Changed;
        Closed += (_, _) => Localization.Changed -= Localization_Changed;
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
        Refresh();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Localization_Changed(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        AuditGrid.ItemsSource = AutomaticArchiveExtractionAudit.ReadRecent();
        UpdateActions();
    }

    private void AuditGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActions();

    private void UpdateActions()
    {
        if (RetryButton is null || OpenOutputButton is null) return;
        var entry = AuditGrid.SelectedItem as AutomaticArchiveExtractionAuditEntry;
        RetryButton.IsEnabled = !_busy && _retry is not null && entry?.CanRetry == true
            && File.Exists(entry.ArchivePath);
        OpenOutputButton.IsEnabled = !_busy && entry is not null && Directory.Exists(entry.OutputPath);
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _retry is null || AuditGrid.SelectedItem is not AutomaticArchiveExtractionAuditEntry entry) return;
        _busy = true;
        RefreshButton.IsEnabled = false;
        AuditGrid.IsEnabled = false;
        RetryStatusText.Text = Localization.T("RetryExtracting");
        UpdateActions();
        try
        {
            await _retry(entry.ArchivePath, this);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, Localization.Error(exception.Message), Localization.T("OperationFailed"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
            RefreshButton.IsEnabled = true;
            AuditGrid.IsEnabled = true;
            RetryStatusText.Text = string.Empty;
            Refresh();
            if (AuditGrid.Items.Count > 0) AuditGrid.SelectedIndex = 0;
        }
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (AuditGrid.SelectedItem is not AutomaticArchiveExtractionAuditEntry entry) return;
        try
        {
            if (!Directory.Exists(entry.OutputPath)) throw new DirectoryNotFoundException(Localization.T("OutputFolderMissing"));
            Process.Start(new ProcessStartInfo(entry.OutputPath) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, Localization.Error(exception.Message), Localization.T("AuditTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateActions();
        }
    }
}
