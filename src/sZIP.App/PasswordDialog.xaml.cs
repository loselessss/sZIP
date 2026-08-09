using System.Windows;

namespace sZIP.App;

public partial class PasswordDialog : Window
{
    public PasswordDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public string Password => PasswordInput.Password;

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PasswordInput.Password))
        {
            System.Windows.MessageBox.Show(this, "암호를 입력하세요.", "sZIP",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
