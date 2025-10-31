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
    private readonly MainViewModel _vm;

    // Drag & drop state for reordering accounts
    private Point _dragStartPoint;
    private Account _draggedAccount;
    private Squad _draggedSquad;

    // Drag visual feedback
    private TreeViewItem _adornerItem;
    private InsertionAdorner _insertionAdorner;

    private readonly ModuleService _moduleService = new();
    private List<ModuleDefinition> _modules = new();

    private readonly ModulesApiClient _modulesApi = new();
    private readonly IModulesSyncService _modulesSync = App.Services.GetService(typeof(IModulesSyncService)) as IModulesSyncService ?? new ModulesSyncService();
    private readonly ILuaExecutionService _luaExec = App.Services.GetService(typeof(ILuaExecutionService)) as ILuaExecutionService ?? new LuaExecutionService();
    private readonly ICharactersLoadService _charsLoad = App.Services.GetService(typeof(ICharactersLoadService)) as ICharactersLoadService ?? new CharactersLoadService();
    private readonly IOrderingService _ordering = App.Services.GetService(typeof(IOrderingService)) as IOrderingService ?? new OrderingService();

    /// <summary>
    /// Конструктор главного окна. Подключает ViewModel из DI, настраивает подписки на события
    /// инициализации и изменения размеров/позиции окна, а также загрузку и синхронизацию модулей.
    /// Вся бизнес-логика вынесена во ViewModel/сервисы; здесь остаётся только UI-специфика.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        
        _vm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel ?? new MainViewModel();
        DataContext = _vm;
        
        // Запуск модуля теперь выполняется напрямую через координатор из VM (без событий)
        // Подписываемся на запрос загрузки персонажей из VM
        try { _vm.RequestLoadCharacters += OnRequestLoadCharacters; } catch { }
        
        // Reposition updates popup when window size or position changes
        try
        {
            SizeChanged += (_, _) => { try { if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup(); } catch { } };
            LocationChanged += (_, _) => { try { if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup(); } catch { } };
            StateChanged += (_, _) => { try { if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup(); } catch { } };
        }
        catch { }
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
        Loaded += async (_, _) =>
        {
            try { LoadModules(); } catch { }
            try { await SyncInstalledModulesAsync(); } catch { }
        };
    }

    /// <summary>
    /// Синхронизирует список установленных локально модулей с серверным профилем пользователя.
    /// Устанавливает недостающие и удаляет лишние модули локально. Ошибки не прерывают работу UI.
    /// </summary>
    private async Task SyncInstalledModulesAsync()
    {
        try
        {
            var changed = await _modulesSync.SyncInstalledAsync();
            if (changed)
            {
                try { LoadModules(); } catch { }
            }
        }
        catch { }
    }

    /// <summary>
        /// Устанавливает или обновляет модуль локально по данным, полученным с сервера (Modules API).
        /// Сохраняет файл скрипта и обновляет запись в локальном каталоге модулей.
        /// </summary>
        private void InstallModuleLocally(Pw.Hub.Services.ModuleDto module)
    {
        try { _modulesSync.InstallModuleLocally(module); } catch { }
    }

    /// <summary>
    /// Удаляет модуль локально по идентификатору: удаляет файл скрипта (если существует)
    /// и запись в локальной коллекции модулей.
    /// </summary>
    private void RemoveModuleLocally(Guid id)
    {
        try { _modulesSync.RemoveModuleLocally(id); } catch { }
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

    /// <summary>
    /// Синхронизирует выделение в TreeView с текущим аккаунтом, выбранным в AccountManager.
    /// Выполняется на UI-потоке, безопасно обрабатывает отсутствие контейнеров.
    /// </summary>
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

    /// <summary>
    /// Отражает изменения данных текущего аккаунта (аватар, имя, SiteId) в соответствующих VM-объектах,
    /// чтобы UI обновил визуальное представление без полного перезагрузки данных.
    /// </summary>
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

    /// <summary>
    /// Блокирует взаимодействие с деревом навигации на время смены аккаунта
    /// и показывает статус в строке состояния. Выполняется на UI-потоке.
    /// </summary>
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


    private void OnAddSquadClick(object sender, RoutedEventArgs e)
    {
        // Устаревший обработчик. Логика добавления отряда перенесена в MainViewModel (AddSquadCommand).
        // Метод оставлен пустым для совместимости автосгенерированного XAML в obj.
        return;
    }

    private void OnAddAccountClick(object sender, RoutedEventArgs e)
    {
        // Устаревший обработчик. Логика добавления аккаунта перенесена в MainViewModel (AddAccountCommand).
        // Метод оставлен пустым для совместимости автосгенерированного XAML в obj.
        return;
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

                // Persist ordering for both squads через сервис порядка
                var targetAccForService = _dropTargetAccount ?? target.account;
                var insertAfterAccount = !_insertAbove;
                _ = _ordering.ReorderAccountAsync(dragged, targetSquadVm, targetAccForService, insertAfterAccount);

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

                // Persist order via ordering service
                if (_dropTargetSquad != null)
                {
                    var insertAfter = !_insertAboveSquad;
                    _ = _ordering.ReorderSquadAsync(draggedSquad, _dropTargetSquad, insertAfter);
                }
                else if (_vm.Squads.Count > 0)
                {
                    var anchor = _vm.Squads[Math.Min(newIndex, _vm.Squads.Count - 1)];
                    var insertAfter = newIndex > _vm.Squads.IndexOf(anchor);
                    _ = _ordering.ReorderSquadAsync(draggedSquad, anchor, insertAfter);
                }
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

    // Удалено: устаревшая логика сохранения порядка. Теперь используется IOrderingService.
    // private async Task PersistAllSquadsOrderAsync() { }
    // private async Task PersistAccountMoveAsync(Account moved, Squad source, Squad target) { }

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
        // Устаревший обработчик. Логика редактирования отряда перенесена в MainViewModel (EditSquadCommand).
        // Метод оставлен пустым для совместимости автосгенерированного XAML в obj.
        return;
    }

    private void OnEditAccountClick(object sender, RoutedEventArgs e)
    {
        // Устаревший обработчик. Логика редактирования аккаунта перенесена в MainViewModel (EditAccountCommand).
        // Метод оставлен пустым для совместимости автосгенерированного XAML в obj.
        return;
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        // Устаревший обработчик. Логика удаления перенесена в MainViewModel (DeleteNodeCommand).
        // Метод оставлен пустым для совместимости автосгенерированного XAML в obj.
        await Task.CompletedTask;
        return;
    }

    /// <summary>
    /// При закрытии окна скрывает его в трей вместо завершения приложения.
    /// Также закрывает попап обновлений, если он открыт.
    /// </summary>
    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        if (_vm.IsUpdatesPopupOpen)
            _vm.IsUpdatesPopupOpen = false;
        Hide();
    }

    /// <summary>
    /// Обработчик активации окна: дебаунс-проверка наличия обновлений модулей и показ попапа при необходимости.
    /// </summary>
    private async void MainWindow_OnActivated(object sender, EventArgs e)
    {
        try
        {
            await _vm.CheckUpdatesDebouncedAsync();
            if (_vm.IsUpdatesPopupOpen)
            {
                try { PositionUpdatesPopup(); } catch { }
            }
        }
        catch
        {
            // игнорируем ошибки UI/позиционирования
        }
    }

    /// <summary>
    /// Обработчик кнопки закрытия попапа обновлений.
    /// Делегирует закрытие во ViewModel через биндинг свойства IsUpdatesPopupOpen.
    /// </summary>
    private void UpdatesPopup_CloseClick(object sender, RoutedEventArgs e)
    {
        try { _vm.IsUpdatesPopupOpen = false; } catch { }
    }

    // Close updates popup when window loses activation (user switches to other app)
    /// <summary>
    /// Закрывает попап обновлений при потере фокуса главным окном (переключение на другое приложение).
    /// </summary>
    private void MainWindow_OnDeactivated(object sender, EventArgs e)
    {
        try { if (_vm.IsUpdatesPopupOpen) _vm.IsUpdatesPopupOpen = false; } catch { }
    }

    // Close updates popup when window is minimized; otherwise keep it positioned
    /// <summary>
    /// При смене состояния окна (сворачивание/разворачивание) скрывает попап при свернутом окне
    /// и перепозиционирует его при возврате в нормальное состояние.
    /// </summary>
    private void MainWindow_OnStateChanged(object sender, EventArgs e)
    {
        try
        {
            if (WindowState == WindowState.Minimized)
            {
                if (_vm.IsUpdatesPopupOpen) _vm.IsUpdatesPopupOpen = false;
            }
            else
            {
                if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup();
            }
        }
        catch { }
    }

    /// <summary>
    /// Вычисляет и устанавливает позицию попапа обновлений в правом нижнем углу окна.
    /// Учитывает DPI (переводит пиксели устройства в DIPs) и состояние окна.
    /// </summary>
    private void PositionUpdatesPopup()
    {
        try
        {
            if (RootGrid == null || UpdatesPopup == null)
                return;

            // Desired margins from right/bottom edges
            const double rightMargin = 30;
            const double bottomMargin = 40;

            // Ensure absolute placement so popup tracks window movement precisely
            try
            {
                UpdatesPopup.Placement = PlacementMode.AbsolutePoint;
                // Break any PlacementTarget binding to avoid relative positioning glitches
                UpdatesPopup.PlacementTarget = null;
            }
            catch { }

            // Determine popup size (fallback when not yet measured)
            var child = UpdatesPopup.Child as FrameworkElement;
            double popupWidth = child?.ActualWidth > 0 ? child.ActualWidth : 320;
            double popupHeight = child?.ActualHeight > 0 ? child.ActualHeight : 140;

            // Compute bottom-right point inside RootGrid in screen coordinates (pixels)
            var gridWidth = RootGrid.ActualWidth;
            var gridHeight = RootGrid.ActualHeight;
            if (gridWidth <= 0 || gridHeight <= 0)
                return;

            var bottomRight = new Point(Math.Max(0, gridWidth - popupWidth - rightMargin),
                                        Math.Max(0, gridHeight - popupHeight - bottomMargin));

            // Convert from element coordinates to screen pixels
            var screenPoint = RootGrid.PointToScreen(bottomRight);

            // Convert device pixels to WPF DIPs
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var transform = source.CompositionTarget.TransformFromDevice;
                var dipPoint = transform.Transform(new Point(screenPoint.X, screenPoint.Y));
                UpdatesPopup.HorizontalOffset = dipPoint.X;
                UpdatesPopup.VerticalOffset = dipPoint.Y;
            }
            else
            {
                // Fallback: assume 1:1
                UpdatesPopup.HorizontalOffset = screenPoint.X;
                UpdatesPopup.VerticalOffset = screenPoint.Y;
            }
        }
        catch { }
    }

    private static string NormalizeSemVer(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "0.0.0";
        var core = v.Split('+')[0];
        core = core.Split('-')[0];
        return core.Trim();
    }

    private static bool TryParseVersion(string v, out Version ver)
    {
        return Version.TryParse(NormalizeSemVer(v), out ver);
    }

    private static bool IsUpdateAvailable(string local, string server)
    {
        if (TryParseVersion(local, out var lv) && TryParseVersion(server, out var sv))
        {
            return sv > lv;
        }
        // fallback string compare
        return !string.Equals(local, server, StringComparison.Ordinal);
    }
    
    public void LoadModules()
    {
        try
        {
            _modules = _moduleService.LoadModules();
            // Синхронизируем коллекцию VM для блока быстрого запуска (биндинг ItemsSource в XAML)
            try { _vm.ReloadQuickModules(); } catch { }
            // Кнопка будет управляться CanExecute команды, но оставим явную блокировку как fallback
            RunModuleButton.IsEnabled = _modules?.Count > 0;
        }
        catch
        {
        }
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
        try
        {
            var win = new Windows.ModulesLibraryWindow() { Owner = this };
            // Show modeless so it doesn't block MainWindow/UI
            win.Show();
            // Hide popup if it was open
            try { if (_vm.IsUpdatesPopupOpen) _vm.IsUpdatesPopupOpen = false; } catch { }
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

    private async void OnLoadCharactersClick(object sender, RoutedEventArgs e)
    {
        // Устаревший обработчик. Логика перенесена в MainViewModel (LoadCharactersCommand).
        // Метод оставлен пустым для совместимости автосгенерированного XAML в obj.
        await Task.CompletedTask;
        return;
    }

    /// <summary>
    /// Обработчик VM-запроса на загрузку персонажей: делегирует выполнение сервису загрузки персонажей.
    /// </summary>
    private async void OnRequestLoadCharacters(Account account)
    {
        if (account == null) return;
        try { await _charsLoad.LoadForAccountAsync(account, AccountPage, this); } catch { }
    }
}