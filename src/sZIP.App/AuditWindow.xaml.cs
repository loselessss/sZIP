using System.Diagnostics;
using System.IO;
using System.Windows;

namespace sZIP.App;

public partial class AuditWindow : Window
{
    public AuditWindow()
    {
        InitializeComponent();
        AuditPathText.Text = AutomaticArchiveExtractionAudit.AuditPath;
        Localization.Changed += Localization_Changed;
        Closed += (_, _) => Localization.Changed -= Localization_Changed;
        Refresh();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Localization_Changed(object? sender, EventArgs e) => Refresh();

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
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
            DiagnosticLog.Write("audit-folder.open.failed", exception);
        }
    }

    private void Refresh() =>
        AuditGrid.ItemsSource = AutomaticArchiveExtractionAudit.ReadRecent();
}
