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