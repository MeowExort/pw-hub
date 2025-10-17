using System.Windows;

namespace Pw.Hub.Pages;

public partial class CreateSquadWindow
{
    public string SquadName { get; private set; } = string.Empty;

    public CreateSquadWindow()
    {
        InitializeComponent();
        SquadNameTextBox.Focus();
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var name = SquadNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Введите название отряда", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SquadName = name;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
