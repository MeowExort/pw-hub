using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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

    private void OpenAIGenerator_Click(object sender, RoutedEventArgs e)
    {
        var win = new Pw.Hub.Windows.AIGeneratorWindow
        {
            Owner = this
        };
        win.Show();
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
                        NavigationTree.IsEnabled = !isChanging;
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

    private void OnReloadModulesClick(object sender, RoutedEventArgs e)
    {
        LoadModules();
    }

    private void OnCreateModuleClick(object sender, RoutedEventArgs e)
    {
        var editor = new Windows.ModuleEditorWindow();
        editor.Owner = this;
        if (editor.ShowDialog() == true)
        {
            _moduleService.AddOrUpdateModule(editor.Module);
            LoadModules();
            // select the new/updated module
            ModulesList.SelectedItem = _modules.FirstOrDefault(m => m.Id == editor.Module.Id);
        }
    }

    private void OnEditModuleClick(object sender, RoutedEventArgs e)
    {
        if (ModulesList.SelectedItem is not ModuleDefinition selected)
        {
            MessageBox.Show("Выберите модуль для редактирования", "Модули");
            return;
        }

        var editor = new Windows.ModuleEditorWindow(selected) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            _moduleService.AddOrUpdateModule(editor.Module);
            LoadModules();
            ModulesList.SelectedItem = _modules.FirstOrDefault(m => m.Id == editor.Module.Id);
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