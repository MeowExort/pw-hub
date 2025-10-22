using System.Windows;

namespace Pw.Hub.Pages;

public partial class CreateAccountWindow
{
    public string AccountName { get; private set; } = string.Empty;
    public string Email { get; private set; }

    public CreateAccountWindow()
    {
        InitializeComponent();
        AccountNameTextBox.Focus();
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var name = AccountNameTextBox.Text.Trim();
        var email = EmailTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Введите название аккаунта", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AccountName = name;
        Email = string.IsNullOrWhiteSpace(email) ? null : email;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
