using System.Windows;
using System.Windows.Controls;
using Pw.Hub.Services;
using Markdig;
using System.IO;
using Pw.Hub.Models;

namespace Pw.Hub.Windows
{
    public partial class ModulesLibraryWindow : Window
    {
        private readonly ModulesApiClient _api;
        private ModuleDto _selected;
        private readonly string _userId;
        private readonly ModuleService _moduleService = new();

        public ModulesLibraryWindow(string apiBaseUrl = null, string userId = null)
        {
            InitializeComponent();
            _api = new ModulesApiClient(apiBaseUrl);
            _userId = AuthState.CurrentUser.UserId ?? throw new ArgumentNullException(nameof(AuthState.CurrentUser.UserId));
            Loaded += async (_, _) => await InitAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                // Ensure WebView2 is initialized
                if (DescriptionWebView.CoreWebView2 == null)
                {
                    await DescriptionWebView.EnsureCoreWebView2Async();
                }
            }
            catch
            {
                // ignore, we'll try to load anyway
            }

            await UpdateDevPanelAsync();
            await SearchAndBindAsync();
        }

        private async Task UpdateDevPanelAsync()
        {
            try
            {
                if (_api.CurrentUser == null)
                {
                    await _api.MeAsync();
                }

                DevPanel.Visibility = (_api.CurrentUser?.Developer == true) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                DevPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task SearchAndBindAsync()
        {
            try
            {
                var sort = (SortCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var order = (OrderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var resp = await _api.SearchAsync(SearchTextBox.Text, TagsTextBox.Text, sort, order, 1, 50);
                ModulesList.ItemsSource = resp.Items;

                // compute updates availability
                UpdateAllButton.IsEnabled = ComputeAndSetUpdateIndicators(resp.Items) > 0;

                if (resp.Items.Count > 0)
                {
                    ModulesList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка загрузки библиотеки: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            await SearchAndBindAsync();
        }

        private void ModulesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = ModulesList.SelectedItem as ModuleDto;
            if (_selected != null)
            {
                // Determine local version and update availability
                var localVer = GetLocalVersion(_selected.Id);
                var serverVer = _selected.Version ?? "1.0.0";
                var title = string.IsNullOrWhiteSpace(serverVer) ? _selected.Name : _selected.Name + "  v" + serverVer;
                title += $" Автор: {_selected.AuthorUsername}";
                if (!string.IsNullOrWhiteSpace(localVer))
                {
                    var lv = localVer ?? string.Empty;
                    var sv = serverVer ?? string.Empty;
                    if (IsUpdateAvailable(lv, sv))
                    {
                        title += "  (локально v" + lv + " — доступно v" + sv + ")";
                        UpdateButton.IsEnabled = true;
                    }
                    else
                    {
                        title += "  (локально v" + lv + ")";
                        UpdateButton.IsEnabled = false;
                    }
                }
                else
                {
                    UpdateButton.IsEnabled = false;
                }

                TitleText.Text = title;
            }
            else
            {
                TitleText.Text = string.Empty;
                UpdateButton.IsEnabled = false;
            }

            var md = _selected?.Description ?? string.Empty;
            var html = string.IsNullOrWhiteSpace(md) ? "<i>Нет описания</i>" : Markdown.ToHtml(md);
            TrySetHtml(html);
            UpdateActionButtons();
        }

        private int ComputeAndSetUpdateIndicators(IList<ModuleDto> items)
        {
            try
            {
                var locals = _moduleService.LoadModules();
                var count = 0;
                foreach (var m in items)
                {
                    var local = locals.FirstOrDefault(x =>
                        string.Equals(x.Id, m.Id.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (local != null && IsUpdateAvailable(local.Version ?? "1.0.0", m.Version ?? "1.0.0"))
                        count++;
                }

                return count;
            }
            catch
            {
                return 0;
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

        private string GetLocalVersion(Guid id)
        {
            try
            {
                var locals = _moduleService.LoadModules();
                var local = locals.FirstOrDefault(x =>
                    string.Equals(x.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
                return local?.Version;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUpdateAvailable(string local, string server)
        {
            if (TryParseVersion(local, out var lv) && TryParseVersion(server, out var sv))
            {
                return sv > lv;
            }

            // fallback string compare if parsing fails
            return !string.Equals(local, server, StringComparison.Ordinal);
        }

        private async void TrySetHtml(string html)
        {
            try
            {
                if (DescriptionWebView.CoreWebView2 == null)
                    await DescriptionWebView.EnsureCoreWebView2Async();

                var css = @"body{font-family:Segoe UI,Arial,sans-serif;padding:12px;background:#171A21;color:#C7D5E0;}
                    h1,h2,h3,h4,h5,h6{color:#C7D5E0}
                    a{color:#66C0F4}
                    pre{background:#1B2838;padding:8px;border-radius:6px;overflow:auto;border:1px solid #2A475E}
                    code{background:#1B2838;padding:2px 4px;border-radius:4px;border:1px solid #2A475E}
                    blockquote{border-left:3px solid #2A475E;margin:8px 0;padding:4px 12px;color:#B8C6D1}
                    table{border-collapse:collapse}
                    th,td{border:1px solid #2A475E;padding:6px}
                    ul,ol{padding-left:22px}";
                var doc =
                    $"<!DOCTYPE html><html><head><meta charset='utf-8'><style>{css}</style></head><body>{html}</body></html>";
                DescriptionWebView.NavigateToString(doc);
            }
            catch
            {
                // ignore rendering issues
            }
        }

        private void UpdateActionButtons()
        {
            try
            {
                if (_selected == null)
                {
                    InstallButton.Visibility = Visibility.Collapsed;
                    UninstallButton.Visibility = Visibility.Collapsed;
                    UpdateButton.Visibility = Visibility.Collapsed;
                    return;
                }

                var localVer = GetLocalVersion(_selected.Id);
                var installed = !string.IsNullOrWhiteSpace(localVer);
                InstallButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
                UninstallButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;

                var serverVer = _selected.Version ?? "1.0.0";
                var canUpdate = installed && IsUpdateAvailable(localVer!, serverVer);
                UpdateButton.Visibility = canUpdate ? Visibility.Visible : Visibility.Collapsed;
                UpdateButton.IsEnabled = canUpdate;
            }
            catch
            {
            }
        }

        private async void OnUpdateSelectedClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var localVer = GetLocalVersion(_selected.Id);
                if (string.IsNullOrWhiteSpace(localVer) || !IsUpdateAvailable(localVer!, _selected.Version ?? "1.0.0"))
                {
                    MessageBox.Show(this, "Обновление не требуется", "Модули", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                InstallModuleLocally(_selected);
                RefreshOwnerModules();
                await SearchAndBindAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось обновить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnUpdateAllClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var items = ModulesList.Items.Cast<object>().OfType<ModuleDto>().ToList();
                var locals = _moduleService.LoadModules();
                int updated = 0;
                foreach (var m in items)
                {
                    var local = locals.FirstOrDefault(x =>
                        string.Equals(x.Id, m.Id.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (local != null && IsUpdateAvailable(local.Version ?? "1.0.0", m.Version ?? "1.0.0"))
                    {
                        InstallModuleLocally(m);
                        updated++;
                    }
                }

                if (updated > 0)
                {
                    MessageBox.Show(this, $"Обновлено модулей: {updated}", "Обновление модулей", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    RefreshOwnerModules();
                    await SearchAndBindAsync();
                }
                else
                {
                    MessageBox.Show(this, "Нет модулей, требующих обновления", "Обновление модулей",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при обновлении модулей: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnInstallClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var res1 = await _api.UninstallAsync(_selected.Id, _userId);
                var res = await _api.InstallAsync(_selected.Id, _userId);
                if (res != null)
                {
                    // Update counts from API
                    _selected.InstallCount = res.InstallCount;
                    ModulesList.Items.Refresh();

                    // Save locally: write script file and add to modules.json
                    InstallModuleLocally(res);
                    RefreshOwnerModules();
                    UpdateActionButtons();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось установить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnUninstallClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var res = await _api.UninstallAsync(_selected.Id, _userId);
                if (res != null)
                {
                    _selected.InstallCount = res.InstallCount;
                    ModulesList.Items.Refresh();

                    // Remove locally
                    RemoveModuleLocally(_selected.Id);
                    RefreshOwnerModules();
                    UpdateActionButtons();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось удалить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void InstallModuleLocally(ModuleDto module)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var scriptsDir = Path.Combine(baseDir, "Scripts");
                Directory.CreateDirectory(scriptsDir);
                var fileName = module.Id.ToString() + ".lua";
                var scriptPath = Path.Combine(scriptsDir, fileName);
                File.WriteAllText(scriptPath, module.Script ?? string.Empty);

                var def = new ModuleDefinition
                {
                    Id = module.Id.ToString(),
                    Name = module.Name,
                    Version = string.IsNullOrWhiteSpace(module.Version) ? "1.0.0" : module.Version,
                    Description = module.Description ?? string.Empty,
                    Script = fileName,
                    Inputs = module.Inputs?.Select(i => new ModuleInput
                    {
                        Name = i.Name,
                        Label = string.IsNullOrWhiteSpace(i.Label) ? i.Name : i.Label,
                        Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                        Default = string.IsNullOrWhiteSpace(i.Default) ? null : i.Default,
                        Required = i.Required
                    }).ToList() ?? new List<ModuleInput>()
                };

                var svc = new ModuleService();
                svc.AddOrUpdateModule(def);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);

                // ignore local install errors to not break API flow; user will get message if needed elsewhere
            }
        }

        private void RemoveModuleLocally(Guid id)
        {
            try
            {
                var svc = new ModuleService();
                var list = svc.LoadModules();
                var existing = list.FirstOrDefault(m =>
                    string.Equals(m.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // Try delete script file if under Scripts
                    try
                    {
                        var baseDir = AppContext.BaseDirectory;
                        var candidate1 = Path.Combine(baseDir, existing.Script);
                        var candidate2 = Path.Combine(baseDir, "Scripts", existing.Script);
                        if (File.Exists(candidate1)) File.Delete(candidate1);
                        else if (File.Exists(candidate2)) File.Delete(candidate2);
                    }
                    catch
                    {
                    }

                    list.Remove(existing);
                    svc.SaveModules(list);
                }
            }
            catch
            {
                // ignore local removal errors
            }
        }

        private void RefreshOwnerModules()
        {
            try
            {
                if (Owner is Pw.Hub.MainWindow mw)
                {
                    mw.LoadModules();
                }
            }
            catch
            {
            }
        }


        private async void OnDevCreateClick(object sender, RoutedEventArgs e)
        {
            var editor = new ModulesApiEditorWindow();
            editor.Owner = this;
            if (editor.ShowDialog() == true)
            {
                var req = editor.GetRequest();
                var created = await _api.CreateModuleAsync(req);
                if (created != null)
                {
                    await SearchAndBindAsync();
                    ModulesList.SelectedItem = created;
                }
            }
        }

        private async void OnDevEditClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Выберите модуль");
                return;
            }

            if (_api.CurrentUser?.Developer != true || !string.Equals(_selected.OwnerUserId, _api.CurrentUser.UserId,
                    StringComparison.Ordinal))
            {
                MessageBox.Show("Можно редактировать только свои модули");
                return;
            }

            var editor = new ModulesApiEditorWindow(_selected);
            editor.Owner = this;
            if (editor.ShowDialog() == true)
            {
                var req = editor.GetRequest();
                var updated = await _api.UpdateModuleAsync(_selected.Id, req);
                if (updated != null)
                {
                    await SearchAndBindAsync();
                    ModulesList.SelectedItem = updated;
                }
            }
        }

        private async void OnDevDeleteClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            if (_api.CurrentUser?.Developer != true || !string.Equals(_selected.OwnerUserId, _api.CurrentUser.UserId,
                    StringComparison.Ordinal))
            {
                MessageBox.Show("Можно удалять только свои модули");
                return;
            }

            if (MessageBox.Show("Удалить модуль?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
                MessageBoxResult.Yes)
            {
                if (await _api.DeleteModuleAsync(_selected.Id))
                {
                    await SearchAndBindAsync();
                }
            }
        }

        private void OnOpenLuaEditorClick(object sender, RoutedEventArgs e)
        {
            // var runner = new LuaScriptRunner(mainWindow.AccountPage.AccountManager, mainWindow.AccountPage.Browser);
            if (Application.Current.MainWindow is not MainWindow mainWindow) return;
            var win = new LuaEditorWindow(mainWindow.AccountPage.LuaRunner);
            win.Show();
        }
    }
}