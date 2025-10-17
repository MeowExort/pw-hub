using System.Windows;
using Pw.Hub.Models;

namespace Pw.Hub.Pages;

public partial class EditSquadWindow
{
    private readonly Squad _squad;
    public string SquadName { get; private set; } = string.Empty;

    public EditSquadWindow(Squad squad)
    {
        InitializeComponent();
        _squad = squad;
        SquadNameTextBox.Text = squad.Name;
        SquadNameTextBox.SelectAll();
        SquadNameTextBox.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
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
