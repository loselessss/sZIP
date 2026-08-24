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
        Refresh();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh();

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
