using System.Windows;

namespace sZIP.App;

public partial class RenameEntryDialog : Window
{
    public RenameEntryDialog() : this(string.Empty) { }

    public RenameEntryDialog(string currentName)
    {
        InitializeComponent();
        NameTextBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    public string NewName => NameTextBox.Text.Trim();

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            System.Windows.MessageBox.Show(this, Localization.T("InvalidEntryName"),
                Localization.T("RenameEntry"), MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }
        DialogResult = true;
    }
}
