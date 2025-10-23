using System.Windows;
using Pw.Hub.Models;

namespace Pw.Hub.Pages;

public partial class EditAccountWindow
{
    private readonly Account _account;
    public string AccountName { get; private set; } = string.Empty;
    public string Email { get; private set; }

    public EditAccountWindow(Account account)
    {
        InitializeComponent();
        _account = account;
        AccountNameTextBox.Text = account.Name;

        // Bind servers list (if any). Two-way binding in XAML will update DefaultCharacterOptionId
        ServersList.ItemsSource = _account.Servers ?? new List<AccountServer>();

        AccountNameTextBox.SelectAll();
        AccountNameTextBox.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
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
