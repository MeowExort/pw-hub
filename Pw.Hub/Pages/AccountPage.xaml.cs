using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Windows;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Pw.Hub.Pages;

public partial class AccountPage : IWebViewHost, INotifyPropertyChanged
{
    public IAccountManager AccountManager;
    public IBrowser Browser;
    public LuaScriptRunner LuaRunner;

    // Overlay: show until the first successful account change
    public bool IsCoreInitialized => Wv?.CoreWebView2?.CookieManager != null;

    // Overlay state property (bindable)
    private bool _isNoAccountOverlayVisible = true; // show until first account is selected successfully

    public bool IsNoAccountOverlayVisible
    {
        get => _isNoAccountOverlayVisible;
        set
        {
            if (value != _isNoAccountOverlayVisible)
            {
                _isNoAccountOverlayVisible = value;
                OnPropertyChanged(nameof(IsNoAccountOverlayVisible));
            }
        }
    }

    // Loading indicator state
    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (value != _isLoading)
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
    {
        try
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        catch
        {
        }
    }

    // IWebViewHost implementation
    public WebView2 Current => Wv;
    public Task ReplaceAsync(WebView2 newControl) => ReplaceWebViewControlAsync(newControl);

    public Task PreloadAsync(WebView2 newControl) => PreloadWebViewControlAsync(newControl);
    public Task FinalizeSwapAsync(WebView2 newControl) => FinalizeSwapWebViewControlAsync(newControl);

    public AccountPage()
    {
        InitializeComponent();
        DataContext = this; // for overlay bindings

        Wv.Source = new Uri("https://pwonline.ru");
        Wv.NavigationCompleted += WvOnNavigationCompleted;
        Wv.NavigationStarting += WvOnNavigationStarting;
        // Основной браузер (страница аккаунта) работает в режиме InPrivate, чтобы не сохранять данные между сессиями.
        Browser = new WebCoreBrowser(this, BrowserSessionIsolationMode.InPrivate);
        // Внимание: начиная с этой версии новая сессия создаётся ТОЛЬКО в legacy Lua API (в1).
        // Переключения из панели навигации (UI) больше не пересоздают сессию автоматически.
        AccountManager = new AccountManager(Browser);
        // Anti-detect: before every account switch, recreate a fresh InPrivate session and apply a new fingerprint
        if (AccountManager is Pw.Hub.Services.AccountManager am)
        {
            am.EnsureNewSessionBeforeSwitchAsync = async () =>
            {
                try
                {
                    await Browser.CreateNewSessionAsync(BrowserSessionIsolationMode.InPrivate);
                    var fp = FingerprintGenerator.Generate();
                    await Browser.ApplyAntiDetectAsync(fp);
                }
                catch
                {
                }
            };
        }

        // Инициализация UI панели навигации
        try
        {
            UpdateNavUi();
        }
        catch
        {
        }

        try
        {
            AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
        }
        catch
        {
        }

        // Subscribe only to account changed to hide initial overlay after first successful switch
        try
        {
            AccountManager.CurrentAccountChanged += OnCurrentAccountChanged;
        }
        catch
        {
        }

        LuaRunner = new LuaScriptRunner(AccountManager, Browser);
    }

    private static Dictionary<string, string> _jsCode = new();

    private static string GetJs(string fileName)
    {
        try
        {
            if (_jsCode.TryGetValue(fileName, out var js))
                return js;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Scripts", "promo", fileName);
            if (File.Exists(path))
            {
                _jsCode[fileName] = File.ReadAllText(path);
                return _jsCode[fileName];
            }
        }
        catch
        {
            // ignored
        }

        return "";
    }

    private async Task<string> ExecuteJsModule(string fileName)
    {
        try
        {
            var jsCode = GetJs(fileName);
            return await Browser.ExecuteScriptAsync(jsCode);
        }
        catch
        {
            return null;
        }
    }

