using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Pages;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;

namespace Pw.Hub;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly MainViewModel _vm = new();
    private object? _contextMenuTarget;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private static void CollapseSiblings(TreeViewItem item)
    {
        var parent = ItemsControl.ItemsControlFromItemContainer(item);
        if (parent == null) return;

        foreach (var obj in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(obj) is TreeViewItem sibling &&
                !ReferenceEquals(sibling, item))
            {
                sibling.IsExpanded = false;
            }
        }
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        // Находим элемент TreeView под курсором мыши
        _contextMenuTarget = null;

        var mousePosition = Mouse.GetPosition(NavigationTree);
        var hitTestResult = VisualTreeHelper.HitTest(NavigationTree, mousePosition);

        if (hitTestResult != null)
        {
            var element = hitTestResult.VisualHit;

            // Ищем TreeViewItem вверх по дереву
            while (element != null && element is not TreeViewItem)
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is TreeViewItem treeViewItem)
            {
                _contextMenuTarget = treeViewItem.DataContext;
            }
        }

        // Проверяем тип элемента под курсором
        var isSquad = _contextMenuTarget is Squad;
        var isAccount = _contextMenuTarget is Account;

        // "Добавить аккаунт" доступна только для отрядов
        AddAccountMenuItem.IsEnabled = isSquad;

        // "Редактировать отряд" доступна только для отрядов
        EditSquadMenuItem.IsEnabled = isSquad;

        // "Редактировать аккаунт" доступна только для аккаунтов
        EditAccountMenuItem.IsEnabled = isAccount;

        // "Удалить" доступна для отрядов и аккаунтов
        DeleteMenuItem.IsEnabled = isSquad || isAccount;
        DeleteMenuItem.Header = isSquad ? "Удалить отряд" : isAccount ? "Удалить аккаунт" : "Удалить";
    }

    private void OnAddSquadClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CreateSquadWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            using var db = new AppDbContext();
            var newSquad = new Squad
            {
                Name = dialog.SquadName
            };

            db.Squads.Add(newSquad);
            db.SaveChanges();

            // Перезагружаем данные
            _vm.Reload();
        }
    }

    private void OnAddAccountClick(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not Squad selectedSquad)
        {
            MessageBox.Show("Выберите отряд для добавления аккаунта", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new CreateAccountWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            using var db = new AppDbContext();
            var newAccount = new Account
            {
                Name = dialog.AccountName,
                Email = dialog.Email,
                SquadId = selectedSquad.Id,
                ImageSource = ""
            };

            db.Accounts.Add(newAccount);
            db.SaveChanges();

            // Перезагружаем данные
            var wasExpanded =
                NavigationTree.ItemContainerGenerator.ContainerFromItem(selectedSquad) is TreeViewItem tvi &&
                tvi.IsExpanded;
            _vm.Reload();

            // Раскрываем отряд, чтобы показать новый аккаунт
            if (wasExpanded)
            {
                var reloadedSquad = _vm.Squads.FirstOrDefault(s => s.Id == selectedSquad.Id);
                if (reloadedSquad != null &&
                    NavigationTree.ItemContainerGenerator.ContainerFromItem(reloadedSquad) is TreeViewItem newTvi)
                {
                    newTvi.IsExpanded = true;
                }
            }
        }
    }

    private void NavigationTree_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!AccountPage.IsCoreInitialized)
        {
            return;
        }

        if (e.OriginalSource is ToggleButton)
        {
            return;
        }

        SelectedItemChanged(
            NavigationTree.ItemContainerGenerator.ContainerFromItem((sender as TreeView).SelectedItem) as TreeViewItem);
    }

    private void SelectedItemChanged(TreeViewItem? tvi)
    {
        ControlsList_SelectedItemChanged();
        if (tvi != null)
        {
            tvi.IsExpanded = !tvi.IsExpanded;
        }
    }

    private async void ControlsList_SelectedItemChanged()
    {
        if (NavigationTree.SelectedItem is Account account)
        {
            if (AccountPage.Account?.Id != account.Id)
            {
                // Если уже открыта другая страница аккаунта, обновляем её контекст
                await AccountPage.ChangeAccount(account);
            }

            return;
        }

        if (NavigationTree.SelectedItem is Squad squad)
        {
            if (NavigationTree.ItemContainerGenerator.ContainerFromItem(squad) is not TreeViewItem tvi)
                return;
            tvi.BringIntoView();
            CollapseSiblings(tvi);
        }
    }

    private void OnEditSquadClick(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not Squad selectedSquad)
        {
            MessageBox.Show("Выберите отряд для редактирования", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new EditSquadWindow(selectedSquad)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            // Сохраняем текущий выбранный элемент
            // var currentSelection = NavigationTree.SelectedItem;

            selectedSquad.Name = dialog.SquadName;

            using var db = new AppDbContext();
            db.Update(selectedSquad);
            db.SaveChanges();

            // Обновляем отображение в TreeView
            // NavigationTree.Items.Refresh();

            // Восстанавливаем выбор
            // SetSelectedItem(currentSelection);
        }
    }

    private void OnEditAccountClick(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not Account selectedAccount)
        {
            MessageBox.Show("Выберите аккаунт для редактирования", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new EditAccountWindow(selectedAccount)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            // Сохраняем текущий выбранный элемент
            // var currentSelection = NavigationTree.SelectedItem;

            selectedAccount.Name = dialog.AccountName;
            selectedAccount.Email = dialog.Email;

            using var db = new AppDbContext();
            db.Update(selectedAccount);
            db.SaveChanges();

            // Обновляем отображение в TreeView
            // NavigationTree.Items.Refresh();

            // Восстанавливаем выбор
            // SetSelectedItem(currentSelection);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is Squad selectedSquad)
        {
            var result = MessageBox.Show(
                $"Вы действительно хотите удалить отряд \"{selectedSquad.Name}\"?\nВсе аккаунты в этом отряде также будут удалены.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                using var db = new AppDbContext();
                var squadToDelete = db.Squads.Find(selectedSquad.Id);
                if (squadToDelete != null)
                {
                    db.Squads.Remove(squadToDelete);
                    db.SaveChanges();

                    // Перезагружаем данные
                    _vm.Reload();

                    // Очищаем ContentFrame если был выбран удаленный элемент
                    if (NavigationTree.SelectedItem == selectedSquad ||
                        (NavigationTree.SelectedItem is Account acc && acc.SquadId == selectedSquad.Id))
                    {
                        await AccountPage.ChangeAccount(null);
                    }
                }
            }
        }
        else if (_contextMenuTarget is Account selectedAccount)
        {
            var result = MessageBox.Show(
                $"Вы действительно хотите удалить аккаунт \"{selectedAccount.Name}\"?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                using var db = new AppDbContext();
                var accountToDelete = db.Accounts.Find(selectedAccount.Id);
                if (accountToDelete != null)
                {
                    var squadId = accountToDelete.SquadId;

                    db.Accounts.Remove(accountToDelete);
                    db.SaveChanges();

                    // Сохраняем состояние развернутости отряда
                    var squad = _vm.Squads.FirstOrDefault(s => s.Id == squadId);
                    var wasExpanded = squad != null &&
                                      NavigationTree.ItemContainerGenerator
                                          .ContainerFromItem(squad) is TreeViewItem tvi &&
                                      tvi.IsExpanded;

                    // Перезагружаем данные
                    _vm.Reload();

                    // Восстанавливаем развернутость отряда
                    if (wasExpanded)
                    {
                        var reloadedSquad = _vm.Squads.FirstOrDefault(s => s.Id == squadId);
                        if (reloadedSquad != null &&
                            NavigationTree.ItemContainerGenerator.ContainerFromItem(reloadedSquad) is TreeViewItem
                                newTvi)
                        {
                            newTvi.IsExpanded = true;
                        }
                    }

                    // Очищаем ContentFrame если был выбран удаленный аккаунт
                    if (NavigationTree.SelectedItem == selectedAccount)
                    {
                        await AccountPage.ChangeAccount(null);
                    }
                }
            }
        }
    }

    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public async Task<bool> ChangeAccount(Account account)
    {
        return await AccountPage.ChangeAccount(account);
    }
}