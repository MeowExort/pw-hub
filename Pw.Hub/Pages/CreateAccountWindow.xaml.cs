using System.Windows;

namespace Pw.Hub.Pages;

public partial class CreateAccountWindow
{
    public string AccountName { get; private set; } = string.Empty;

    public CreateAccountWindow()
    {
        InitializeComponent();
        AccountNameTextBox.Focus();
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var name = AccountNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Введите название аккаунта", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AccountName = name;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
