using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.EntityFrameworkCore;
using NLua;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Pages;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using Pw.Hub.ViewModels;

namespace Pw.Hub;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly MainViewModel _vm = new();
    private object _contextMenuTarget;

    // Drag & drop state for reordering accounts
    private Point _dragStartPoint;
    private Account _draggedAccount;
    private Squad _draggedSquad;

    // Drag visual feedback
    private TreeViewItem _adornerItem;
    private InsertionAdorner _insertionAdorner;

    private readonly ModuleService _moduleService = new();
    private List<ModuleDefinition> _modules = new();

    public MainWindow()
    {
        InitializeComponent();
        
        DataContext = _vm;
        try
        {
            // Subscribe to account change events to sync TreeView selection
            AccountPage.AccountManager.CurrentAccountChanged += OnCurrentAccountChanged;
            // Subscribe to current account data/property changes to mirror updates (e.g., avatar) into VM
            AccountPage.AccountManager.CurrentAccountDataChanged += OnCurrentAccountDataChanged;
            // Subscribe to account changing progress to block UI interactions during switch
            AccountPage.AccountManager.CurrentAccountChanging += OnCurrentAccountChanging;
        }
        catch { }
        Loaded += (_, _) => LoadModules();
    }

    private void OpenProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new Pw.Hub.Windows.ProfileEditWindow
            {
                Owner = this
            };
            win.ShowDialog();
        }
        catch
        {
        }
    }

    private void OnCurrentAccountChanged(Account account)
    {
        if (account == null) return;
        // Ensure run on UI thread
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                // Find account in current ViewModel collections
                var squad = _vm.Squads.FirstOrDefault(s => s.Accounts.Any(a => a.Id == account.Id));
                if (squad == null) return;

                var vmAccount = squad.Accounts.FirstOrDefault(a => a.Id == account.Id);
                if (vmAccount == null) return;

                // Ensure containers are generated
                NavigationTree.UpdateLayout();
                if (NavigationTree.ItemContainerGenerator.ContainerFromItem(squad) is not TreeViewItem squadItem)
                {
                    return;
                }

                // Expand squad to generate children
                squadItem.IsExpanded = true;
                squadItem.UpdateLayout();

                if (squadItem.ItemContainerGenerator.ContainerFromItem(vmAccount) is TreeViewItem accountItem)
                {
                    accountItem.IsSelected = true;
                    accountItem.BringIntoView();
                }
            }
            catch
            {
                // ignore selection errors
            }
        });
    }

    private void OnCurrentAccountDataChanged(Account account)
    {
        if (account == null) return;
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var squad = _vm.Squads.FirstOrDefault(s => s.Accounts.Any(a => a.Id == account.Id));
                if (squad == null) return;
                var vmAccount = squad.Accounts.FirstOrDefault(a => a.Id == account.Id);
                if (vmAccount == null) return;

                // Mirror properties that affect UI visuals
                if (!string.Equals(vmAccount.ImageSource, account.ImageSource, StringComparison.Ordinal))
                    vmAccount.ImageSource = account.ImageSource;
                if (!string.Equals(vmAccount.SiteId, account.SiteId, StringComparison.Ordinal))
                    vmAccount.SiteId = account.SiteId;
                if (!string.Equals(vmAccount.Name, account.Name, StringComparison.Ordinal))
                    vmAccount.Name = account.Name;
            }
            catch
            {
            }
        });
    }

    private void OnCurrentAccountChanging(bool isChanging)
    {
        // Block TreeView interactions while account switching is in progress
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (NavigationTree != null)
                    {
                        // Блокируем взаимодействие без изменения визуального состояния
                        NavigationTree.IsHitTestVisible = !isChanging;
                        NavigationTree.Focusable = !isChanging;
                    }
                    if (StatusBarText != null)
                        StatusBarText.Text = isChanging ? "Смена аккаунта..." : string.Empty;
                }
                catch { }
            });
        }
        catch { }
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
        // "Загрузить персонажей" доступна только для аккаунтов
        LoadCharactersMenuItem.IsEnabled = isAccount;

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
            var maxOrder = db.Squads.Select(s => (int?)s.OrderIndex).Max() ?? -1;
            var newSquad = new Squad
            {
                Name = dialog.SquadName,
                OrderIndex = maxOrder + 1
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
            // determine next order index in this squad
            var maxOrder = db.Accounts.Where(a => a.SquadId == selectedSquad.Id).Select(a => (int?)a.OrderIndex).Max() ?? -1;
            var newAccount = new Account
            {
                Name = dialog.AccountName,
                SquadId = selectedSquad.Id,
                ImageSource = "",
                OrderIndex = maxOrder + 1
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

    private void OnAddAccountInlineClick(object sender, RoutedEventArgs e)
    {
        // Inline add account button inside a Squad item (DataContext is Squad)
        if (sender is not FrameworkElement fe || fe.DataContext is not Squad selectedSquad)
        {
            MessageBox.Show("Выберите отряд для добавления аккаунта", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new CreateAccountWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            using var db = new AppDbContext();
            var newAccount = new Account
            {
                Name = dialog.AccountName,
                SquadId = selectedSquad.Id,
                ImageSource = string.Empty
            };
            db.Accounts.Add(newAccount);
            db.SaveChanges();

            // Try to keep the squad expanded after reload so the new account is visible
            var wasExpanded = NavigationTree.ItemContainerGenerator.ContainerFromItem(selectedSquad) is TreeViewItem tvi && tvi.IsExpanded;
            _vm.Reload();

            var reloadedSquad = _vm.Squads.FirstOrDefault(s => s.Id == selectedSquad.Id);
            if (reloadedSquad != null && NavigationTree.ItemContainerGenerator.ContainerFromItem(reloadedSquad) is TreeViewItem newTvi)
            {
                newTvi.IsExpanded = true;
            }
        }
    }

    private void NavigationTree_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            _dragStartPoint = e.GetPosition(NavigationTree);
            _draggedAccount = null;
            _draggedSquad = null;

            // identify if we pressed on an Account or Squad item
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not TreeViewItem)
                element = VisualTreeHelper.GetParent(element);
            if (element is TreeViewItem tvi)
            {
                if (tvi.DataContext is Account acc)
                {
                    _draggedAccount = acc;
                }
                else if (tvi.DataContext is Squad sq)
                {
                    _draggedSquad = sq;
                }
            }
        }
        catch { }
    }

    private void NavigationTree_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_draggedAccount == null && _draggedSquad == null) return;
            // Respect account switching lock
            try { if (AccountPage?.AccountManager?.IsChanging == true) return; } catch { }

            var pos = e.GetPosition(NavigationTree);
            var diff = new Vector(Math.Abs(pos.X - _dragStartPoint.X), Math.Abs(pos.Y - _dragStartPoint.Y));
            if (diff.X < SystemParameters.MinimumHorizontalDragDistance && diff.Y < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (_draggedAccount != null)
            {
                var dataAcc = new DataObject(typeof(Account), _draggedAccount);
                DragDrop.DoDragDrop(NavigationTree, dataAcc, DragDropEffects.Move);
            }
            else if (_draggedSquad != null)
            {
                var dataSq = new DataObject(typeof(Squad), _draggedSquad);
                DragDrop.DoDragDrop(NavigationTree, dataSq, DragDropEffects.Move);
            }
        }
        catch { }
    }

    private Account _dropTargetAccount;
    private bool _insertAbove;

    // Squad DnD state
    private Squad _dropTargetSquad;
    private bool _insertAboveSquad;

    private void NavigationTree_OnDragOver(object sender, DragEventArgs e)
    {
        try
        {
            // Account drag
            if (e.Data.GetDataPresent(typeof(Account)))
            {
                var dragged = (Account)e.Data.GetData(typeof(Account));
                if (dragged == null) { e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner(); return; }

                // determine target under mouse
                var target = GetAccountOrSquadUnderMouse(e.OriginalSource as DependencyObject);
                if (target.squad == null) { e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner(); return; }

                // allow dropping into any squad; just require a squad target exists
                e.Effects = DragDropEffects.Move;

                // Visualize insertion position
                if (target.account != null)
                {
                    var squadItem = NavigationTree.ItemContainerGenerator.ContainerFromItem(target.squad) as TreeViewItem;
                    TreeViewItem accItem = null;
                    if (squadItem != null)
                    {
                        accItem = squadItem.ItemContainerGenerator.ContainerFromItem(target.account) as TreeViewItem;
                    }
                    if (accItem != null)
                    {
                        var pos = e.GetPosition(accItem);
                        var above = pos.Y < accItem.ActualHeight / 2;
                        _dropTargetAccount = target.account;
                        _insertAbove = above;
                        UpdateInsertionAdorner(accItem, above);
                    }
                    else
                    {
                        ClearInsertionAdorner();
                    }
                }
                else
                {
                    // dropping into squad (end). Show indicator at the end of last account if any
                    var squadVm = _vm.Squads.FirstOrDefault(s => s.Id == target.squad.Id);
                    if (squadVm != null && squadVm.Accounts.Count > 0)
                    {
                        var lastAcc = squadVm.Accounts.Last();
                        var squadItem = NavigationTree.ItemContainerGenerator.ContainerFromItem(target.squad) as TreeViewItem;
                        var lastItem = squadItem?.ItemContainerGenerator.ContainerFromItem(lastAcc) as TreeViewItem;
                        if (lastItem != null)
                        {
                            _dropTargetAccount = lastAcc;
                            _insertAbove = false; // after last
                            UpdateInsertionAdorner(lastItem, false);
                        }
                        else
                        {
                            ClearInsertionAdorner();
                        }
                    }
                    else
                    {
                        ClearInsertionAdorner();
                    }
                }

                e.Handled = true;
                return;
            }

            // Squad drag
            if (e.Data.GetDataPresent(typeof(Squad)))
            {
                var draggedSquad = (Squad)e.Data.GetData(typeof(Squad));
                if (draggedSquad == null) { e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner(); return; }

                // find squad item under mouse
                TreeViewItem targetItem = null;
                Squad targetSquad = null;
                var dep = e.OriginalSource as DependencyObject;
                while (dep != null && dep is not TreeViewItem) dep = VisualTreeHelper.GetParent(dep);
                if (dep is TreeViewItem tvi)
                {
                    if (tvi.DataContext is Squad sq)
                    {
                        targetItem = tvi; targetSquad = sq;
                    }
                }

                e.Effects = DragDropEffects.Move;
                if (targetItem != null && targetSquad != null)
                {
                    var pos = e.GetPosition(targetItem);
                    var above = pos.Y < targetItem.ActualHeight / 2;
                    _dropTargetSquad = targetSquad;
                    _insertAboveSquad = above;
                    UpdateInsertionAdorner(targetItem, above);
                }
                else
                {
                    // If not directly over an item, show below the last squad
                    if (_vm.Squads.Count > 0)
                    {
                        var last = _vm.Squads.Last();
                        var lastItem = NavigationTree.ItemContainerGenerator.ContainerFromItem(last) as TreeViewItem;
                        if (lastItem != null)
                        {
                            _dropTargetSquad = last;
                            _insertAboveSquad = false;
                            UpdateInsertionAdorner(lastItem, false);
                        }
                        else
                        {
                            ClearInsertionAdorner();
                        }
                    }
                    else
                    {
                        ClearInsertionAdorner();
                    }
                }

                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner();
        }
        catch { }
    }

    private void NavigationTree_OnDrop(object sender, DragEventArgs e)
    {
        try
        {
            // Account drop
            if (e.Data.GetDataPresent(typeof(Account)))
            {
                var dragged = (Account)e.Data.GetData(typeof(Account));
                if (dragged == null) { ClearInsertionAdorner(); return; }

                var target = GetAccountOrSquadUnderMouse(e.OriginalSource as DependencyObject);
                if (target.squad == null) { ClearInsertionAdorner(); return; }

                // determine source and target squads in VM
                var sourceSquadVm = _vm.Squads.FirstOrDefault(s => s.Id == dragged.SquadId);
                var targetSquadVm = _vm.Squads.FirstOrDefault(s => s.Id == (target.account?.SquadId ?? target.squad.Id));
                if (sourceSquadVm == null || targetSquadVm == null) { ClearInsertionAdorner(); return; }

                // compute anchor and index within target list
                var targetList = targetSquadVm.Accounts;
                int anchorIndex;
                if (_dropTargetAccount != null)
                {
                    var anchor = targetList.FirstOrDefault(a => a.Id == _dropTargetAccount.Id);
                    anchorIndex = anchor != null ? targetList.IndexOf(anchor) : targetList.Count - 1;
                }
                else if (target.account != null)
                {
                    anchorIndex = targetList.IndexOf(targetList.First(a => a.Id == target.account.Id));
                }
                else
                {
                    anchorIndex = targetList.Count - 1; // end
                }

                int newIndex = _insertAbove ? anchorIndex : anchorIndex + 1;
                if (newIndex < 0) newIndex = 0;
                if (newIndex > targetList.Count) newIndex = targetList.Count; // allow insert at end

                // Remove from source and insert into target
                var sourceList = sourceSquadVm.Accounts;
                var sourceIdx = sourceList.IndexOf(sourceList.First(a => a.Id == dragged.Id));
                if (sourceIdx < 0) { ClearInsertionAdorner(); return; }

                // If moving within the same list, adjust for removal
                if (ReferenceEquals(sourceList, targetList) && sourceIdx < newIndex) newIndex--;
                sourceList.RemoveAt(sourceIdx);
                if (newIndex < 0) newIndex = 0;
                if (newIndex > targetList.Count) newIndex = targetList.Count;
                targetList.Insert(newIndex, dragged);

                // Update model to new squad if changed
                var movedAcrossSquads = sourceSquadVm.Id != targetSquadVm.Id;
                if (movedAcrossSquads)
                {
                    dragged.SquadId = targetSquadVm.Id;
                }

                // Persist ordering for both squads, including SquadId change if any
                PersistAccountMoveAsync(dragged, sourceSquadVm, targetSquadVm).ConfigureAwait(false);

                // Select moved account in UI
                try
                {
                    if (NavigationTree.ItemContainerGenerator.ContainerFromItem(targetSquadVm) is TreeViewItem tvi)
                    {
                        tvi.IsExpanded = true;
                        // Force generator to realize containers
                        tvi.UpdateLayout();
                        var accItem = tvi.ItemContainerGenerator.ContainerFromItem(dragged) as TreeViewItem;
                        if (accItem != null)
                        {
                            accItem.IsSelected = true;
                            accItem.BringIntoView();
                        }
                    }
                }
                catch { }
                return;
            }

            // Squad drop
            if (e.Data.GetDataPresent(typeof(Squad)))
            {
                var draggedSquad = (Squad)e.Data.GetData(typeof(Squad));
                if (draggedSquad == null) { ClearInsertionAdorner(); return; }

                var list = _vm.Squads;
                var oldIndex = list.IndexOf(list.First(s => s.Id == draggedSquad.Id));
                if (oldIndex < 0) { ClearInsertionAdorner(); return; }

                int anchorIndex = -1;
                if (_dropTargetSquad != null)
                {
                    var anchor = list.FirstOrDefault(s => s.Id == _dropTargetSquad.Id);
                    anchorIndex = anchor != null ? list.IndexOf(anchor) : list.Count - 1;
                }
                else
                {
                    // try find item under mouse
                    var dep = e.OriginalSource as DependencyObject;
                    while (dep != null && dep is not TreeViewItem) dep = VisualTreeHelper.GetParent(dep);
                    if (dep is TreeViewItem tvi && tvi.DataContext is Squad sq)
                    {
                        anchorIndex = list.IndexOf(list.First(s => s.Id == sq.Id));
                    }
                    else
                    {
                        anchorIndex = list.Count - 1;
                    }
                }

                int newIndex = _insertAboveSquad ? anchorIndex : anchorIndex + 1;
                if (oldIndex < newIndex) newIndex--; // adjust for removal
                newIndex = Math.Max(0, Math.Min(list.Count - 1, newIndex));

                if (newIndex != oldIndex)
                    list.Move(oldIndex, newIndex);

                PersistAllSquadsOrderAsync().ConfigureAwait(false);
                return;
            }
        }
        catch { }
        finally
        {
            ClearInsertionAdorner();
        }
    }

    private (Squad squad, Account account) GetAccountOrSquadUnderMouse(DependencyObject element)
    {
        try
        {
            var dep = element;
            while (dep != null && dep is not TreeViewItem)
                dep = VisualTreeHelper.GetParent(dep);
            if (dep is TreeViewItem tvi)
            {
                if (tvi.DataContext is Account acc)
                {
                    // find its squad
                    var squad = _vm.Squads.FirstOrDefault(s => s.Id == acc.SquadId);
                    return (squad, acc);
                }
                if (tvi.DataContext is Squad sq)
                {
                    return (sq, null);
                }
            }
        }
        catch { }
        return (null, null);
    }

    private void UpdateInsertionAdorner(TreeViewItem item, bool above)
    {
        try
        {
            if (item == null) { ClearInsertionAdorner(); return; }
            if (!ReferenceEquals(_adornerItem, item) || _insertionAdorner == null)
            {
                ClearInsertionAdorner();
                _adornerItem = item;
                var layer = AdornerLayer.GetAdornerLayer(item);
                if (layer != null)
                {
                    _insertionAdorner = new InsertionAdorner(item) { PositionAbove = above };
                    layer.Add(_insertionAdorner);
                }
            }
            else
            {
                _insertionAdorner.PositionAbove = above;
                _insertionAdorner.InvalidateVisual();
            }
        }
        catch { }
    }

    private void ClearInsertionAdorner()
    {
        try
        {
            if (_adornerItem != null && _insertionAdorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(_adornerItem);
                if (layer != null)
                {
                    layer.Remove(_insertionAdorner);
                }
            }
        }
        catch { }
        finally
        {
            _insertionAdorner = null;
            _adornerItem = null;
            _dropTargetAccount = null;
            _dropTargetSquad = null;
            _insertAboveSquad = false;
        }
    }

    private void NavigationTree_OnDragLeave(object sender, DragEventArgs e)
    {
        ClearInsertionAdorner();
    }

    private async Task PersistSquadOrderAsync(Squad squad)
    {
        try
        {
            await using var db = new AppDbContext();
            // Load accounts for squad and map new order by current VM order
            var orderMap = squad.Accounts.Select((a, idx) => new { a.Id, Index = idx }).ToDictionary(x => x.Id, x => x.Index);
            var accs = db.Accounts.Where(a => a.SquadId == squad.Id).ToList();
            foreach (var a in accs)
            {
                if (orderMap.TryGetValue(a.Id, out var idx)) a.OrderIndex = idx;
            }
            await db.SaveChangesAsync();
        }
        catch
        {
            // On error, reload from DB to keep consistency
            try { _vm.Reload(); } catch { }
        }
    }

    private async Task PersistAllSquadsOrderAsync()
    {
        try
        {
            await using var db = new AppDbContext();
            // Map current order by VM
            var orderMap = _vm.Squads.Select((s, idx) => new { s.Id, Index = idx })
                                     .ToDictionary(x => x.Id, x => x.Index);
            var squads = db.Squads.ToList();
            foreach (var s in squads)
            {
                if (orderMap.TryGetValue(s.Id, out var idx)) s.OrderIndex = idx;
            }
            await db.SaveChangesAsync();
        }
        catch
        {
            try { _vm.Reload(); } catch { }
        }
    }

    private async Task PersistAccountMoveAsync(Account moved, Squad source, Squad target)
    {
        try
        {
            await using var db = new AppDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            // Update SquadId if changed
            var acc = await db.Accounts.FirstOrDefaultAsync(a => a.Id == moved.Id);
            if (acc != null)
            {
                acc.SquadId = target.Id;
            }

            if (source.Id == target.Id)
            {
                // Reindex one squad (same list)
                var map = target.Accounts.Select((a, idx) => new { a.Id, idx })
                                         .ToDictionary(x => x.Id, x => x.idx);
                var dbAccs = db.Accounts.Where(a => a.SquadId == target.Id).ToList();
                foreach (var a in dbAccs)
                {
                    if (map.TryGetValue(a.Id, out var idx)) a.OrderIndex = idx;
                }
            }
            else
            {
                // Reindex source squad
                var sourceMap = source.Accounts.Select((a, idx) => new { a.Id, idx })
                                              .ToDictionary(x => x.Id, x => x.idx);
                var sourceDbAccs = db.Accounts.Where(a => a.SquadId == source.Id).ToList();
                foreach (var a in sourceDbAccs)
                {
                    if (sourceMap.TryGetValue(a.Id, out var idx)) a.OrderIndex = idx;
                }

                // Reindex target squad (includes moved account at new position)
                var targetMap = target.Accounts.Select((a, idx) => new { a.Id, idx })
                                              .ToDictionary(x => x.Id, x => x.idx);
                var targetDbAccs = db.Accounts.Where(a => a.SquadId == target.Id).ToList();
                foreach (var a in targetDbAccs)
                {
                    if (targetMap.TryGetValue(a.Id, out var idx)) a.OrderIndex = idx;
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            try { _vm.Reload(); } catch { }
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

    private void SelectedItemChanged(TreeViewItem tvi)
    {
        ControlsList_SelectedItemChanged();
        if (tvi != null)
        {
            tvi.IsExpanded = !tvi.IsExpanded;
        }
    }

    private async void ControlsList_SelectedItemChanged()
    {
        // Block account switching from TreeView while AccountManager is in progress
        try { if (AccountPage?.AccountManager?.IsChanging == true) return; } catch { }

        if (NavigationTree.SelectedItem is Account account)
        {
            if (AccountPage.AccountManager.CurrentAccount?.Id != account.Id)
            {
                // Если уже открыта другая страница аккаунта, обновляем её контекст
                await AccountPage.AccountManager.ChangeAccountAsync(account.Id);
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
                        await AccountPage.AccountManager.ChangeAccountAsync(null);
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
                        await AccountPage.AccountManager.ChangeAccountAsync(null);
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
    
    public void LoadModules()
    {
        try
        {
            _modules = _moduleService.LoadModules();
            ModulesList.ItemsSource = _modules;
            RunModuleButton.IsEnabled = _modules.Count > 0;
        }
        catch
        {
        }
    }

    private void OnRunModuleClick(object sender, RoutedEventArgs e)
    {
        if (ModulesList.SelectedItem is not ModuleDefinition module)
        {
            MessageBox.Show("Выберите модуль из списка.");
            return;
        }

        var argsWindow = new Windows.ModuleArgsWindow(module) { Owner = this };
        if (argsWindow.ShowDialog() != true)
            return;

        // Persist last used arguments for this module
        try
        {
            module.LastArgs = argsWindow.StringValues;
            _moduleService.AddOrUpdateModule(module);
        }
        catch { }

        var runner = new LuaScriptRunner(AccountPage.AccountManager, AccountPage.Browser);
        var logWindow = new Windows.ScriptLogWindow(module.Name) { Owner = this };

        // Wire sinks
        runner.SetPrintSink(text => logWindow.AppendLog(text));
        runner.SetProgressSink((percent, message) => logWindow.ReportProgress(percent, message));

        // Wire stop action
        logWindow.SetStopAction(() =>
        {
            try
            {
                runner.Stop();
                logWindow.AppendLog("[Остановлено пользователем]");
            }
            catch
            {
            }
        });

        logWindow.SetRunning(true);

        // Fire-and-forget API run increment so the library reflects usage
        _ = IncrementRunIfApiModuleAsync(module);

        string result = null;
        // Start execution and update UI on completion
        var task = runner.RunModuleAsync(module, argsWindow.Values).ContinueWith(t =>
        {
            try
            {
                result = t.IsCompletedSuccessfully ? t.Result : null;
                logWindow.MarkCompleted(result);
                var title = $"Модуль: {module.Name}";
                var text = string.IsNullOrWhiteSpace(result) ? "Выполнено" : result;
                if (Application.Current is App app)
                {
                    app.NotifyIcon.ShowBalloonTip(5, title, text, System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            catch
            {
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        // Show modal log window while running; Close becomes available when finished
        logWindow.ShowDialog();
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var lua = new Lua();
            lua.State.Encoding = Encoding.UTF8;

            var tcs = new TaskCompletionSource<string>();
            var luaIntegration = new LuaIntegration(AccountPage.AccountManager, tcs);

            // Register overload that accepts NLua.LuaFunction
            var mi = typeof(LuaIntegration).GetMethod("GetAccountAsyncCallback", new[] { typeof(LuaFunction) });
            if (mi == null)
            {
                MessageBox.Show("Не удалось найти метод интеграции Lua.");
                return;
            }

            lua.RegisterFunction("GetAccountAsyncCallback", luaIntegration, mi);

            // Start script; it will call our C# function with a Lua callback.
            lua.DoString(@"
                GetAccountAsyncCallback(function(accountName)
                    return 'Hello from Lua, ' .. accountName .. '!'
                end)
            ");

            // Wait until C# receives the result from Lua callback
            var text = await tcs.Task.ConfigureAwait(true);
            MessageBox.Show(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка Lua: {ex.Message}");
        }
    }

    private void OpenModulesLibrary_Click(object sender, RoutedEventArgs e)
    {
        var win = new Windows.ModulesLibraryWindow() { Owner = this };
        win.ShowDialog();
    }

    private async Task IncrementRunIfApiModuleAsync(ModuleDefinition module)
    {
        try
        {
            if (Guid.TryParse(module.Id, out var id))
            {
                var client = new ModulesApiClient();
                await client.IncrementRunAsync(id);
            }
        }
        catch
        {
        }
    }

    private class JsOption
    {
        public string value { get; set; } = string.Empty;
        public string text { get; set; } = string.Empty;
    }

    private void OnLoadCharactersClick(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not Account selectedAccount)
        {
            MessageBox.Show("Выберите аккаунт", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var log = new Windows.ScriptLogWindow("Загрузка персонажей", true) { Owner = this };

        // Запускаем асинхронный процесс загрузки до показа модального окна
        async void StartLoad()
        {
            try
            {
                log.AppendLog($"Переключаемся на аккаунт: {selectedAccount.Name}");
                await AccountPage.AccountManager.ChangeAccountAsync(selectedAccount.Id);

                log.AppendLog("Переходим на страницу промо-предметов...");
                await AccountPage.Browser.NavigateAsync("https://pwonline.ru/promo_items.php");

                var hasShard = await AccountPage.Browser.WaitForElementExistsAsync(".js-shard", 1000);
                if (!hasShard)
                {
                    log.AppendLog("Не найден элемент .js-шard — список серверов.");
                    log.MarkCompleted("Ошибка: не удалось получить список серверов");
                    return;
                }

                log.AppendLog("Читаем список серверов...");
                var shardsJson = await AccountPage.Browser.ExecuteScriptAsync(
                    "(function(){ var s=document.querySelector('.js-shard'); if(!s) return []; return Array.from(s.options).filter(o=>o.value).map(o=>({ value:o.value, text:o.textContent.trim() })); })()"
                );
                var shards =
                    JsonSerializer.Deserialize<List<JsOption>>(shardsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<JsOption>();

                var servers = new List<AccountServer>();
                for (var index = 0; index < shards.Count; index++)
                {
                    var shard = shards[index];
                    log.AppendLog($"[{index + 1}/{shards.Count}] Сервер: {shard.text}");

                    // Выбираем сервер
                    var valEsc = (shard.value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
                    await AccountPage.Browser.ExecuteScriptAsync(
                        $"(function(){{ var s=document.querySelector('.js-shard'); if(!s) return false; s.value='{valEsc}'; s.dispatchEvent(new Event('change', {{ bubbles:true }})); return true; }})()"
                    );

                    // Ждем появления/обновления списка персонажей
                    await AccountPage.Browser.WaitForElementExistsAsync(".js-char", 500);

                    var charsJson = await AccountPage.Browser.ExecuteScriptAsync(
                        "(function(){ var c=document.querySelector('.js-char'); if(!c) return []; return Array.from(c.options).filter(o=>o.value).map(o=>({ value:o.value, text:o.textContent.trim() })); })()"
                    );
                    var chars = JsonSerializer.Deserialize<List<JsOption>>(charsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<JsOption>();

                    var server = new AccountServer
                    {
                        OptionId = shard.value ?? string.Empty,
                        Name = shard.text ?? string.Empty,
                        Characters = [],
                        AccountId = selectedAccount.Id
                    };
                    server.Characters = chars.Select(ch => new AccountCharacter
                        {
                            OptionId = ch.value ?? string.Empty, Name = ch.text ?? string.Empty, ServerId = server.Id
                        })
                        .ToList();
                    servers.Add(server);
                    log.AppendLog($"   Персонажей: {server.Characters.Count}");
                }

                await using (var db = new AppDbContext())
                {
                    var accounts = _vm.Squads.SelectMany(x => x.Accounts).ToList();
                    var acc = accounts.FirstOrDefault(x => x.Id == selectedAccount.Id);
                    if (acc != null)
                    {
                        foreach (var server in servers)
                        {
                            var chars = new List<AccountCharacter>();
                            var existing = acc.Servers.FirstOrDefault(s => s.OptionId == server.OptionId);
                            if (existing != null)
                            {
                                var newChars = server.Characters.Where(character =>
                                        existing.Characters.All(c => c.OptionId != character.OptionId))
                                    .ToList();
                                foreach (var newChar in newChars)
                                    newChar.ServerId = existing.Id;
                                chars.AddRange(newChars);
                            }
                            if (existing == null)
                            {
                                await db.AddAsync(server);
                                existing = server;
                                chars.AddRange(server.Characters);
                            }
                            
                            if (existing.Characters.Count == 1)
                                existing.DefaultCharacterOptionId = existing.Characters.First().OptionId;
                            await db.AddRangeAsync(chars);
                        }

                        db.Update(acc);
                        await db.SaveChangesAsync();
                    }
                }
                // _vm.Reload();

                log.MarkCompleted("Готово: данные о серверах и персонажах сохранены.");
            }
            catch (Exception ex)
            {
                log.AppendLog("Ошибка: " + ex.Message);
                log.MarkCompleted("Завершено с ошибкой");
            }
        }

        StartLoad();
        // Показываем окно как модальное, пока идёт загрузка (кнопка Закрыть станет доступной по завершении)
        log.ShowDialog();
    }
}