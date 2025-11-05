using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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
    // Менеджер динамических браузеров v2
    public Services.BrowserManager BrowserManager { get; private set; }

    // Добавление BrowserView в рабочее пространство
    public void AddBrowserView(Controls.BrowserView view)
    {
        try
        {
            if (view == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AddBrowserView(view));
                return;
            }
            BrowserWorkspace.Visibility = Visibility.Visible;
            BrowserWorkspace.Children.Add(view);
            
            // Когда есть хотя бы один динамический браузер (v2), прячем основной AccountPage,
            // чтобы избежать наложения двух HWND (WebView2) и артефактов отрисовки (airspace).
            try
            {
                if (AccountPage != null)
                {
                    AccountPage.Visibility = Visibility.Collapsed;
                    AccountPage.IsHitTestVisible = false;
                }
            }
            catch { }

            UpdateBrowserWorkspaceGrid();
        }
        catch { }
    }

    // Удаление BrowserView из рабочего пространства
    public void RemoveBrowserView(Controls.BrowserView view)
    {
        try
        {
            if (view == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => RemoveBrowserView(view));
                return;
            }
            BrowserWorkspace.Children.Remove(view);
            if (BrowserWorkspace.Children.Count == 0)
            {
                BrowserWorkspace.Visibility = Visibility.Collapsed;
                // Возвращаем основной браузер AccountPage на экран
                try
                {
                    if (AccountPage != null)
                    {
                        AccountPage.Visibility = Visibility.Visible;
                        AccountPage.IsHitTestVisible = true;
                    }
                }
                catch { }
            }
            UpdateBrowserWorkspaceGrid();
        }
        catch { }
    }

    // Пересчёт строк/колонок для равных размеров ячеек
    public void UpdateBrowserWorkspaceGrid()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateBrowserWorkspaceGrid);
                return;
            }
            int n = BrowserWorkspace.Children.Count;
            if (n <= 0)
            {
                BrowserWorkspace.Rows = 1;
                BrowserWorkspace.Columns = 1;
                return;
            }
            int cols = (int)Math.Ceiling(Math.Sqrt(n));
            int rows = (int)Math.Ceiling((double)n / cols);
            BrowserWorkspace.Columns = cols;
            BrowserWorkspace.Rows = rows;
        }
        catch { }
    }
    private readonly MainViewModel _vm;


    private readonly ModuleService _moduleService = new();
    private List<ModuleDefinition> _modules = new();

    private readonly ModulesApiClient _modulesApi = new();

    private readonly IModulesSyncService _modulesSync =
        App.Services.GetService(typeof(IModulesSyncService)) as IModulesSyncService ?? new ModulesSyncService();

    private readonly ILuaExecutionService _luaExec =
        App.Services.GetService(typeof(ILuaExecutionService)) as ILuaExecutionService ?? new LuaExecutionService();

    private readonly ICharactersLoadService _charsLoad =
        App.Services.GetService(typeof(ICharactersLoadService)) as ICharactersLoadService ??
        new CharactersLoadService();

    private readonly IOrderingService _ordering =
        App.Services.GetService(typeof(IOrderingService)) as IOrderingService ?? new OrderingService();

    /// <summary>
    /// Конструктор главного окна. Подключает ViewModel из DI, настраивает подписки на события
    /// инициализации и изменения размеров/позиции окна, а также загрузку и синхронизацию модулей.
    /// Вся бизнес-логика вынесена во ViewModel/сервисы; здесь остаётся только UI-специфика.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // Инициализируем менеджер браузеров v2
        BrowserManager = new BrowserManager(this);

        _vm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel ?? new MainViewModel();
        DataContext = _vm;

        // Подписываемся на запросы из VM
        try
        {
            _vm.RequestLoadCharacters += OnRequestLoadCharacters;
        }
        catch
        {
        }

        try
        {
            _vm.RequestSelectTreeItem += OnRequestSelectTreeItem;
        }
        catch
        {
        }

        // Reposition updates popup when window size or position changes
        try
        {
            SizeChanged += (_, _) =>
            {
                try
                {
                    if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup();
                }
                catch
                {
                }
            };
            LocationChanged += (_, _) =>
            {
                try
                {
                    if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup();
                }
                catch
                {
                }
            };
            StateChanged += (_, _) =>
            {
                try
                {
                    if (_vm.IsUpdatesPopupOpen) PositionUpdatesPopup();
                }
                catch
                {
                }
            };
        }
        catch
        {
        }

        try
        {
            // Subscribe to account change events to sync TreeView selection
            AccountPage.AccountManager.CurrentAccountChanged += OnCurrentAccountChanged;
            // Subscribe to current account data/property changes to mirror updates (e.g., avatar) into VM
            AccountPage.AccountManager.CurrentAccountDataChanged += OnCurrentAccountDataChanged;
            // Subscribe to account changing progress to block UI interactions during switch
            AccountPage.AccountManager.CurrentAccountChanging += OnCurrentAccountChanging;
        }
        catch
        {
        }

        Loaded += async (_, _) =>
        {
            try
            {
                LoadModules();
            }
            catch
            {
            }

            try
            {
                await SyncInstalledModulesAsync();
            }
            catch
            {
            }
        };
    }

    private async void OnRequestSelectTreeItem(object item)
    {
        try
        {
            await Dispatcher.InvokeAsync(async () => { await SelectInNavigationTreeAsync(item); },
                System.Windows.Threading.DispatcherPriority.Background);
        }
        catch
        {
        }
    }

    private async Task SelectInNavigationTreeAsync(object item)
    {
        if (item == null) return;

        // If a different account is being changed, postpone selection
        try
        {
            if (AccountPage?.AccountManager?.IsChanging == true) return;
        }
        catch
        {
        }

        if (item is Account acc)
        {
            // Find the corresponding instances from VM collections by Id
            var squad = _vm.Squads.FirstOrDefault(s => s.Id == acc.SquadId);
            if (squad == null) return;
            var targetAccount = squad.Accounts.FirstOrDefault(a => a.Id == acc.Id);
            if (targetAccount == null) return;

            // Ensure squad container exists and is expanded
            var squadContainer = await GetContainerAsync(NavigationTree, squad);
            if (squadContainer == null) return;
            squadContainer.IsExpanded = true;
            await Task.Yield();

            var accContainer = await GetContainerAsync(squadContainer, targetAccount);
            if (accContainer == null) return;
            accContainer.IsSelected = true;
            accContainer.Focus();
            accContainer.BringIntoView();
            return;
        }

        if (item is Squad sq)
        {
            var targetSquad = _vm.Squads.FirstOrDefault(s => s.Id == sq.Id);
            if (targetSquad == null) return;
            var container = await GetContainerAsync(NavigationTree, targetSquad);
            if (container == null) return;
            container.IsExpanded = true;
            container.IsSelected = true;
            container.Focus();
            container.BringIntoView();
        }
    }

    private async Task<TreeViewItem> GetContainerAsync(ItemsControl parent, object item)
    {
        // Try a few times to wait for item containers to be generated
        for (int i = 0; i < 10; i++)
        {
            var gen = (parent as ItemsControl)?.ItemContainerGenerator;
            if (gen != null)
            {
                var container = gen.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                    return container;
                if (gen.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    // One more attempt after generated
                    container = gen.ContainerFromItem(item) as TreeViewItem;
                    if (container != null) return container;
                }
            }

            await Task.Delay(25);
            try
            {
                parent.UpdateLayout();
            }
            catch
            {
            }
        }

        return null;
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
                try
                {
                    LoadModules();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Устанавливает или обновляет модуль локально по данным, полученным с сервера (Modules API).
    /// Сохраняет файл скрипта и обновляет запись в локальном каталоге модулей.
    /// </summary>
    private void InstallModuleLocally(Pw.Hub.Services.ModuleDto module)
    {
        try
        {
            _modulesSync.InstallModuleLocally(module);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Удаляет модуль локально по идентификатору: удаляет файл скрипта (если существует)
    /// и запись в локальной коллекции модулей.
    /// </summary>
    private void RemoveModuleLocally(Guid id)
    {
        try
        {
            _modulesSync.RemoveModuleLocally(id);
        }
        catch
        {
        }
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
                catch
                {
                }
            });
        }
        catch
        {
        }
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

        // Only toggle expand/collapse on click; selection change logic is handled by SelectedItemChanged event handler
        if (sender is TreeView tv)
        {
            var tvi = tv.ItemContainerGenerator.ContainerFromItem(tv.SelectedItem) as TreeViewItem;
            if (tvi != null)
            {
                tvi.IsExpanded = !tvi.IsExpanded;
            }
        }
    }

    private void NavigationTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        try
        {
            // Centralized selection change processing (handles both user and programmatic selection)
            ControlsList_SelectedItemChanged();
        }
        catch
        {
        }
    }

    private void NavigationTree_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // Select the item under the mouse so that context menu commands target it
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not TreeViewItem)
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is TreeViewItem tvi)
            {
                tvi.IsSelected = true;
                tvi.Focus();
            }
        }
        catch
        {
            // ignore any UI exceptions
        }
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
        try
        {
            if (AccountPage?.AccountManager?.IsChanging == true) return;
        }
        catch
        {
        }

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


    /// <summary>
    /// При закрытии окна скрывает его в трей вместо завершения приложения.
    /// Также закрывает попап обновлений, если он открыт.
    /// </summary>
    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        if (_vm.IsUpdatesPopupOpen)
            _vm.IsUpdatesPopupOpen = false;

        // Отписываемся от событий VM, чтобы избежать утечек памяти
        try
        {
            _vm.RequestLoadCharacters -= OnRequestLoadCharacters;
        }
        catch
        {
        }

        try
        {
            _vm.RequestSelectTreeItem -= OnRequestSelectTreeItem;
        }
        catch
        {
        }

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
                try
                {
                    PositionUpdatesPopup();
                }
                catch
                {
                }
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
        try
        {
            _vm.IsUpdatesPopupOpen = false;
        }
        catch
        {
        }
    }

    // Close updates popup when window loses activation (user switches to other app)
    /// <summary>
    /// Закрывает попап обновлений при потере фокуса главным окном (переключение на другое приложение).
    /// </summary>
    private void MainWindow_OnDeactivated(object sender, EventArgs e)
    {
        try
        {
            if (_vm.IsUpdatesPopupOpen) _vm.IsUpdatesPopupOpen = false;
        }
        catch
        {
        }
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
        catch
        {
        }
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
            catch
            {
            }

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
        catch
        {
        }
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
            try
            {
                _vm.ReloadQuickModules();
            }
            catch
            {
            }

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
            try
            {
                if (_vm.IsUpdatesPopupOpen) _vm.IsUpdatesPopupOpen = false;
            }
            catch
            {
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
        try
        {
            // 1) Выполнить загрузку персонажей для аккаунта
            await _charsLoad.LoadForAccountAsync(account, AccountPage, this);

            // 2) Зафиксировать текущий выбор непосредственно перед перезагрузкой данных
            object selected = null;
            try
            {
                selected = NavigationTree?.SelectedItem;
            }
            catch
            {
            }

            // 3) Перезагрузить коллекции отрядов/аккаунтов в VM
            try
            {
                _vm.Reload();
            }
            catch
            {
            }

            // 4) Восстановить выбор, если он был. Не переопределяем выбор, если его нет.
            try
            {
                if (selected is Account selAcc)
                {
                    // Создаём лёгкий дескриптор по Id/SquadId для поиска в обновлённых коллекциях
                    var descriptor = new Account { Id = selAcc.Id, SquadId = selAcc.SquadId };
                    await SelectInNavigationTreeAsync(descriptor);
                }
                else if (selected is Squad selSquad)
                {
                    var descriptor = new Squad { Id = selSquad.Id };
                    await SelectInNavigationTreeAsync(descriptor);
                }
            }
            catch
            {
            }
        }
        catch
        {
        }
    }
}