    private void OnCurrentAccountChanged(Account account)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => OnCurrentAccountChanged(account));
                return;
            }

            if (account == null)
                return;
            // Hide the initial placeholder after the first successful account change
            IsNoAccountOverlayVisible = false;
            // Unsubscribe to avoid further UI updates for subsequent switches
            try
            {
                AccountManager.CurrentAccountChanged -= OnCurrentAccountChanged;
            }
            catch
            {
            }
        }
        catch
        {
        }
    }

    // Host delegate for WebCoreBrowser: safely replace Wv in the visual tree and rewire handlers
    private async Task ReplaceWebViewControlAsync(WebView2 newWv)
    {
        if (newWv == null) return;

        // Detach handlers from old control
        try
        {
            Wv.NavigationCompleted -= WvOnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationStarting -= WvOnNavigationStarting;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationCompleted -= Wv_OnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged;
        }
        catch
        {
        }

        // Preserve placement
        var parent = Wv.Parent as Grid;
        int row = Grid.GetRow(Wv);
        int column = Grid.GetColumn(Wv);
        int rowSpan = Grid.GetRowSpan(Wv);
        int columnSpan = Grid.GetColumnSpan(Wv);
        int index = parent?.Children.IndexOf(Wv) ?? -1;

        // Remove and dispose old control
        try
        {
            Wv.Dispose();
        }
        catch
        {
        }

        parent?.Children.Remove(Wv);

        // Insert new control
        if (parent != null)
        {
            if (index >= 0)
                parent.Children.Insert(index, newWv);
            else
                parent.Children.Add(newWv);
            Grid.SetRow(newWv, row);
            Grid.SetColumn(newWv, column);
            Grid.SetRowSpan(newWv, rowSpan);
            Grid.SetColumnSpan(newWv, columnSpan);
        }

        // Update field and reattach handlers
        Wv = newWv;
        try
        {
            Wv.NavigationCompleted += WvOnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationStarting += WvOnNavigationStarting;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationCompleted += Wv_OnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted;
        }
        catch
        {
        }

        // Ensure core of the new control if possible (browser will also try)
        try
        {
            await Wv.EnsureCoreWebView2Async();
        }
        catch
        {
        }
    }

    // Preload new WebView2 hidden without removing old one (to avoid white flash)
    private async Task PreloadWebViewControlAsync(WebView2 newWv)
    {
        if (newWv == null) return;
        var parent = Wv.Parent as Grid;
        if (parent == null) return;

        // Put new control into the same cell but keep it hidden until final swap
        int row = Grid.GetRow(Wv);
        int column = Grid.GetColumn(Wv);
        int rowSpan = Grid.GetRowSpan(Wv);
        int columnSpan = Grid.GetColumnSpan(Wv);

        newWv.Visibility = Visibility.Hidden;
        if (!parent.Children.Contains(newWv))
        {
            // Insert directly before old Wv to preserve z-order of overlay elements
            var index = parent.Children.IndexOf(Wv);
            if (index >= 0)
                parent.Children.Insert(index, newWv);
            else
                parent.Children.Add(newWv);
        }

        Grid.SetRow(newWv, row);
        Grid.SetColumn(newWv, column);
        Grid.SetRowSpan(newWv, rowSpan);
        Grid.SetColumnSpan(newWv, columnSpan);

        // Try to initialize core (safe to ignore errors)
        try
        {
            await newWv.EnsureCoreWebView2Async();
        }
        catch
        {
        }
    }

    // Finalize swap: remove old control, show new one, rewire handlers
    private async Task FinalizeSwapWebViewControlAsync(WebView2 newWv)
    {
        if (newWv == null) return;

        // Detach handlers from old control
        try
        {
            Wv.NavigationCompleted -= WvOnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationStarting -= WvOnNavigationStarting;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationCompleted -= Wv_OnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted;
        }
        catch
        {
        }

        var parent = Wv.Parent as Grid;
        try
        {
            Wv.Dispose();
        }
        catch
        {
        }

        try
        {
            parent?.Children.Remove(Wv);
        }
        catch
        {
        }

        // Switch field and attach handlers to the new control
        Wv = newWv;
        try
        {
            Wv.NavigationCompleted += WvOnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationStarting += WvOnNavigationStarting;
        }
        catch
        {
        }

        try
        {
            Wv.NavigationCompleted += Wv_OnNavigationCompleted;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted;
        }
        catch
        {
        }

        // Make it visible
        try
        {
            Wv.Visibility = Visibility.Visible;
        }
        catch
        {
        }

        // Ensure core again just in case
        try
        {
            await Wv.EnsureCoreWebView2Async();
        }
        catch
        {
        }

        // After swap, Core might already be initialized and InitializationCompleted won't fire.
        // Ensure WebMessage pipeline is configured for the active Core instance.
        try
        {
            if (Wv?.CoreWebView2 != null)
            {
                try
                {
                    Wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
                }
                catch
                {
                }

                try
                {
                    Wv.CoreWebView2.WebMessageReceived -= CoreWebView2OnWebMessageReceived;
                }
                catch
                {
                }

                try
                {
                    Wv.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;
                }
                catch
                {
                }

                try
                {
                    Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged;
                }
                catch
                {
                }

                try
                {
                    Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged;
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

    private void WvOnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Enforce domain restrictions first; only show loader for allowed navigations
        bool allowed = false;
        try
        {
            allowed = e.Uri.StartsWith("https://pwonline.ru") || e.Uri.StartsWith("http://pwonline.ru") ||
                      e.Uri.StartsWith("https://pw.mail.ru") || e.Uri.StartsWith("http://pw.mail.ru");
        }
        catch
        {
        }

        if (!allowed)
        {
            try
            {
                IsLoading = false;
            }
            catch
            {
            }

            e.Cancel = true;
            return;
        }

        try
        {
            IsLoading = true;
        }
        catch
        {
        }
    }

    private async void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            IsLoading = false;
            AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
            UpdateNavUi();

            if (Browser.Source.AbsoluteUri.Contains("promo_items.php"))
            {
                // Передаём в страницу последнее значение acc_info из текущего аккаунта для предвыбора персонажа
                var lastAccInfo = AccountManager?.CurrentAccount?.PromoAccInfo ?? string.Empty;
                var jsValue = JsonSerializer.Serialize(lastAccInfo, JsonSerializerOptions.Web);
                await Browser.ExecuteScriptAsync($"window.__pwLastAccInfo = {jsValue};");

                // Базовый каркас модульной системы Promo — должен быть загружен первым
                await ExecuteJsModule("promo_core.js");

                if (!Browser.Source.AbsoluteUri.Contains("do=activate"))
                {
                    // Инициализации модулей
                    await ExecuteJsModule("promo_init.js");
                    
                    // Всплывающее окно "Управление" и режимы отображения для списка предметов
                    await ExecuteJsModule("promo_manage_popup.js");

                    // Список персонажей для перевода для окна "Управление"
                    await ExecuteJsModule("promo_character_select.js");

                    // Стилизация и центрирование кнопки "Передать"
                    await ExecuteJsModule("promo_submit_ui.js");

                    // Асинхронная отправка формы с индикацией и последующим reload
                    await ExecuteJsModule("promo_async_submit.js");
                }

                // Хук отправки формы перевода подарков для сохранение информации о персонаже в текущем аккаунте
                await ExecuteJsModule("promo_submit_hook.js");

                // Периодический ping WebView
                await ExecuteJsModule("promo_ping.js");

                // Всплывающее окно "Активация сундуков"
                await ExecuteJsModule("promo_chests_popup.js");

                // Всплывающее окно "История передачи" (открывается после reload по маркеру)
                await ExecuteJsModule("promo_transfer_history_popup.js");

                // Запуск зарегистрированных модулей (безопасно, если Promo отсутствует)
                try
                {
                    await Browser.ExecuteScriptAsync("try{ if (window.Promo && Promo.autoRun) Promo.autoRun(); }catch(e){}");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            // ignored
        }
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        try
        {
            Wv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30);
        }
        catch
        {
        }

        try
        {
            // Enable web messages and subscribe to receive messages from injected scripts
            Wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
        }
        catch
        {
        }

        try
        {
            var core = Wv.CoreWebView2;
            // JS-хелпер конфигурации приложения для страницы аккаунта.
            // В отличие от BrowserView, здесь возможен конфликт с window.appConfig самого сайта,
            // поэтому мы всегда создаём отдельный shim window.pwHubAppConfig и при необходимости
            // мягко «достраиваем» методы get/set на существующем window.appConfig (только если их нет).
            var helper = @"(function(){
  try{
    if (!window.pwHubAppConfig){
      var pending = new Map();
      var nextId = 1;
      function tryLocalGet(key, def){ try{ var raw = localStorage.getItem(key); return raw!=null ? JSON.parse(raw) : def; }catch(_){ return def; } }
      function tryLocalSet(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch(_){ }
      }

      var shim = {
        get: function(key, def){
          return new Promise(function(resolve){
            var id = nextId++;
            pending.set(id, resolve);
            try{
              if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                window.chrome.webview.postMessage({ type: 'appConfig:get', id: id, key: key });
              } else {
                resolve(tryLocalGet(key, def));
                return;
              }
            } catch(_){
              resolve(tryLocalGet(key, def));
              return;
            }
            setTimeout(function(){
              if (pending.has(id)){
                pending.delete(id);
                resolve(tryLocalGet(key, def));
              }
            }, 800);
          });
        },
        set: function(key, val){
          return new Promise(function(resolve){
            var id = nextId++;
            pending.set(id, resolve);
            try{
              if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                window.chrome.webview.postMessage({ type: 'appConfig:set', id: id, key: key, value: val });
              } else {
                tryLocalSet(key, val);
                resolve(true);
                return;
              }
            } catch(_){
              tryLocalSet(key, val);
              resolve(true);
              return;
            }
            setTimeout(function(){
              if (pending.has(id)){
                pending.delete(id);
                tryLocalSet(key, val);
                resolve(true);
              }
            }, 800);
          });
        }
      };

      // Наш «официальный» канал для скриптов Pw.Hub
      window.pwHubAppConfig = shim;

      // Аккуратно достраиваем window.appConfig, не ломая уже существующие поля/методы.
      try{
        if (!window.appConfig){ window.appConfig = {}; }
        if (!window.appConfig.get){ window.appConfig.get = shim.get; }
        if (!window.appConfig.set){ window.appConfig.set = shim.set; }
      }catch(_){ }

      if (window.chrome && window.chrome.webview){
        window.chrome.webview.addEventListener('message', function(ev){
          try{
            var msg = ev && ev.data;
            if (!msg || msg.type !== 'appConfig:resp') return;
            var res = pending.get(msg.id);
            if (!res) return;
            pending.delete(msg.id);
            if ('value' in msg) res(msg.value); else res(true);
          }catch(__){}
        });
      }
    }
  }catch(__){}
})();";
            try
            {
                core.AddScriptToExecuteOnDocumentCreatedAsync(helper);
            }
            catch
            {
            }
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2.WebMessageReceived -= CoreWebView2OnWebMessageReceived;
        }
        catch
        {
        }

        try
        {
            Wv.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;
        }
        catch
        {
        }

        try
        {
            UpdateNavUi();
        }
        catch
        {
        }

        try
        {
            AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
        }
        catch
        {
        }

        Browser.SetCookieAsync([]);
    }


    private async void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            IsLoading = false;
        }
        catch
        {
        }

        if (AccountManager.CurrentAccount == null)
            return;
        var exists = await Browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 500);
        if (!exists)
            return;
        var imgSrc =
            await Browser.ExecuteScriptAsync("document.querySelector(\"div.user_photo > span > a > img\").src");
        if (string.IsNullOrEmpty(imgSrc))
            return;
        if (imgSrc.Contains("null"))
            return;
        var img = imgSrc.Trim('"');
        var hasChanges = false;
        if (img != AccountManager.CurrentAccount.ImageSource)
        {
            AccountManager.CurrentAccount.ImageSource = img;
            hasChanges = true;
        }

        if (string.IsNullOrEmpty(AccountManager.CurrentAccount.SiteId))
        {
            AccountManager.CurrentAccount.SiteId = await AccountManager.GetSiteId();
            hasChanges = true;
        }

        if (!hasChanges)
            return;
        await using var db = new AppDbContext();
        db.Update(AccountManager.CurrentAccount);
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
        }
    }

    private void CoreWebView2OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            // Try to get raw string payload
            string payloadStr = null;
            try
            {
                payloadStr = e.TryGetWebMessageAsString();
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(payloadStr))
            {
                try
                {
                    payloadStr = e.WebMessageAsJson;
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(payloadStr)) return;

            try
            {
                //System.Diagnostics.Debug.WriteLine("[WebMessage] raw=" +
                //                                   (payloadStr?.Length > 300
                //                                       ? payloadStr.Substring(0, 300) + "..."
                //                                       : payloadStr));
            }
            catch
            {
            }

            using var doc = JsonDocument.Parse(payloadStr);
            var root = doc.RootElement;
            // Some pages may double-encode; handle quoted JSON
            if (root.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var inner = JsonDocument.Parse(root.GetString() ?? "{}");
                    HandleWebPayload(inner.RootElement);
                    return;
                }
                catch
                {
                }
            }

            HandleWebPayload(root);
        }
        catch
        {
        }
    }

    private void HandleWebPayload(JsonElement root)
    {
        try
        {
            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();
            // Канал конфигурации приложения: appConfig.get/set
            if (string.Equals(type, "appConfig:get", StringComparison.Ordinal))
            {
                try
                {
                    var appConfig = App.Services.GetService<IAppConfigService>();
                    if (appConfig == null) return;

                    var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                    var key = root.TryGetProperty("key", out var kEl) ? kEl.GetString() : null;
                    string? value = null;
                    if (!string.IsNullOrEmpty(key))
                    {
                        appConfig.TryGetString(key!, out value);
                    }

                    var valuePart = value ?? "null";
                    var resp = $"{{\"type\":\"appConfig:resp\",\"id\":{id},\"value\":{valuePart}}}";
                    try
                    {
                        Wv.CoreWebView2?.PostWebMessageAsJson(resp);
                    }
                    catch
                    {
                    }
                }
                catch
                {
                }

                return;
            }

            if (string.Equals(type, "appConfig:set", StringComparison.Ordinal))
            {
                try
                {
                    var appConfig = App.Services.GetService<IAppConfigService>();
                    if (appConfig == null) return;

                    var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                    var key = root.TryGetProperty("key", out var kEl) ? kEl.GetString() : null;
                    if (!string.IsNullOrEmpty(key))
                    {
                        if (root.TryGetProperty("value", out var vEl))
                        {
                            var raw = vEl.GetRawText();
                            appConfig.SetString(key!, raw);
                        }
                        else
                        {
                            appConfig.SetString(key!, "null");
                        }
                    }

                    var resp = JsonSerializer.Serialize(new { type = "appConfig:resp", id = id, ok = true });
                    try
                    {
                        Wv.CoreWebView2?.PostWebMessageAsJson(resp);
                    }
                    catch
                    {
                    }

                    try
                    {
                        _ = appConfig.SaveAsync();
                    }
                    catch
                    {
                    }
                }
                catch
                {
                }

                return;
            }

            // Диагностический канал логов из JS (promoLog)
            if (string.Equals(type, "promo_log", StringComparison.Ordinal))
            {
                try
                {
                    var evName = root.TryGetProperty("event", out var evEl) ? evEl.GetString() : null;
                    var ts = root.TryGetProperty("ts", out var tsEl) ? tsEl.GetInt64() : 0L;
                    string? dataStr = null;
                    if (root.TryGetProperty("data", out var dataEl))
                    {
                        try
                        {
                            var raw = dataEl.GetRawText();
                            if (!string.IsNullOrWhiteSpace(raw))
                            {
                                dataStr = raw.Length > 500 ? raw.Substring(0, 500) + "..." : raw;
                            }
                        }
                        catch
                        {
                        }
                    }

                    try
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[promo_popup] event={evName ?? "(null)"}, ts={ts}, data={dataStr ?? "null"}");
                    }
                    catch
                    {
                    }
                }
                catch
                {
                }

                return;
            }

            // Accept diagnostic pings silently
            if (string.Equals(type, "promo_ping", StringComparison.Ordinal) ||
                string.Equals(type, "promo_ping2", StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(type, "promo_form_submit", StringComparison.Ordinal)) return;

            var doVal = root.TryGetProperty("do", out var doEl) ? doEl.GetString() ?? string.Empty : string.Empty;
            var accInfo = root.TryGetProperty("acc_info", out var accEl)
                ? accEl.GetString() ?? string.Empty
                : string.Empty;

            List<string> cartItems = new();
            if (root.TryGetProperty("cart_items", out var cartEl) && cartEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in cartEl.EnumerateArray())
                {
                    try
                    {
                        cartItems.Add(it.ToString());
                    }
                    catch
                    {
                    }
                }
            }

            void apply()
            {
                var acc = AccountManager?.CurrentAccount;
                if (acc == null) return;
                try
                {
                    acc.PromoDo = doVal;
                }
                catch
                {
                }

                try
                {
                    acc.PromoAccInfo = accInfo;
                }
                catch
                {
                }

                try
                {
                    acc.PromoCartItems = cartItems;
                }
                catch
                {
                }

                try
                {
                    acc.PromoLastSubmittedAt = DateTime.Now;
                }
                catch
                {
                }

                using var db = new AppDbContext();
                db.Update(acc);
                db.SaveChanges();
            }

            if (!Dispatcher.CheckAccess()) Dispatcher.Invoke(apply);
            else apply();
        }
        catch
        {
        }
    }

    private void CoreWebView2OnHistoryChanged(object? sender, object e)
    {
        try
        {
            UpdateNavUi();
        }
        catch
        {
        }
    }

    private void UpdateNavUi()
    {
        try
        {
            var core = Wv?.CoreWebView2;
            if (core == null)
            {
                BtnBack.IsEnabled = false;
                BtnForward.IsEnabled = false;
                BtnRefresh.IsEnabled = false;
            }
            else
            {
                BtnBack.IsEnabled = core.CanGoBack;
                BtnForward.IsEnabled = core.CanGoForward;
                BtnRefresh.IsEnabled = true;
            }
        }
        catch
        {
        }

        try
        {
            AddressBox.Text = Wv?.Source?.ToString() ?? AddressBox.Text;
        }
        catch
        {
        }
    }

    private void BtnBack_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Wv?.CoreWebView2?.CanGoBack == true) Wv.CoreWebView2.GoBack();
        }
        catch
        {
        }

        try
        {
            UpdateNavUi();
        }
        catch
        {
        }
    }

    private void BtnForward_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Wv?.CoreWebView2?.CanGoForward == true) Wv.CoreWebView2.GoForward();
        }
        catch
        {
        }

        try
        {
            UpdateNavUi();
        }
        catch
        {
        }
    }

    private void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Wv?.CoreWebView2 != null) Wv.CoreWebView2.Reload();
        }
        catch
        {
        }

        try
        {
            UpdateNavUi();
        }
        catch
        {
        }
    }

    // Quick actions
    private void BtnHome_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Wv?.CoreWebView2 != null)
                Wv.CoreWebView2.Navigate("https://pwonline.ru/");
            else
                Wv.Source = new Uri("https://pwonline.ru/");
        }
        catch
        {
        }
    }

    private void BtnPromo_OnClick(object sender, RoutedEventArgs e)
    {
        const string url = "https://pwonline.ru/promo_items.php";
        try
        {
            if (Wv?.CoreWebView2 != null)
                Wv.CoreWebView2.Navigate(url);
            else
                Wv.Source = new Uri(url);
        }
        catch
        {
        }
    }

    private void BtnMdm_OnClick(object sender, RoutedEventArgs e)
    {
        const string url = "https://pwonline.ru/chests2.php";
        try
        {
            if (Wv?.CoreWebView2 != null)
                Wv.CoreWebView2.Navigate(url);
            else
                Wv.Source = new Uri(url);
        }
        catch
        {
        }
    }

    private void BtnZoglo_OnClick(object sender, RoutedEventArgs e)
    {
        const string url = "https://pwonline.ru/static/lp/playnewpw1/?mt_sub1=8356541&mt_click_id=mt-rihxx9-1762538013-2465897769";
        try
        {
            if (Wv?.CoreWebView2 != null)
                Wv.CoreWebView2.Navigate(url);
            else
                Wv.Source = new Uri(url);
        }
        catch
        {
        }
    }

    private void AddressBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var raw = AddressBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        string url = BuildAllowedUrl(raw);
        if (url == null) return; // запрещаем чужие домены

        try
        {
            if (Wv?.CoreWebView2 != null)
                Wv.CoreWebView2.Navigate(url);
            else
                Wv.Source = new Uri(url);
        }
        catch
        {
        }
    }

    private static string BuildAllowedUrl(string input)
    {
        try
        {
            string s = input;
            if (s.StartsWith("/"))
            {
                s = "https://pwonline.ru" + s;
            }
            else if (!s.StartsWith("http://") && !s.StartsWith("https://"))
            {
                s = "https://" + s;
            }

            if (s.Equals("https://pwonline.ru", StringComparison.OrdinalIgnoreCase))
                s += "/";

            var ok = s.StartsWith("https://pwonline.ru") || s.StartsWith("http://pwonline.ru") ||
                     s.StartsWith("https://pw.mail.ru") || s.StartsWith("http://pw.mail.ru");
            return ok ? s : null;
        }
        catch
        {
            return null;
        }
    }
}