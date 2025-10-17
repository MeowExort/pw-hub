using System.Windows;
using Pw.Hub.Models;

namespace Pw.Hub.Pages;

public partial class EditAccountWindow
{
    private readonly Account _account;
    public string AccountName { get; private set; } = string.Empty;
    public string? Email { get; private set; }

    public EditAccountWindow(Account account)
    {
        InitializeComponent();
        _account = account;
        AccountNameTextBox.Text = account.Name;
        EmailTextBox.Text = account.Email ?? string.Empty;
        AccountNameTextBox.SelectAll();
        AccountNameTextBox.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
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
