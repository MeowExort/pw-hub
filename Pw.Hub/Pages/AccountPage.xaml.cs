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
        set { if (value != _isNoAccountOverlayVisible) { _isNoAccountOverlayVisible = value; OnPropertyChanged(nameof(IsNoAccountOverlayVisible)); } }
    }

    // Loading indicator state
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (value != _isLoading) { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) { try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); } catch { } }

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
                catch { }
            };
        }

        // Инициализация UI панели навигации
        try { UpdateNavUi(); } catch { }
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }

        // Subscribe only to account changed to hide initial overlay after first successful switch
        try
        {
            AccountManager.CurrentAccountChanged += OnCurrentAccountChanged;
        }
        catch { }

        LuaRunner = new LuaScriptRunner(AccountManager, Browser);
    }

    private static string? _promoChestsPopupJs;

    private static string? GetPromoChestsPopupJs()
    {
        try
        {
            if (!string.IsNullOrEmpty(_promoChestsPopupJs))
                return _promoChestsPopupJs;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Scripts", "promo", "promo_chests_popup.js");
            if (File.Exists(path))
            {
                _promoChestsPopupJs = File.ReadAllText(path);
                return _promoChestsPopupJs;
            }
        }
        catch
        {
            // ignored
        }

        return null;
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
            try { AccountManager.CurrentAccountChanged -= OnCurrentAccountChanged; } catch { }
        }
        catch { }
    }

    // Host delegate for WebCoreBrowser: safely replace Wv in the visual tree and rewire handlers
    private async Task ReplaceWebViewControlAsync(WebView2 newWv)
    {
        if (newWv == null) return;

        // Detach handlers from old control
        try { Wv.NavigationCompleted -= WvOnNavigationCompleted; } catch { }
        try { Wv.NavigationStarting -= WvOnNavigationStarting; } catch { }
        try { Wv.NavigationCompleted -= Wv_OnNavigationCompleted; } catch { }
        try { Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted; } catch { }
        try { Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged; } catch { }

        // Preserve placement
        var parent = Wv.Parent as Grid;
        int row = Grid.GetRow(Wv);
        int column = Grid.GetColumn(Wv);
        int rowSpan = Grid.GetRowSpan(Wv);
        int columnSpan = Grid.GetColumnSpan(Wv);
        int index = parent?.Children.IndexOf(Wv) ?? -1;

        // Remove and dispose old control
        try { Wv.Dispose(); } catch { }
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
        try { Wv.NavigationCompleted += WvOnNavigationCompleted; } catch { }
        try { Wv.NavigationStarting += WvOnNavigationStarting; } catch { }
        try { Wv.NavigationCompleted += Wv_OnNavigationCompleted; } catch { }
        try { Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted; } catch { }

        // Ensure core of the new control if possible (browser will also try)
        try { await Wv.EnsureCoreWebView2Async(); } catch { }
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
        try { await newWv.EnsureCoreWebView2Async(); } catch { }
    }

    // Finalize swap: remove old control, show new one, rewire handlers
    private async Task FinalizeSwapWebViewControlAsync(WebView2 newWv)
    {
        if (newWv == null) return;

        // Detach handlers from old control
        try { Wv.NavigationCompleted -= WvOnNavigationCompleted; } catch { }
        try { Wv.NavigationStarting -= WvOnNavigationStarting; } catch { }
        try { Wv.NavigationCompleted -= Wv_OnNavigationCompleted; } catch { }
        try { Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted; } catch { }

        var parent = Wv.Parent as Grid;
        try { Wv.Dispose(); } catch { }
        try { parent?.Children.Remove(Wv); } catch { }

        // Switch field and attach handlers to the new control
        Wv = newWv;
        try { Wv.NavigationCompleted += WvOnNavigationCompleted; } catch { }
        try { Wv.NavigationStarting += WvOnNavigationStarting; } catch { }
        try { Wv.NavigationCompleted += Wv_OnNavigationCompleted; } catch { }
        try { Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted; } catch { }

        // Make it visible
        try { Wv.Visibility = Visibility.Visible; } catch { }

        // Ensure core again just in case
        try { await Wv.EnsureCoreWebView2Async(); } catch { }

        // After swap, Core might already be initialized and InitializationCompleted won't fire.
        // Ensure WebMessage pipeline is configured for the active Core instance.
        try
        {
            if (Wv?.CoreWebView2 != null)
            {
                try { Wv.CoreWebView2.Settings.IsWebMessageEnabled = true; } catch { }
                try { Wv.CoreWebView2.WebMessageReceived -= CoreWebView2OnWebMessageReceived; } catch { }
                try { Wv.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived; } catch { }
                try { Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged; } catch { }
                try { Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged; } catch { }
            }
        }
        catch { }
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
        catch { }

        if (!allowed)
        {
            try { IsLoading = false; } catch { }
            e.Cancel = true;
            return;
        }

        try { IsLoading = true; } catch { }
    }

    private async void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try { IsLoading = false; } catch { }
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }
        try { UpdateNavUi(); } catch { }
        if (Browser.Source.AbsoluteUri.Contains("promo_items.php"))
        {
            if (!Browser.Source.AbsoluteUri.Contains("do=activate"))
            {
                await Browser.ExecuteScriptAsync(
                    """
                    $('.items_container input[type=checkbox]').unbind('click');
                    """);
            }

            // Передаём в страницу последнее значение acc_info из текущего аккаунта для предвыбора персонажа
            try
            {
                var lastAccInfo = AccountManager?.CurrentAccount?.PromoAccInfo ?? string.Empty;
                var jsValue = JsonSerializer.Serialize(lastAccInfo, JsonSerializerOptions.Web);
                await Browser.ExecuteScriptAsync($"window.__pwLastAccInfo = {jsValue};");
            }
            catch { }

            await Browser.ExecuteScriptAsync(
                """
                var hasElement = !!document.getElementById('promo_separator')
                if (!hasElement) 
                {
                    var element = document.createElement('div');
                    element.id = 'promo_separator';
                    $('.promo_container_content_body')[0].after(element);
                }
                """);
            
            if (!Browser.Source.AbsoluteUri.Contains("do=activate"))
            {
                await Browser.ExecuteScriptAsync(
                    """
                    try {
                        // Send a lightweight ping to verify WebMessage channel early
                        try{ if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){ window.chrome.webview.postMessage('{"type":"promo_ping","ts":"'+Date.now()+'"}'); } }catch(_){}

                        // helpers to persist/restore state (позиция/размер/свернутость окна «Управление»)
                        var PROMO_POPUP_KEY = 'promo_popup_state';
                        // Быстрый bootstrap-ключ в localStorage, чтобы при старте попап сразу оказывался на сохранённом месте,
                        // а не «переезжал» после асинхронной загрузки конфигурации
                        var PROMO_POPUP_BOOTSTRAP_KEY = 'promo_popup_state_bootstrap';
                        // Простая обёртка для логов из JS → .NET
                        function promoLog(eventName, payload){
                            try{
                                var msg = { type:'promo_log', event:eventName, data:payload||null, ts:Date.now() };
                                if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                                    window.chrome.webview.postMessage(JSON.stringify(msg));
                                }
                            }catch(_){ }
                        }

                        // Кэш состояния внутри сессии (замена прямого appConfig.get для синхронного чтения)
                        var __promoPopup_state = null;

                        // Локальный shim на случай, если по какой‑то причине глобальный pwHubAppConfig ещё не создан.
                        // Строим поверх window.chrome.webview.postMessage с теми же сообщениями appConfig:get/set.
                        function ensurePwHubAppConfigShim(){
                            try{
                                if (window.pwHubAppConfig && (window.pwHubAppConfig.get || window.pwHubAppConfig.set)){
                                    try{ promoLog('ensurePwHubAppConfig_exists', { hasPwHub:true }); }catch(__){}
                                    return;
                                }
                                var hasChrome = !!(window.chrome && window.chrome.webview && window.chrome.webview.postMessage);
                                try{ promoLog('ensurePwHubAppConfig_create_attempt', { hasChrome: hasChrome }); }catch(__){}
                                if (!hasChrome) return;

                                var pending = new Map();
                                var nextId = 1;
                                function tryLocalGet(key, def){ try{ var raw = localStorage.getItem(key); return raw!=null ? JSON.parse(raw) : def; }catch(_){ return def; } }
                                function tryLocalSet(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch(_){ } }

                                var shim = {
                                    get: function(key, def){
                                        return new Promise(function(resolve){
                                            var id = nextId++;
                                            pending.set(id, resolve);
                                            try{
                                                window.chrome.webview.postMessage({ type: 'appConfig:get', id: id, key: key });
                                            }catch(_){
                                                pending.delete(id);
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
                                                window.chrome.webview.postMessage({ type: 'appConfig:set', id: id, key: key, value: val });
                                            }catch(_){
                                                pending.delete(id);
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

                                window.pwHubAppConfig = shim;
                                try{
                                    if (!window.appConfig){ window.appConfig = {}; }
                                    if (!window.appConfig.get) window.appConfig.get = shim.get;
                                    if (!window.appConfig.set) window.appConfig.set = shim.set;
                                }catch(__){}

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

                                try{ promoLog('ensurePwHubAppConfig_created', { hasPwHub: !!window.pwHubAppConfig }); }catch(__){}
                            }catch(e){
                                try{ promoLog('ensurePwHubAppConfig_exception', e && e.message ? e.message : String(e)); }catch(__){}
                            }
                        }

                        // Попробуем гарантированно подготовить shim до первого запроса конфигурации.
                        ensurePwHubAppConfigShim();

                        // Получение актуального объекта конфигурации приложения.
                        // Сначала используем наш shim window.pwHubAppConfig, затем fallback на window.appConfig.
                        function getPromoAppConfig(){
                            try{
                                var hasPw = !!(window.pwHubAppConfig && (window.pwHubAppConfig.get || window.pwHubAppConfig.set));
                                var hasApp = !!(window.appConfig && (window.appConfig.get || window.appConfig.set));
                                try{ promoLog('getPromoAppConfig_probe', { hasPwHub: hasPw, hasAppConfig: hasApp }); }catch(__){}
                                if (hasPw) return window.pwHubAppConfig;
                                if (hasApp) return window.appConfig;
                            }catch(_){ }
                            try{ promoLog('getPromoAppConfig_null', null); }catch(__){}
                            return null;
                        }

                        // Сохранение состояния: appConfig + in-memory кэш (__promoPopup_state) + локальный bootstrap в localStorage
                        function savePopupState(el, state){
                            try{
                                state = state || {};
                                __promoPopup_state = state;
                                promoLog('savePopupState', state);
                                // Локальный bootstrap для мгновенного восстановления при следующем старте
                                try{
                                    localStorage.setItem(PROMO_POPUP_BOOTSTRAP_KEY, JSON.stringify(state));
                                }catch(__){}
                                try{
                                    var appCfg = getPromoAppConfig();
                                    if (appCfg && appCfg.set){
                                        appCfg.set(PROMO_POPUP_KEY, state)
                                            .then(function(){ try{ promoLog('savePopupState_ok', null); }catch(__){} })
                                            .catch(function(err){ try{ promoLog('savePopupState_err', (err && (err.message||err)) || 'unknown'); }catch(__){} });
                                    } else {
                                        promoLog('savePopupState_no_appConfig', null);
                                    }
                                }catch(e){
                                    promoLog('savePopupState_exception', e && e.message ? e.message : String(e));
                                }
                            }catch(e){
                                promoLog('savePopupState_outer_exception', e && e.message ? e.message : String(e));
                            }
                        }

                        // Загрузка состояния: сначала пытаемся синхронно восстановить из кэша/localStorage,
                        // а затем при первом вызове асинхронно подтягиваем из appConfig
                        function loadPopupState(){
                            try{
                                if (__promoPopup_state != null){
                                    return __promoPopup_state;
                                }

                                // 1) Быстрая инициализация из localStorage, чтобы попап сразу появился в нужной позиции/размере
                                try{
                                    var rawBootstrap = localStorage.getItem(PROMO_POPUP_BOOTSTRAP_KEY);
                                    if (rawBootstrap){
                                        try{
                                            var cached = JSON.parse(rawBootstrap);
                                            if (cached && typeof cached === 'object'){
                                                __promoPopup_state = cached;
                                                if (popup){
                                                    try{
                                                        if (cached.width) popup.style.width = cached.width + 'px';
                                                        if (cached.height) popup.style.height = cached.height + 'px';
                                                        if (cached.useLeftTop){
                                                            popup.style.left = (cached.left||16) + 'px';
                                                            popup.style.top = (cached.top||16) + 'px';
                                                            popup.style.right = '';
                                                            popup.style.bottom = '';
                                                        } else {
                                                            popup.style.right = (cached.right||16) + 'px';
                                                            popup.style.bottom = (cached.bottom||16) + 'px';
                                                            popup.style.left = '';
                                                            popup.style.top = '';
                                                        }
                                                        // Свернутость восстанавливаем аккуратно через helper,
                                                        // он перезапишет состояние и синхронно не мигает
                                                        try{
                                                            if (typeof cached.collapsed === 'boolean'){
                                                                setCollapsed(!!cached.collapsed);
                                                            }
                                                        }catch(__){}
                                                    }catch(__){}
                                                }
                                                return __promoPopup_state;
                                            }
                                        }catch(__){}
                                    }
                                }catch(__){}

                                try{
                                    var appCfg = getPromoAppConfig();
                                    if (!window.__promoPopup_stateLoading && appCfg && appCfg.get){
                                        window.__promoPopup_stateLoading = true;
                                        promoLog('loadPopupState_fetch_start', null);
                                        appCfg.get(PROMO_POPUP_KEY, null).then(function(st){
                                            window.__promoPopup_stateLoading = false;
                                            try{
                                                __promoPopup_state = st || null;
                                                promoLog('loadPopupState_fetch_ok', st);
                                                if (!st || !popup) return;
                                                // применим размеры и позицию
                                                try{
                                                    if (st.width) popup.style.width = st.width + 'px';
                                                    if (st.height) popup.style.height = st.height + 'px';
                                                    if (st.useLeftTop){
                                                        popup.style.left = (st.left||16) + 'px';
                                                        popup.style.top = (st.top||16) + 'px';
                                                        popup.style.right = '';
                                                        popup.style.bottom = '';
                                                    } else {
                                                        popup.style.right = (st.right||16) + 'px';
                                                        popup.style.bottom = (st.bottom||16) + 'px';
                                                        popup.style.left = '';
                                                        popup.style.top = '';
                                                    }
                                                    // свернутость тоже восстанавливаем через существующий helper
                                                    try{ setCollapsed(!!st.collapsed); }catch(__){}
                                                }catch(__){}
                                            }catch(ex){
                                                promoLog('loadPopupState_apply_exception', ex && ex.message ? ex.message : String(ex));
                                            }
                                        }).catch(function(err){
                                            window.__promoPopup_stateLoading = false;
                                            promoLog('loadPopupState_fetch_err', (err && (err.message||err)) || 'unknown');
                                        });
                                    }
                                }catch(e){
                                    promoLog('loadPopupState_schedule_exception', e && e.message ? e.message : String(e));
                                }

                                return __promoPopup_state;
                            }catch(e){
                                promoLog('loadPopupState_outer_exception', e && e.message ? e.message : String(e));
                                return __promoPopup_state;
                            }
                        }

                        // Create floating popup if not exists
                        var popup = document.getElementById('promo_popup');
                        var header, contentWrap, compact;
                        var collapsed = false;

                        function getToggleBtn(){
                            try{ return (header ? header.querySelector('button') : null) || document.querySelector('#promo_popup > div button'); }catch(e){ return null; }
                        }

                        function recalcContentHeight(){
                            try{
                                if (!popup || !contentWrap) return;
                                var h = header && header.style.display !== 'none' ? header.offsetHeight : 0;
                                var total = popup.getBoundingClientRect().height;
                                var contentH = Math.max(0, total - h);
                                contentWrap.style.maxHeight = contentH + 'px';
                                contentWrap.style.height = contentH + 'px';
                            }catch(e){}
                        }

                        function setCollapsed(c){
                            try{
                                collapsed = !!c;
                                var st = loadPopupState() || {};
                                st.collapsed = collapsed;
                                var res = document.getElementById('promo_popup_resizer');
                                if (collapsed){
                                    // save current size before collapsing
                                    try{
                                        var rect = popup.getBoundingClientRect();
                                        st.width = rect.width; st.height = rect.height;
                                    }catch(ex){}
                                    // hide full UI
                                    if (contentWrap) contentWrap.style.display = 'none';
                                    if (header) header.style.display = 'none';
                                    if (res) res.style.display = 'none';
                                    // shrink container to compact pill
                                    popup.style.width = 'auto';
                                    popup.style.height = 'auto';
                                    popup.style.minWidth = '0px';
                                    popup.style.minHeight = '0px';
                                    // ВЕРНУЛИ как было ранее: рамку/фон/тень НЕ скрываем у «Управление» в компактном режиме
                                    if (compact) compact.style.display = 'inline-block';
                                } else {
                                    if (compact) compact.style.display = 'none';
                                    if (header) header.style.display = 'flex';
                                    if (contentWrap) contentWrap.style.display = 'block';
                                    if (res) res.style.display = 'block';
                                    // restore size if known
                                    var s2 = loadPopupState() || {};
                                    if (s2.width) popup.style.width = s2.width + 'px';
                                    if (s2.height) popup.style.height = s2.height + 'px';
                                    // restore mins to defaults
                                    popup.style.minWidth = '240px';
                                    popup.style.minHeight = '120px';
                                    // рамка/фон/тень и так сохранены — никаких дополнительных правок не требуется
                                    // recalc content height
                                    recalcContentHeight();
                                }
                                var toggleBtn = getToggleBtn();
                                if (toggleBtn){ toggleBtn.innerText = collapsed ? '+' : '−'; }
                                savePopupState(popup, st);
                            }catch(e){}
                        }
                        if (!popup) {
                            popup = document.createElement('div');
                            popup.id = 'promo_popup';
                            popup.style = [
                                'position: fixed',
                                'right: 12px', // чуть ближе к краю
                                'bottom: 12px',
                                'z-index: 2147483647',
                                'background: #F6F1E7',
                                'border: 1px solid #E2D8C9',
                                'border-radius: 10px',
                                'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
                                'overflow: hidden',
                                'color: #333',
                                'font-family: Arial, sans-serif',
                                // более компактная базовая ширина
                                'width: 300px',
                                'min-width: 220px',
                                // жёсткий предел по ширине, чтобы окно не занимало полэкрана
                                'max-width: 340px',
                                'min-height: 120px',
                                'max-height: 70vh'
                            ].join(';');

                        header = document.createElement('div');
                        header.style = 'display:flex;align-items:center;justify-content:space-between;padding:6px 10px;background:#EDE4D6;border-bottom:1px solid #E2D8C9;cursor:move;user-select:none;';
                        var hTitle = document.createElement('div');
                        hTitle.innerHTML = 'У<span class="lower">правление</span>';
                        hTitle.style = 'font-weight:700;color:#2c4a8d;font-size:13px;letter-spacing:0.2px;';
                        var toggleBtn = document.createElement('button');
                        toggleBtn.innerText = '−';
                        toggleBtn.title = 'Свернуть';
                        toggleBtn.style = 'border:none;background:#D2C0BE;color:#333;border-radius:12px;padding:1px 6px;cursor:pointer;font-size:12px;line-height:16px;';

                        contentWrap = document.createElement('div');
                        contentWrap.id = 'promo_popup_content';
                        contentWrap.style = 'padding:8px;overflow:auto;font-size:12px;';

                        var container = document.createElement('div');
                        container.id = 'promo_container';

                        // legacy resize handle (теперь скрыт, чтобы убрать пользовательский ресайз)
                        var resizer = document.createElement('div');
                        resizer.id = 'promo_popup_resizer';
                        resizer.style = 'display:none;position:absolute;width:14px;height:14px;right:2px;bottom:2px;cursor:default;background:transparent;';
                        // small visual triangle (не отображается из-за display:none)
                        resizer.innerHTML = '<svg width="14" height="14" viewBox="0 0 14 14" style="display:block"><path d="M2 12 L12 2 L12 12 Z" fill="#00000022"/></svg>';

                        // compact view (as a tiny movable button)
                        compact = document.createElement('div');
                        compact.id = 'promo_popup_compact';
                        compact.style = 'display:none; margin:4px; padding:4px 10px; background:#EDE4D6; color:#2c4a8d; font-weight:700; font-size:12px; border-radius:14px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
                        compact.title = 'Развернуть панель управления';
                        compact.innerText = 'Управление';

                        contentWrap.appendChild(container);
                        header.appendChild(hTitle);
                        header.appendChild(toggleBtn);
                        popup.appendChild(header);
                        popup.appendChild(contentWrap);
                        popup.appendChild(resizer);
                        popup.appendChild(compact);
                        document.body.appendChild(popup);

                        // Toggle/compact handlers with local state
                        toggleBtn.onclick = function(){ setCollapsed(!collapsed); };
                        // Click on compact should expand only if это именно клик, а не завершение перетаскивания
                        compact.addEventListener('click', function(e){
                            try{
                                if (compact && (compact.dataset.dragMoved === '1' || compact.dataset.dragJustDragged === '1')){
                                    e.preventDefault(); e.stopPropagation();
                                    return false;
                                }
                                setCollapsed(false);
                            }catch(ex){}
                        });
                    } else {
                        // if popup already exists, fetch sub-elements
                        header = popup.firstElementChild; // expected header
                        contentWrap = document.getElementById('promo_popup_content');
                        compact = document.getElementById('promo_popup_compact');
                        if (!compact){
                            compact = document.createElement('div');
                            compact.id = 'promo_popup_compact';
                            compact.style = 'display:none; margin:4px; padding:4px 10px; background:#EDE4D6; color:#2c4a8d; font-weight:700; font-size:12px; border-radius:14px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
                            compact.title = 'Развернуть панель управления';
                            compact.innerText = 'Управление';
                            popup.appendChild(compact);
                        }
                        var tbtn = getToggleBtn();
                        if (tbtn){ tbtn.onclick = function(){ setCollapsed(!(loadPopupState()||{}).collapsed); }; }
                        if (compact){
                            // Повторная инициализация обработчика клика с защитой от drag→click
                            compact.addEventListener('click', function(e){
                                try{
                                    if (compact && (compact.dataset.dragMoved === '1' || compact.dataset.dragJustDragged === '1')){
                                        e.preventDefault(); e.stopPropagation();
                                        return false;
                                    }
                                    setCollapsed(false);
                                }catch(ex){}
                            });
                        }
                    }

                    // initialize drag/resize once
                    if (popup && !popup.getAttribute('data-draggable-inited')){
                        popup.setAttribute('data-draggable-inited','1');

                        function applyContentHeight(){
                            try{
                                if (!popup || !contentWrap) return;
                                var headH = header ? header.offsetHeight : 0;
                                var total = popup.clientHeight; // точнее, чем getBoundingClientRect для высоты контейнера
                                // учитываем внутренние отступы контента, чтобы реальная область прокрутки была равна (total - headH)
                                var cs = window.getComputedStyle(contentWrap);
                                var padV = (parseFloat(cs.paddingTop)||0) + (parseFloat(cs.paddingBottom)||0);
                                var contentH = Math.max(0, total - headH);
                                // высота самого блока = желаемая область, минус внутренние отступы
                                var blockH = Math.max(0, contentH - padV);
                                contentWrap.style.maxHeight = contentH + 'px';
                                contentWrap.style.height = blockH + 'px';
                            }catch(e){}
                        }

                        // Дебаунсер для автосайза
                        var __autosizeTimer = null;
                        function scheduleAutosize(delay){
                            try{
                                if (__autosizeTimer) { clearTimeout(__autosizeTimer); __autosizeTimer = null; }
                                __autosizeTimer = setTimeout(function(){
                                    try{ requestAnimationFrame(function(){ requestAnimationFrame(function(){ autosizePopupIfNoState(true); }); }); }catch(e){ try{ autosizePopupIfNoState(true); }catch(_){} }
                                }, typeof delay==='number'? delay : 50);
                            }catch(e){}
                        }

                        // Авторазмер: подгоняем так, чтобы контент помещался без скролла
                        function autosizePopupIfNoState(force){
                            try{
                                if (!popup || !contentWrap) return;
                                var st0 = loadPopupState() || {};
                                // Если попап свёрнут — не трогаем (развернётся — пересчитаем)
                                if (st0 && st0.collapsed) return;

                                // Временно убираем ограничения у контента для измерения естественного размера
                                var prevOverflow = contentWrap.style.overflow;
                                var prevH = contentWrap.style.height;
                                var prevMaxH = contentWrap.style.maxHeight;
                                var prevW = popup.style.width;
                                var prevMinW = popup.style.minWidth;
                                var prevMinH = popup.style.minHeight;
                                contentWrap.style.overflow = 'visible';
                                contentWrap.style.height = 'auto';
                                contentWrap.style.maxHeight = 'none';

                                // Элемент контента, который реально содержит наши кнопки
                                var inner = contentWrap.firstElementChild || contentWrap;
                                // Учитываем паддинги контента
                                var cs = window.getComputedStyle(contentWrap);
                                var padV = (parseFloat(cs.paddingTop)||0) + (parseFloat(cs.paddingBottom)||0);
                                var padH = (parseFloat(cs.paddingLeft)||0) + (parseFloat(cs.paddingRight)||0);

                                // Высота = шапка + внутренняя прокрутка контента + вертикальные паддинги (+ нижний margin последнего элемента)
                                var headH = header ? header.offsetHeight : 0;
                                var cScrollH = inner.scrollHeight + padV;
                                try{
                                    var last = inner.lastElementChild;
                                    if (last){
                                        var lcs = window.getComputedStyle(last);
                                        var mb = parseFloat(lcs.marginBottom)||0;
                                        if (mb > 0) cScrollH += mb;
                                    }
                                }catch(__){}
                                // небольшой запас на возможные округления шрифтов/масштабирования
                                var desiredH = headH + cScrollH + 4;
                                var maxH = Math.floor(window.innerHeight * 0.8); // синхронно c CSS max-height:80vh
                                var minH = 140; // как по стилям
                                var newH = Math.max(minH, Math.min(maxH, desiredH));

                                // Ширина: берём максимальную из шапки и контента + горизонтальные паддинги
                                var headW = header ? header.scrollWidth : 0;
                                var cScrollW = inner.scrollWidth + padH;
                                var desiredW = Math.max(260, Math.max(headW, cScrollW));
                                var maxW = Math.floor(window.innerWidth * 0.5); // синхронно c CSS max-width:50vw
                                var newW = Math.max(260, Math.min(maxW, desiredW));

                                popup.style.width = newW + 'px';
                                popup.style.height = newH + 'px';

                                // Вернуть стили и пересчитать контентную область
                                contentWrap.style.overflow = prevOverflow;
                                contentWrap.style.height = prevH;
                                contentWrap.style.maxHeight = prevMaxH;
                                // гарантированно после установки размеров пересчитаем
                                requestAnimationFrame(function(){ applyContentHeight(); });

                                // Сохраняем автоподобранные размеры как базовые (но не помечаем их как ручной ресайз)
                                try{
                                    var stSave = loadPopupState() || {};
                                    stSave.width = newW;
                                    stSave.height = newH;
                                    savePopupState(popup, stSave);
                                }catch(__){}

                                // Отметим, что автосайз выполнен, но позволим повтор через scheduleAutosize, если force=true (после мутаций)
                                popup.setAttribute('data-autosized','1');
                            }catch(e){}
                        }

                        // restore state
                        (function(){
                            var st = loadPopupState();
                            if (!st) {
                                try{ autosizePopupIfNoState(); }catch(e){}
                                try{ applyContentHeight(); }catch(e){}
                                return;
                            }
                            // size
                            if (st.width) popup.style.width = st.width + 'px';
                            if (st.height) popup.style.height = st.height + 'px';
                            // position
                            if (st.useLeftTop){
                                popup.style.left = (st.left||16) + 'px';
                                popup.style.top = (st.top||16) + 'px';
                                popup.style.right = '';
                                popup.style.bottom = '';
                            } else {
                                popup.style.right = (st.right||16) + 'px';
                                popup.style.bottom = (st.bottom||16) + 'px';
                                popup.style.left = '';
                                popup.style.top = '';
                            }
                            // apply collapsed or expanded view via helper if available
                            try{ setCollapsed(!!st.collapsed); }catch(e){}
                            // если размеры не сохранены (старые пользователи), попробуем один раз авторазмер
                            if (!st.width && !st.height && !st.collapsed) { try{ autosizePopupIfNoState(); }catch(e){} }
                            try{ applyContentHeight(); }catch(e){}
                        })();

                        // drag logic (by header or compact pill)
                        (function(){
                            if (!header && !compact) return;
                            var dragging = false, startX=0, startY=0, startLeft=0, startTop=0;
                            var moved = false; var moveThreshold = 4; // px
                            function onMouseDown(e){
                                // allow clicking controls in header (like toggle btn) without drag
                                if (e.target && (e.target.tagName==='BUTTON' || e.target.closest('button'))) return;
                                dragging = true;
                                moved = false;
                                // сбрасываем флаги drag у header/compact перед началом
                                if (header){ delete header.dataset.dragMoved; delete header.dataset.dragJustDragged; }
                                if (compact){ delete compact.dataset.dragMoved; delete compact.dataset.dragJustDragged; }
                                var rect = popup.getBoundingClientRect();
                                // switch to left/top coordinates once dragging starts
                                var st = loadPopupState() || {};
                                if (!st.useLeftTop){
                                    // compute left/top from current right/bottom
                                    var left = window.innerWidth - rect.right;
                                    var top = rect.top;
                                    popup.style.left = (rect.left) + 'px';
                                    popup.style.top = (rect.top) + 'px';
                                    popup.style.right = '';
                                    popup.style.bottom = '';
                                    st.useLeftTop = true;
                                    st.left = rect.left;
                                    st.top = rect.top;
                                    savePopupState(popup, st);
                                }
                                startX = e.clientX; startY = e.clientY;
                                startLeft = parseFloat(popup.style.left || rect.left + '');
                                startTop = parseFloat(popup.style.top || rect.top + '');
                                document.addEventListener('mousemove', onMouseMove);
                                document.addEventListener('mouseup', onMouseUp);
                                e.preventDefault();
                            }
                            function onMouseMove(e){
                                if (!dragging) return;
                                var dx = e.clientX - startX;
                                var dy = e.clientY - startY;
                                if (!moved && (Math.abs(dx) > moveThreshold || Math.abs(dy) > moveThreshold)){
                                    moved = true;
                                    if (header){ header.dataset.dragMoved = '1'; }
                                    if (compact){ compact.dataset.dragMoved = '1'; }
                                }
                                var newLeft = startLeft + dx;
                                var newTop = startTop + dy;
                                // constrain
                                var r = popup.getBoundingClientRect();
                                var maxLeft = window.innerWidth - r.width;
                                var maxTop = window.innerHeight - r.height;
                                newLeft = Math.min(Math.max(0, newLeft), Math.max(0, maxLeft));
                                newTop = Math.min(Math.max(0, newTop), Math.max(0, maxTop));
                                popup.style.left = newLeft + 'px';
                                popup.style.top = newTop + 'px';
                            }
                            function onMouseUp(){
                                if (!dragging) return;
                                dragging = false;
                                document.removeEventListener('mousemove', onMouseMove);
                                document.removeEventListener('mouseup', onMouseUp);
                                var st = loadPopupState() || { useLeftTop: true };
                                var rect = popup.getBoundingClientRect();
                                st.left = rect.left; st.top = rect.top;
                                savePopupState(popup, st);
                                // помечаем, что только что было перетаскивание, чтобы подавить click после mouseup
                                if (moved){
                                    if (header){
                                        header.dataset.dragJustDragged = '1';
                                        setTimeout(function(){ delete header.dataset.dragJustDragged; delete header.dataset.dragMoved; }, 120);
                                    }
                                    if (compact){
                                        compact.dataset.dragJustDragged = '1';
                                        setTimeout(function(){ delete compact.dataset.dragJustDragged; delete compact.dataset.dragMoved; }, 120);
                                    }
                                }
                            }
                            if (header) header.addEventListener('mousedown', onMouseDown);
                            if (compact) compact.addEventListener('mousedown', onMouseDown);
                            // Клик по шапке: сворачивать/разворачивать, если это не был drag и не клик по кнопке
                            if (header){
                                header.addEventListener('click', function(e){
                                    try{
                                        // если клик пришелся на кнопку (toggle), то ничего не делаем — у неё свой обработчик
                                        if (e.target && (e.target.tagName==='BUTTON' || e.target.closest('button'))) return;
                                        if (header.dataset.dragMoved === '1' || header.dataset.dragJustDragged === '1'){
                                            e.preventDefault(); e.stopPropagation();
                                            return false;
                                        }
                                        // переключаем состояние
                                        var st = loadPopupState() || {};
                                        var isCollapsed = !!st.collapsed;
                                        setCollapsed(!isCollapsed);
                                    }catch(ex){}
                                });
                            }
                        })();

                        // resize logic (by handle)
                        (function(){
                            var handle = document.getElementById('promo_popup_resizer');
                            if (!handle) return;
                            // Пользовательский ресайз отключён: хендлер оставляем только как технический элемент,
                            // но не меняем размеры попапа по мыши.
                            function onDown(e){ try{ e.preventDefault(); }catch(_){ } }
                            function onMove(e){ /* no-op */ }
                            function onUp(){ /* no-op */ }
                            handle.addEventListener('mousedown', onDown);
                            // ensure initial content sizing
                            scheduleAutosize(0);
                            requestAnimationFrame(function(){ applyContentHeight(); });
                            window.addEventListener('resize', function(){
                                // При ресайзе окна убедимся, что контент влезает максимально корректно
                                applyContentHeight();
                                // Всегда запускаем автосайз (кроме случая свёрнутого попапа, который фильтруется внутри функции)
                                scheduleAutosize(50);
                            });
                        })();
                    }

                    // Build controls only once inside promo_container
                    if (!document.getElementById('selectAllBtn')) {
                        // Common styles (компактный helper-виджет)
                        var pillBtnCss = [
                            'margin:0',
                            'font-size:12px',
                            'line-height:18px',
                            'font-weight:500',
                            'border:none',
                            'cursor:pointer',
                            'padding:2px 10px',
                            'border-radius:999px',
                            '-webkit-appearance:button',
                            'text-rendering:auto',
                            'display:inline-block',
                            'text-align:center',
                            'white-space:nowrap',
                            'background-color:#D2C0BE',
                            'color:#2c2c2c'
                        ].join(';');
                        var rowCss = 'display:flex; align-items:center; gap:6px; flex-wrap:wrap; margin-bottom:3px;';

                        var buttonContainer = document.createElement('div');
                        // компактный вертикальный стек секций
                        buttonContainer.style = 'display:flex; flex-direction:column; gap:6px; margin-top:2px;';

                        // Заголовок панели управления как у helper-виджета
                        var panelTitle = document.createElement('div');
                        panelTitle.textContent = 'Быстрый выбор предметов';
                        panelTitle.style = 'font-size:11px; text-transform:uppercase; letter-spacing:0.6px; color:#7a6a54; margin:0 0 2px 2px;';
                        buttonContainer.append(panelTitle);

                        // Helpers: selection by label
                        var selectAll = document.createElement('button');
                        selectAll.innerText = 'Выбрать все';
                        selectAll.id = 'selectAllBtn';
                        selectAll.style = pillBtnCss;
                        selectAll.onclick = function() {
                            var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
                            checkboxes.forEach(function(checkbox) { 
                                if (!checkbox.checked){ checkbox.checked = true; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
                            });
                            try{ if (window.__promoSelected_schedule) window.__promoSelected_schedule(0); }catch(e){}
                            return false;
                        };

                        var clearAll = document.createElement('button');
                        clearAll.innerText = 'Убрать все';
                        clearAll.id = 'clearAllBtn';
                        clearAll.style = pillBtnCss;
                        clearAll.onclick = function() {
                            var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
                            checkboxes.forEach(function(checkbox) { 
                                if (checkbox.checked){ checkbox.checked = false; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
                            });
                            try{ if (window.__promoSelected_schedule) window.__promoSelected_schedule(0); }catch(e){}
                            return false;
                        };

                        var selectByLabelTextRegex = function(pattern) {
                            var labels = document.querySelectorAll('.items_container label');
                            labels.forEach(function(label) {
                                if (pattern.test((label.innerText||'').toLowerCase())) {
                                    var checkboxId = label.getAttribute('for');
                                    var checkbox = document.getElementById(checkboxId);
                                    if (checkbox && checkbox.type === 'checkbox') {
                                        var should = true;
                                        if (!checkbox.checked){ checkbox.checked = true; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
                                    }
                                }
                            });
                        };

                        var selectByLabelTextRegexes = function(patterns) {
                            patterns.forEach(function(pattern) { selectByLabelTextRegex(pattern); });
                        };

                        var selectByLabelText = function(text) {
                            var labels = document.querySelectorAll('.items_container label');
                            labels.forEach(function(label) {
                                if ((label.innerText||'').toLowerCase().includes(text.toLowerCase())) {
                                    var checkboxId = label.getAttribute('for');
                                    var checkbox = document.getElementById(checkboxId);
                                    if (checkbox && checkbox.type === 'checkbox') {
                                        if (!checkbox.checked){ checkbox.checked = true; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
                                    }
                                }
                            });
                        };

                        var selectByLabelTexts = function(texts) { texts.forEach(function(text){ selectByLabelText(text); }); };

                        // Unselect helpers
                        var unselectByLabelPattern = function(pattern){
                            var labels = document.querySelectorAll('.items_container label');
                            labels.forEach(function(label){
                                var text = (label.innerText||'').toLowerCase();
                                if (pattern.test(text)){
                                    var checkbox = document.getElementById(label.getAttribute('for'));
                                    if (checkbox && checkbox.type==='checkbox') { if (checkbox.checked){ checkbox.checked = false; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} } }
                                }
                            });
                        };
                        var unselectByLabelPatterns = function(patterns){ patterns.forEach(function(p){ unselectByLabelPattern(p); }); };
                        var unselectByLabelText = function(text) {
                            var labels = document.querySelectorAll('.items_container label');
                            labels.forEach(function(label) {
                                if ((label.innerText||'').toLowerCase().includes(text.toLowerCase())) {
                                    var checkboxId = label.getAttribute('for');
                                    var checkbox = document.getElementById(checkboxId);
                                    if (checkbox && checkbox.type === 'checkbox') { if (checkbox.checked){ checkbox.checked = false; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} } }
                                }
                            });
                        };
                        var unselectByLabelTexts = function(texts) { texts.forEach(function(text){ unselectByLabelText(text); }); };

                        // ================= Items/Chests Grid View (Main page) =================
                        (function(){
                            try{
                                // State: list | grid
                                var VIEW_KEY = 'promo_items_view_mode';
                                // Локальный кэш режима для быстрых синхронных чтений,
                                // чтобы при загрузке страницы не было мигания «Список» → «Плитка».
                                var __promoViewMode_cache = null;

                                // Сохранение режима — appConfig + локальный кэш/localStorage
                                function saveViewMode(mode){
                                    try{
                                        if (mode !== 'grid' && mode !== 'list') return;
                                        __promoViewMode_cache = mode;
                                        try{ localStorage.setItem(VIEW_KEY, mode); }catch(_){ }
                                        if (window.appConfig && window.appConfig.set){
                                            window.appConfig.set(VIEW_KEY, mode);
                                        }
                                    }catch(e){}
                                }

                                // Синхронная загрузка режима: сначала in-memory/LocalStorage, затем дефолт
                                function loadViewMode(defaultMode){
                                    try{
                                        defaultMode = defaultMode || 'list';
                                        if (__promoViewMode_cache === 'grid' || __promoViewMode_cache === 'list') return __promoViewMode_cache;
                                        try{
                                            var v = localStorage.getItem(VIEW_KEY);
                                            if (v === 'grid' || v === 'list'){
                                                __promoViewMode_cache = v;
                                                return v;
                                            }
                                        }catch(_){ }
                                        return defaultMode;
                                    }catch(_){ return defaultMode || 'list'; }
                                }

                                // Асинхронная загрузка из appConfig; использует синхронное значение как базу
                                function loadViewModeAsync(defaultMode){
                                    try{
                                        defaultMode = loadViewMode(defaultMode || 'list');
                                        if (window.appConfig && window.appConfig.get){
                                            return window.appConfig.get(VIEW_KEY, defaultMode).then(function(v){
                                                try{
                                                    if (v === 'grid' || v === 'list'){
                                                        __promoViewMode_cache = v;
                                                        try{ localStorage.setItem(VIEW_KEY, v); }catch(__){}
                                                        return v;
                                                    }
                                                    return defaultMode;
                                                }catch(__){ return defaultMode; }
                                            }).catch(function(){ return defaultMode; });
                                        }
                                        return Promise.resolve(defaultMode);
                                    }catch(_){ return Promise.resolve(loadViewMode(defaultMode)); }
                                }

                                // Create composite host once
                                var tableCont = document.querySelector('.items_container');
                                var composite = document.getElementById('promo_items_composite');
                                if (!composite && tableCont && tableCont.parentElement){
                                    composite = document.createElement('div');
                                    composite.id = 'promo_items_composite';
                                    composite.style = 'display:none; padding:8px 6px; display:flex; flex-direction:column; gap:12px;';

                                    // Section: Предметы
                                    var secItems = document.createElement('div');
                                    secItems.id = 'promo_items_section_items';
                                    secItems.style = 'display:flex; flex-direction:column; gap:8px;';
                                    var secItemsTitle = document.createElement('div');
                                    secItemsTitle.textContent = 'Предметы';
                                    secItemsTitle.title = 'Отображение всех предметов с возможностью выбора';
                                    secItemsTitle.style = 'color:#2c4a8d; font-weight:700;';
                                    var gridHost = document.createElement('div');
                                    gridHost.id = 'promo_items_grid';
                                    gridHost.style = 'padding:4px 0;';
                                    secItems.appendChild(secItemsTitle);
                                    secItems.appendChild(gridHost);

                                    // Section: Сундуки
                                    var secChests = document.createElement('div');
                                    secChests.id = 'promo_items_section_chests';
                                    secChests.style = 'display:flex; flex-direction:column; gap:8px;';
                                    var secChestsTitle = document.createElement('div');
                                    secChestsTitle.textContent = 'Сундуки';
                                    secChestsTitle.title = 'Кликабельные сундуки — переход по ссылке активации';
                                    secChestsTitle.style = 'color:#2c4a8d; font-weight:700;';
                                    var chestsHost = document.createElement('div');
                                    chestsHost.id = 'promo_chests_grid';
                                    chestsHost.style = 'padding:4px 0;';
                                    secChests.appendChild(secChestsTitle);
                                    secChests.appendChild(chestsHost);

                                    composite.appendChild(secItems);
                                    composite.appendChild(secChests);

                                    // place right after the table container
                                    tableCont.parentElement.insertBefore(composite, tableCont.nextSibling);
                                }

                                // Tooltip (single)
                                var tip = document.getElementById('promo_items_grid_tooltip');
                                if (!tip){
                                    tip = document.createElement('div');
                                    tip.id = 'promo_items_grid_tooltip';
                                    tip.style = [
                                        'position: fixed',
                                        'z-index: 2147483647',
                                        'pointer-events: none',
                                        'display: none',
                                        'max-width: 360px',
                                        'background: #ffffff',
                                        'color: #222',
                                        'border: 1px solid #E2D8C9',
                                        'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
                                        'border-radius: 8px',
                                        'padding: 8px 10px',
                                        'font-family: Arial, sans-serif',
                                        'font-size: 13px',
                                        'line-height: 1.35'
                                    ].join(';');
                                    document.body.appendChild(tip);
                                }

                                // Build maps from table
                                var __MS_PER_DAY = 24 * 60 * 60 * 1000;
                                var __itemsMap = {};
                                // Режим сортировки плиток в гриде предметов: default | name | expiry
                                // По умолчанию сортируем по дате сгорания
                                var __promoItemsSortMode = 'expiry';
                                // Признак группировки по типам
                                var __promoItemsGroupByType = true;

                                // Вспомогательная функция: вытащить из текста ровно фрагмент даты/времени
                                function extractExpiryText(text){
                                    try{
                                        if (!text) return null;
                                        text = (text || '').toString();
                                        text = text.replace(/\s+/g, ' ').trim();
                                        // Ищем фрагмент даты формата 31.12.2025 или 31.12.25 23:59
                                        var m = text.match(/(\d{1,2}\.\d{1,2}\.\d{2,4}(?:\s+\d{1,2}:\d{2})?)/);
                                        if (!m) return null;
                                        return m[1];
                                    } catch(_) { return null; }
                                }

                                function parseExpiryDate(text){
                                    try{
                                        var main = extractExpiryText(text);
                                        if (!main) return null;
                                        var parts = main.split(' ');
                                        var d = parts[0].split('.');
                                        var day = parseInt(d[0], 10) || 0;
                                        var month = parseInt(d[1], 10) || 0;
                                        var year = parseInt(d[2], 10) || 0;
                                        if (year < 100) year += 2000;
                                        var hh = 0, mm = 0;
                                        if (parts.length > 1){
                                            var t = parts[1].split(':');
                                            hh = parseInt(t[0], 10) || 0;
                                            mm = parseInt(t[1], 10) || 0;
                                        }
                                        if (!day || !month || !year) return null;
                                        var dt = new Date(year, month - 1, day, hh, mm, 0, 0);
                                        if (isNaN(dt.getTime())) return null;
                                        return dt.getTime();
                                    } catch(_) { return null; }
                                }

                                function rebuildItemsMap(){
                                    try{
                                        __itemsMap = {};
                                        var rows = document.querySelectorAll('.items_container tr');
                                        rows.forEach(function(tr){
                                            try{
                                                var label = tr.querySelector('.item_input_block label');
                                                var input = tr.querySelector('.item_input_block input[type=checkbox]');
                                                if (!label || !input) return;
                                                var nameRaw = (label.innerText||'').trim();
                                                nameRaw = nameRaw.replace(/\(до [^)]+\)/g, '').replace(/\s+/g,' ').trim();
                                                var img = tr.querySelector('.img_item_cell img');
                                                var src = img ? img.getAttribute('src') : '';
                                                if (src && src.startsWith('//')) src = window.location.protocol + src;
                                                var descSpan = tr.querySelector('.img_item_cont span');
                                                var desc = '';
                                                var descPlain = '';
                                                if (descSpan){
                                                    try{
                                                        // HTML используется для тултипа, plain-text — для парсинга типа
                                                        desc = (descSpan.innerHTML || descSpan.innerText || '').toString();
                                                    }catch(_){ desc=''; }
                                                    try{
                                                        descPlain = (descSpan.innerText || descSpan.textContent || '').toString();
                                                    }catch(_){ descPlain = ''; }
                                                }

                                                // Тип предмета из описания (строка вида "Тип: Расходник")
                                                var itemType = '';
                                                try{
                                                    var srcText = descPlain || desc;
                                                    if (srcText){
                                                        // режем только по строке с "Тип:" (до перевода строки)
                                                        // пример: "Описание: ...\nТип: Расходник\nПривязка: Да"
                                                        var mLine = srcText.match(/Тип\s*:[^\n\r]*/i);
                                                        var line = mLine ? mLine[0] : '';
                                                        if (line){
                                                            var mType = line.match(/Тип\s*:\s*(.+)$/i);
                                                            if (mType && mType[1]){
                                                                itemType = (mType[1]||'').replace(/\s+/g,' ').trim();
                                                            }
                                                        }
                                                    }
                                                }catch(__){ itemType = ''; }

                                                var expiryMs = null;
                                                var expiryText = null;
                                                try{
                                                    var dateNode = tr.querySelector('.item_input_block .date_end') || tr.querySelector('.date_end');
                                                    if (dateNode){
                                                        var dtText = (dateNode.textContent || dateNode.innerText || '').trim();
                                                        expiryMs = parseExpiryDate(dtText);
                                                        expiryText = extractExpiryText(dtText);
                                                    }
                                                }catch(_){ expiryMs = null; expiryText = null; }

                                                var idx = Object.keys(__itemsMap).length;
                                                __itemsMap[input.id] = {
                                                    id: input.id,
                                                    cb: input,
                                                    name: nameRaw,
                                                    img: src,
                                                    desc: desc,
                                                    expiryMs: expiryMs,
                                                    expiryText: expiryText,
                                                    type: itemType,
                                                    index: idx
                                                };
                                            }catch(ex){}
                                        });
                                    }catch(e){}
                                }

                                var __chestsMap = {};
                                function rebuildChestsMap(){
                                    try{
                                        __chestsMap = {};
                                        var rows = document.querySelectorAll('.items_container tr');
                                        rows.forEach(function(tr, idx){
                                            try{
                                                var label = tr.querySelector('.chest_input_block label');
                                                var link = tr.querySelector('.chest_input_block a.chest_activate_red');
                                                if (!label || !link) return;
                                                var nameRaw = (label.innerText||'').trim();
                                                nameRaw = nameRaw.replace(/\(до [^)]+\)/g, '').replace(/\s+/g,' ').trim();
                                                var img = tr.querySelector('.img_item_cell img');
                                                var src = img ? img.getAttribute('src') : '';
                                                if (src && src.startsWith('//')) src = window.location.protocol + src;
                                                var descSpan = tr.querySelector('.img_item_cont span');
                                                var desc = '';
                                                if (descSpan){
                                                    try{ desc = (descSpan.innerHTML || descSpan.innerText || '').toString(); }catch(_){ desc=''; }
                                                }
                                                var href = link.getAttribute('href') || '';
                                                // make absolute relative URLs
                                                try{ if (href && href.startsWith('/')) href = location.origin + href; }catch(_){ }
                                                var id = label.getAttribute('for') || ('chest_'+idx);

                                                var expiryMsChest = null;
                                                var expiryTextChest = null;
                                                try{
                                                    var dateNodeChest = tr.querySelector('.chest_input_block .date_end') || tr.querySelector('.date_end');
                                                    if (dateNodeChest){
                                                        var dtTextChest = (dateNodeChest.textContent || dateNodeChest.innerText || '').trim();
                                                        expiryMsChest = parseExpiryDate(dtTextChest);
                                                        expiryTextChest = extractExpiryText(dtTextChest);
                                                    }
                                                }catch(_){ expiryMsChest = null; expiryTextChest = null; }

                                                __chestsMap[id] = { id: id, name: nameRaw, img: src, desc: desc, href: href, expiryMs: expiryMsChest, expiryText: expiryTextChest };
                                            }catch(ex){}
                                        });
                                    }catch(e){}
                                }

                                // Render grid
                                var renderTimer = null;
                                function scheduleGridRender(delay){ try{ if (renderTimer){ clearTimeout(renderTimer); renderTimer=null; } renderTimer = setTimeout(function(){ renderItems(); renderChests(); }, typeof delay==='number'? delay: 30); }catch(e){} }

                                function renderItems(){
                                    try{
                                        var gridHost = document.getElementById('promo_items_grid');
                                        if (!gridHost) return;
                                        gridHost.innerHTML = '';

                                        var all = [];
                                        Object.keys(__itemsMap).forEach(function(id){ var m=__itemsMap[id]; if (m && m.cb) all.push(m); });
                                        if (!all.length) return;

                                        var itemSize = 56; var cropSize = 30; var cropX = 45, cropY = 25;
                                        var nowTs = Date.now();

                                        function makeBlock(m){
                                            var block = document.createElement('div');
                                            block.className = 'promo_grid_item';
                                            block.style = [
                                                'width:'+itemSize+'px','height:'+itemSize+'px','border-radius:10px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#ffffffCC','display:flex','align-items:center','justify-content:center','cursor:pointer','position:relative',
                                                (m.cb && m.cb.checked ? 'outline:2px solid #2c4a8d; outline-offset:-2px;' : 'outline:2px solid transparent; outline-offset:-2px;')
                                            ].join(';');

                                            // Подбор фона по дате сгорания
                                            try {
                                                var bg = '#ffffffCC';
                                                if (typeof m.expiryMs === 'number'){
                                                    var diffDays = (m.expiryMs - nowTs) / __MS_PER_DAY;
                                                    if (diffDays < 0) diffDays = 0;
                                                    if (diffDays < 2){
                                                        // менее 2 суток — красный
                                                        bg = '#ffcccc';
                                                    } else if (diffDays < 5){
                                                        // менее 5 суток — жёлтый
                                                        bg = '#fff6cc';
                                                    }
                                                }
                                                block.style.background = bg;
                                            } catch(_){ }

                                            var crop = document.createElement('div');
                                            crop.style = [ 'width:'+cropSize+'px','height:'+cropSize+'px','overflow:hidden','border-radius:6px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#fff','position:relative' ].join(';');
                                            var img = document.createElement('img'); img.src = m.img || ''; img.alt = m.name; img.title = '';
                                            img.style = [ 'position:absolute','left:-'+cropX+'px','top:-'+cropY+'px','width:auto','height:auto','max-width:none','max-height:none','image-rendering:auto' ].join(';');
                                            crop.appendChild(img); block.appendChild(crop);

                                            // tooltip handlers
                                            block.addEventListener('mouseenter', function(e){
                                                try{
                                                    var html = '<div style="font-weight:700; margin-bottom:4px;">'+(m.name||'')+'</div>' + ((m.desc||'').toString());
                                                    if (m.expiryText){
                                                        html += '<div style="margin-top:4px; color:#8b0000;">Сгорит: '+m.expiryText+'</div>';
                                                    }
                                                    tip.innerHTML = html;
                                                    tip.style.display='block';
                                                }catch(_){ }
                                            });
                                            block.addEventListener('mousemove', function(e){ try{ var x=e.clientX+14, y=e.clientY+14; var w=tip.offsetWidth||320, h=tip.offsetHeight||60; if (x+w>window.innerWidth-8) x = e.clientX - w - 10; if (y+h>window.innerHeight-8) y = e.clientY - h - 10; tip.style.left=x+'px'; tip.style.top=y+'px'; }catch(_){} });
                                            block.addEventListener('mouseleave', function(){ try{ tip.style.display='none'; }catch(_){} });

                                            // toggle on click
                                            block.addEventListener('click', function(){ try{ if (m && m.cb){ m.cb.checked = !m.cb.checked; try{ m.cb.dispatchEvent(new Event('change', { bubbles: true })); }catch(__){} // update highlight immediately
                                                if (m.cb.checked){ block.style.outline = '2px solid #2c4a8d'; block.style.outlineOffset='-2px'; } else { block.style.outline='2px solid transparent'; block.style.outlineOffset='-2px'; } } }catch(ex){} });

                                            return block;
                                        }

                                        // Подготовка групп (по типам или одна общая группа)
                                        var groups = {};
                                        var order = [];
                                        if (__promoItemsGroupByType){
                                            all.forEach(function(m){
                                                var key = (m.type || '').toString();
                                                if (!key) key = 'Прочее';
                                                if (!groups[key]){ groups[key] = []; order.push(key); }
                                                groups[key].push(m);
                                            });
                                        } else {
                                            var keyAll = 'Все предметы';
                                            groups[keyAll] = all.slice(0);
                                            order.push(keyAll);
                                        }

                                        function compareItems(a,b){
                                            try{
                                                if (__promoItemsSortMode === 'name'){
                                                    var an = (a.name||'').toLowerCase();
                                                    var bn = (b.name||'').toLowerCase();
                                                    if (an < bn) return -1;
                                                    if (an > bn) return 1;
                                                    return (a.index||0) - (b.index||0);
                                                }
                                                if (__promoItemsSortMode === 'expiry'){
                                                    // Сначала по "зоне" истечения (красный/жёлтый/белый/без даты), затем по названию
                                                    function bucket(m){
                                                        if (typeof m.expiryMs !== 'number' || !m.expiryMs) return 3; // без даты — последними
                                                        var diffDays = (m.expiryMs - nowTs) / __MS_PER_DAY;
                                                        if (diffDays < 0) diffDays = 0;
                                                        if (diffDays < 2) return 0; // красный
                                                        if (diffDays < 5) return 1; // жёлтый
                                                        return 2; // белый (остальное)
                                                    }

                                                    var ba = bucket(a);
                                                    var bb = bucket(b);
                                                    if (ba !== bb) return ba - bb;

                                                    // внутри одной зоны сортируем по названию
                                                    var aen = (a.name||'').toLowerCase();
                                                    var ben = (b.name||'').toLowerCase();
                                                    if (aen < ben) return -1;
                                                    if (aen > ben) return 1;

                                                    return (a.index||0) - (b.index||0);
                                                }
                                            }catch(_){ }
                                            // По умолчанию — как на сайте (исходный порядок)
                                            return (a.index||0) - (b.index||0);
                                        }

                                        // Корневая обёртка: панель сортировки/группировки + контейнер для групп
                                        var root = document.createElement('div');
                                        root.style = 'display:flex; flex-direction:column; gap:6px;';
                                        gridHost.appendChild(root);

                                        // Панель сортировки и группировки
                                        try{
                                            var sortBar = document.createElement('div');
                                            sortBar.style = 'display:flex; flex-wrap:wrap; align-items:center; gap:8px; font-size:12px; color:#444;';

                                            // Блок сортировки
                                            var sortLabel = document.createElement('span');
                                            sortLabel.textContent = 'Сортировка:';
                                            sortBar.appendChild(sortLabel);

                                            function mkSortBtn(text, mode){
                                                var b = document.createElement('button');
                                                b.type = 'button';
                                                b.textContent = text;
                                                // Визуальная индикация активного режима сортировки
                                                if (mode === __promoItemsSortMode){
                                                    b.style = 'padding:2px 6px; border-radius:4px; border:1px solid #2c4a8d; background:#e0ecff; cursor:pointer; font-size:12px; font-weight:600;';
                                                } else {
                                                    b.style = 'padding:2px 6px; border-radius:4px; border:1px solid #C6B9A4; background:#f9f5ee; cursor:pointer; font-size:12px;';
                                                }
                                                b.addEventListener('click', function(){ try{ __promoItemsSortMode = mode; renderItems(); }catch(_){ } });
                                                return b;
                                            }

                                            sortBar.appendChild(mkSortBtn('как на сайте', 'default'));
                                            sortBar.appendChild(mkSortBtn('по названию', 'name'));
                                            sortBar.appendChild(mkSortBtn('по дате сгорания', 'expiry'));

                                            // Разделитель
                                            var sep = document.createElement('span');
                                            sep.textContent = '·';
                                            sep.style = 'opacity:0.6;';
                                            sortBar.appendChild(sep);

                                            // Переключатель группировки по типу
                                            var groupLabel = document.createElement('label');
                                            groupLabel.style = 'display:flex; align-items:center; gap:4px; cursor:pointer;';
                                            var groupChk = document.createElement('input');
                                            groupChk.type = 'checkbox';
                                            groupChk.checked = !!__promoItemsGroupByType;
                                            groupChk.addEventListener('change', function(){
                                                try{
                                                    __promoItemsGroupByType = !!groupChk.checked;
                                                    renderItems();
                                                }catch(_){ }
                                            });
                                            var groupText = document.createElement('span');
                                            groupText.textContent = 'Группировать по типу';
                                            groupLabel.appendChild(groupChk);
                                            groupLabel.appendChild(groupText);
                                            sortBar.appendChild(groupLabel);

                                            root.appendChild(sortBar);
                                        }catch(_){ }

                                        // Контейнер для групп
                                        var groupsHost = document.createElement('div');
                                        groupsHost.style = 'display:flex; flex-direction:column; gap:4px;';
                                        root.appendChild(groupsHost);

                                        // Рендер групп
                                        order.forEach(function(key){
                                            var items = groups[key] || [];
                                            if (!items.length) return;

                                            // Заголовок группы + быстрые действия для группы
                                            var headerRow = document.createElement('div');
                                            headerRow.style = 'width:100%; display:flex; align-items:center; justify-content:space-between; margin:4px 0 2px 2px; gap:4px;';

                                            var header = document.createElement('div');
                                            header.textContent = key;
                                            header.style = 'font-weight:700; font-size:13px; color:#2c4a8d;';
                                            headerRow.appendChild(header);

                                            // Кнопки "Выбрать все" / "Убрать все" для группы
                                            var groupActions = document.createElement('div');
                                            // небольшой отступ справа, чтобы не наезжать на разметку
                                            groupActions.style = 'display:flex; gap:4px; margin-right:12px;';

                                            function mkGroupBtn(text, action){
                                                var b = document.createElement('button');
                                                b.type = 'button';
                                                b.textContent = text;
                                                b.style = 'padding:1px 5px; border-radius:4px; border:1px solid #C6B9A4; background:#fdf9f1; cursor:pointer; font-size:11px;';
                                                b.addEventListener('click', function(){
                                                    try{
                                                        items.forEach(function(m){
                                                            if (!m || !m.cb) return;
                                                            var desired = action === 'select';
                                                            if (m.cb.checked !== desired){
                                                                m.cb.checked = desired;
                                                                try{ m.cb.dispatchEvent(new Event('change', { bubbles:true })); }catch(_){ }
                                                            }
                                                        });
                                                    }catch(_){ }
                                                    // после изменения чекбоксов грид перерисуется через scheduleGridRender
                                                });
                                                return b;
                                            }

                                            groupActions.appendChild(mkGroupBtn('Выбрать все', 'select'));
                                            groupActions.appendChild(mkGroupBtn('Убрать все', 'unselect'));
                                            headerRow.appendChild(groupActions);

                                            groupsHost.appendChild(headerRow);

                                            var wrap = document.createElement('div');
                                            wrap.style = 'display:flex; flex-wrap:wrap; gap:8px; align-content:flex-start;';
                                            groupsHost.appendChild(wrap);

                                            try{ items.sort(compareItems); }catch(_){ }
                                            items.forEach(function(m){
                                                var blk = makeBlock(m);
                                                wrap.appendChild(blk);
                                            });
                                        });
                                    }catch(e){}
                                }

                                function renderChests(){
                                    try{
                                        var chestsHost = document.getElementById('promo_chests_grid');
                                        if (!chestsHost) return;
                                        chestsHost.innerHTML = '';
                                        var items = []; Object.keys(__chestsMap).forEach(function(id){ var m=__chestsMap[id]; if (m) items.push(m); });
                                        if (items.length === 0){
                                            // если сундуков нет — скрываем секцию заголовка
                                            var sec = document.getElementById('promo_items_section_chests');
                                            if (sec) sec.style.display = 'none';
                                            return;
                                        } else {
                                            var sec = document.getElementById('promo_items_section_chests');
                                            if (sec) sec.style.display = 'flex';
                                        }
                                        items.sort(function(a,b){ return a.name.localeCompare(b.name, 'ru'); });
                                        var wrap = document.createElement('div');
                                        wrap.style = 'display:flex; flex-wrap:wrap; gap:8px; align-content:flex-start;';
                                        chestsHost.appendChild(wrap);

                                        var itemSize = 56; var cropSize = 30; var cropX = 45, cropY = 25;
                                        var nowTsChests = Date.now();
                                        items.forEach(function(m){
                                            var block = document.createElement('a');
                                            block.className = 'promo_chest_item';
                                            block.href = m.href || '#';
                                            block.style = [
                                                'width:'+itemSize+'px','height:'+itemSize+'px','border-radius:10px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#ffffffCC','display:flex','align-items:center','justify-content:center','cursor:pointer','position:relative','text-decoration:none'
                                            ].join(';');

                                            // Подбор фона по дате сгорания (для сундуков)
                                            try {
                                                var bgChest = '#ffffffCC';
                                                if (typeof m.expiryMs === 'number'){
                                                    var diffDaysChest = (m.expiryMs - nowTsChests) / __MS_PER_DAY;
                                                    if (diffDaysChest < 0) diffDaysChest = 0;
                                                    if (diffDaysChest < 2){
                                                        bgChest = '#ffcccc';
                                                    } else if (diffDaysChest < 5){
                                                        bgChest = '#fff6cc';
                                                    }
                                                }
                                                block.style.background = bgChest;
                                            } catch(_) { }

                                            var crop = document.createElement('div');
                                            crop.style = [ 'width:'+cropSize+'px','height:'+cropSize+'px','overflow:hidden','border-radius:6px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#fff','position:relative' ].join(';');
                                            var img = document.createElement('img'); img.src = m.img || ''; img.alt = m.name; img.title = '';
                                            img.style = [ 'position:absolute','left:-'+cropX+'px','top:-'+cropY+'px','width:auto','height:auto','max-width:none','max-height:none','image-rendering:auto' ].join(';');
                                            crop.appendChild(img); block.appendChild(crop);

                                            // tooltip handlers (on anchor)
                                            block.addEventListener('mouseenter', function(e){
                                                try{
                                                    var html = '<div style="font-weight:700; margin-bottom:4px;">'+(m.name||'')+'</div>' + ((m.desc||'').toString());
                                                    if (m.expiryText){
                                                        html += '<div style="margin-top:4px; color:#8b0000;">Сгорит: '+m.expiryText+'</div>';
                                                    }
                                                    tip.innerHTML = html;
                                                    tip.style.display='block';
                                                }catch(_){ }
                                            });
                                            block.addEventListener('mousemove', function(e){ try{ var x=e.clientX+14, y=e.clientY+14; var w=tip.offsetWidth||320, h=tip.offsetHeight||60; if (x+w>window.innerWidth-8) x = e.clientX - w - 10; if (y+h>window.innerHeight-8) y = e.clientY - h - 10; tip.style.left=x+'px'; tip.style.top=y+'px'; }catch(_){} });
                                            block.addEventListener('mouseleave', function(){ try{ tip.style.display='none'; }catch(_){} });

                                            // open link on click (default anchor behavior). Ensure same tab
                                            block.addEventListener('click', function(e){ try{ if (block.href){ e.preventDefault(); window.location.href = block.href; } }catch(_){} });
                                            wrap.appendChild(block);
                                        });
                                    }catch(e){}
                                }

                                // React to checkbox changes
                                if (tableCont && !tableCont.getAttribute('data-grid-listener')){
                                    tableCont.setAttribute('data-grid-listener','1');
                                    tableCont.addEventListener('change', function(e){ try{ scheduleGridRender(0); }catch(_){} });
                                }

                                // Expose scheduler for bulk actions
                                window.__promoGrid_schedule = scheduleGridRender;

                                // Apply mode show/hide
                                function applyViewMode(mode){
                                    try{
                                        mode = mode || loadViewMode();
                                        var gridHost = document.getElementById('promo_items_grid');
                                        var chestsHost = document.getElementById('promo_chests_grid');
                                        var composite = document.getElementById('promo_items_composite');
                                        if (!tableCont || !composite) return;
                                        if (mode === 'grid'){
                                            // build map and render once visible
                                            rebuildItemsMap();
                                            rebuildChestsMap();
                                            scheduleGridRender(0);
                                            composite.style.display = 'block';
                                            tableCont.style.display = 'none';
                                        } else {
                                            if (composite) composite.style.display = 'none';
                                            tableCont.style.display = '';
                                        }
                                    }catch(e){}
                                }
                                window.__promoGrid_applyMode = applyViewMode;

                                // Init now (respect stored mode) без лишнего мигания
                                var initialMode = loadViewMode('list');
                                applyViewMode(initialMode);

                                // Also re-map after small delay to ensure images/DOM ready
                                setTimeout(function(){ try{ rebuildItemsMap(); rebuildChestsMap(); scheduleGridRender(0); }catch(_){ } }, 200);
                    }catch(e){}
                })();

                        // ================= UI: View mode toggle row =================
                        (function(){
                            try{
                                var row = document.createElement('div'); row.style = rowCss;
                                var label = document.createElement('div'); label.textContent = 'Режим отображения:'; label.style = 'min-width:max-content; color:#2c4a8d; font-weight:700;';
                                var btnList = document.createElement('button'); btnList.id='viewModeListBtn'; btnList.textContent='Список'; btnList.style=pillBtnCss;
                                var btnGrid = document.createElement('button'); btnGrid.id='viewModeGridBtn'; btnGrid.textContent='Плитка'; btnGrid.style=pillBtnCss;

                                function setActive(mode){ try{ if (mode==='grid'){ btnGrid.style.backgroundColor='#C5B0AE'; btnList.style.backgroundColor='#D2C0BE'; } else { btnList.style.backgroundColor='#C5B0AE'; btnGrid.style.backgroundColor='#D2C0BE'; } }catch(_){ } }
                                async function save(mode){ try{ if (window.appConfig && window.appConfig.set) { await window.appConfig.set('promo_items_view_mode', mode); } else { try{ localStorage.setItem('promo_items_view_mode', mode); }catch(__){} } }catch(_){ } }
                                async function load(){ try{ if (window.appConfig && window.appConfig.get){ var v = await window.appConfig.get('promo_items_view_mode', 'list'); return v||'list'; } try{ return localStorage.getItem('promo_items_view_mode')||'list'; }catch(__){ return 'list'; } }catch(_){ return 'list'; } }

                                btnList.addEventListener('click', function(){ try{ save('list'); setActive('list'); if (window.__promoGrid_applyMode) window.__promoGrid_applyMode('list'); }catch(_){ } });
                                btnGrid.addEventListener('click', function(){ try{ save('grid'); setActive('grid'); if (window.__promoGrid_applyMode) window.__promoGrid_applyMode('grid'); }catch(_){ } });

                                // Инициализация: сначала дефолт, затем подгрузка из конфига
                                setActive('list');
                                try{ load().then(function(mode){ try{ setActive(mode); if (window.__promoGrid_applyMode) window.__promoGrid_applyMode(mode); }catch(__){} }); }catch(__){}
                                row.appendChild(label); row.appendChild(btnList); row.appendChild(btnGrid);
                                buttonContainer.appendChild(row);
                            }catch(e){}
                        })();

                        // ================= Selected Items Popup =================
                        (function(){
                            try{
                                // Отключение всплывающего окна «Предметы» (Selected Items Popup)
                                // Требование: удалить именно попап «Предметы». Оставляем логику остальной страницы нетронутой.
                                // Для совместимости с потенциальными внешними обращениями объявляем no-op хелперы и выходим.
                                // Дополнительно: сохраняем/читаем глобальный ключ 'promo_popup_compact' через appConfig, чтобы состояние
                                // (размер/позиция/свернутость) при необходимости могло быть использовано другими частями.
                                try {
                                    window.__promoPopup_saveCompact = function(state){ try{ if (window.appConfig && window.appConfig.set){ window.appConfig.set('promo_popup_compact', state||{}); } }catch(__){} };
                                    window.__promoPopup_loadCompact = function(def){ try{ def = def||{}; if (window.appConfig && window.appConfig.get){ return window.appConfig.get('promo_popup_compact', def); } return Promise.resolve(def); }catch(__){ return Promise.resolve(def||{}); } };
                                } catch(__){}
                                window.__promoSelected_rebuildMap = function(){};
                                window.__promoSelected_refresh = function(){};
                                window.__promoSelected_schedule = function(){};
                                return; // прерываем выполнение блока, не создаём DOM попапа
                                // State helpers for the selected popup
                                function saveSelState(st){ try{ localStorage.setItem('promo_selected_popup_state', JSON.stringify(st)); }catch(e){} }
                                function loadSelState(){ try{ var s = localStorage.getItem('promo_selected_popup_state'); return s? JSON.parse(s): {}; }catch(e){ return {}; } }

                                // Create popup if missing
                                var sp = document.getElementById('promo_selected_popup');
                                var sHeader, sContent, sCompact, sTooltip, sResizer;
                                function recalcSelContentHeight(){ try{ if (!sp||!sContent||!sHeader) return; var h = sHeader.offsetHeight; var total = sp.getBoundingClientRect().height; var contentH = Math.max(0, total - h); sContent.style.maxHeight = contentH+'px'; sContent.style.height = contentH+'px'; }catch(e){} }
                                function setSelCollapsed(c){ try{ var st=loadSelState(); st.collapsed=!!c; if (!!c){
                                        // hide header/content/resizer, show compact with count
                                        if (sHeader) sHeader.style.display='none'; if (sContent) sContent.style.display='none'; if (sResizer) sResizer.style.display='none'; if (sCompact) { sCompact.style.display='inline-block'; }
                                        // shrink container and hide frame
                                        sp.style.width='auto'; sp.style.height='auto'; sp.style.minWidth='0'; sp.style.minHeight='0';
                                        
                                    } else {
                                        if (sCompact) sCompact.style.display='none'; if (sHeader) sHeader.style.display='flex'; if (sContent) sContent.style.display='block'; if (sResizer) sResizer.style.display='block';
                                        // restore frame look
                                        
                                        sp.style.minWidth='220px'; sp.style.minHeight='120px';
                                        // restore saved size
                                        if (st.width) sp.style.width = st.width + 'px'; if (st.height) sp.style.height = st.height + 'px';
                                        recalcSelContentHeight();
                                    }
                                    var t = (sHeader? sHeader.querySelector('button') : null); if (t) t.innerText = st.collapsed? '+':'−'; saveSelState(st); }catch(e){} }

                                if (!sp){
                                    sp = document.createElement('div'); sp.id='promo_selected_popup'; sp.style = [
                                        'position: fixed',
                                        'left: 16px',
                
                                        'z-index: 2147483646',
                                        'background: #F6F1E7',
                                        'border: 1px solid #E2D8C9',
                                        'border-radius: 12px',
                                        'box-shadow: 0 8px 24px rgba(0,0,0,0.2)',
                                        'overflow: hidden',
                                        'color: #333',
                                        'font-family: Arial, sans-serif',
                                        'width: 320px',
                                        'min-width: 220px',
                                        'max-width: 40vw',
                                        'min-height: 120px',
                                        'max-height: 60vh'
                                    ].join(';');

                                    sHeader = document.createElement('div');
                                    sHeader.style = 'display:flex;align-items:center;justify-content:space-between;padding:8px 12px;background:#EDE4D6;border-bottom:1px solid #E2D8C9;cursor:move;user-select:none;';
                                    var hTitle = document.createElement('div'); hTitle.textContent = 'Предметы'; hTitle.style='font-weight:700;color:#2c4a8d;';
                                    var tgl = document.createElement('button'); tgl.innerText='−'; tgl.title='Свернуть'; tgl.style='border:none;background:#D2C0BE;color:#333;border-radius:16px;padding:2px 8px;cursor:pointer;';

                                    sContent = document.createElement('div'); sContent.id='promo_selected_list_wrap'; sContent.style='padding:8px;overflow:auto;';
                                    var list = document.createElement('div'); list.id='promo_selected_list'; list.style='display:flex;flex-wrap:wrap;gap:8px;align-content:flex-start;'; sContent.appendChild(list);

                                    // compact pill
                                    sCompact = document.createElement('div'); 
                                    sCompact.id='promo_selected_compact';
                                    sCompact.style = 'display:none; margin:6px; padding:6px 12px; background:#EDE4D6; color:#2c4a8d; font-weight:700; border-radius:16px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;'; 
                                    sCompact.title='Развернуть окно «Предметы»'; sCompact.textContent='Предметы';

                                    // resizer
                                    sResizer = document.createElement('div'); sResizer.id='promo_selected_resizer'; sResizer.style='position:absolute;width:14px;height:14px;right:2px;bottom:2px;cursor:nwse-resize;background:transparent;';
                                    sResizer.innerHTML = '<svg width="14" height="14" viewBox="0 0 14 14" style="display:block"><path d="M2 12 L12 2 L12 12 Z" fill="#00000022"/></svg>';

                                    // tooltip (single instance)
                                    sTooltip = document.createElement('div');
                                    sTooltip.id = 'promo_selected_tooltip';
                                    sTooltip.style = [
                                        'position: fixed',
                                        'z-index: 2147483647',
                                        'pointer-events: none',
                                        'display: none',
                                        'max-width: 360px',
                                        'background: #ffffff',
                                        'color: #222',
                                        'border: 1px solid #E2D8C9',
                                        'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
                                        'border-radius: 8px',
                                        'padding: 8px 10px',
                                        'font-family: Arial, sans-serif',
                                        'font-size: 13px',
                                        'line-height: 1.35'
                                    ].join(';');

                                    sHeader.appendChild(hTitle); sHeader.appendChild(tgl);
                                    sp.appendChild(sHeader); sp.appendChild(sContent); sp.appendChild(sResizer); sp.appendChild(sCompact); sp.appendChild(sTooltip);
                                    document.body.appendChild(sp);

                                    tgl.onclick = function(){ var st=loadSelState(); setSelCollapsed(!(st&&st.collapsed)); };
                                    sCompact.addEventListener('click', function(e){ try{ if (sCompact && (sCompact.dataset.dragMoved==='1' || sCompact.dataset.dragJustDragged==='1')){ e.preventDefault(); return false; } setSelCollapsed(false); }catch(ex){} });

                                    // drag
                                    (function(){
                                        var dragging=false, startX=0, startY=0, startLeft=0, startTop=0, moved=false, thr=4;
                                        function onDown(e){ if (e.target && (e.target.tagName==='BUTTON' || e.target.closest('button'))) return; dragging=true; moved=false; if (sHeader){ delete sHeader.dataset.dragMoved; delete sHeader.dataset.dragJustDragged; } if (sCompact){ delete sCompact.dataset.dragMoved; delete sCompact.dataset.dragJustDragged; }
                                            var rect = sp.getBoundingClientRect();
                                            // ensure left/top coordinates (и не привязываем к нижнему краю)
                                            if (!sp.style.left && sp.style.right){ sp.style.left = rect.left + 'px'; sp.style.top = rect.top + 'px'; sp.style.right=''; sp.style.bottom=''; }
                                            startX = e.clientX; startY = e.clientY; startLeft = parseFloat(sp.style.left || rect.left); startTop = parseFloat(sp.style.top || rect.top);
                                            document.addEventListener('mousemove', onMove); document.addEventListener('mouseup', onUp); e.preventDefault(); }
                                        function onMove(e){ if (!dragging) return; var dx=e.clientX-startX, dy=e.clientY-startY; if (!moved && (Math.abs(dx)>thr || Math.abs(dy)>thr)){ moved=true; if (sHeader) sHeader.dataset.dragMoved='1'; if (sCompact) sCompact.dataset.dragMoved='1'; }
                                            var newL=startLeft+dx, newT=startTop+dy; var r=sp.getBoundingClientRect(); var maxL = window.innerWidth - r.width; var maxT = window.innerHeight - r.height; newL = Math.min(Math.max(0,newL), Math.max(0,maxL)); newT = Math.min(Math.max(0,newT), Math.max(0,maxT)); sp.style.left=newL+'px'; sp.style.top=newT+'px'; }
                                        function onUp(){ if (!dragging) return; dragging=false; document.removeEventListener('mousemove', onMove); document.removeEventListener('mouseup', onUp); var st=loadSelState(); var rect=sp.getBoundingClientRect(); st.left=rect.left; st.top=rect.top; saveSelState(st); if (moved){ if (sHeader){ sHeader.dataset.dragJustDragged='1'; setTimeout(function(){ delete sHeader.dataset.dragJustDragged; delete sHeader.dataset.dragMoved; }, 120);} if (sCompact){ sCompact.dataset.dragJustDragged='1'; setTimeout(function(){ delete sCompact.dataset.dragJustDragged; delete sCompact.dataset.dragMoved; }, 120);} } }
                                        if (sHeader) sHeader.addEventListener('mousedown', onDown);
                                        if (sCompact) sCompact.addEventListener('mousedown', onDown);
                                        if (sHeader){ sHeader.addEventListener('click', function(e){ try{ if (e.target && (e.target.tagName==='BUTTON' || e.target.closest('button'))) return; if (sHeader.dataset.dragMoved==='1' || sHeader.dataset.dragJustDragged==='1'){ e.preventDefault(); return false; } var st=loadSelState(); setSelCollapsed(!(st&&st.collapsed)); }catch(ex){} }); }
                                    })();

                                    // restore state
                                    (function(){ var st=loadSelState(); if (st && (st.left!=null && st.top!=null)){ sp.style.left = st.left + 'px'; sp.style.top = st.top + 'px'; sp.style.right=''; sp.style.bottom=''; }
                                        if (st && (st.width||st.height)){ if (st.width) sp.style.width = st.width + 'px'; if (st.height) sp.style.height = st.height + 'px'; }
                                        setSelCollapsed(!!(st&&st.collapsed)); requestAnimationFrame(function(){ recalcSelContentHeight(); }); })();
                                } else {
                                    sHeader = sp.firstElementChild; sContent = document.getElementById('promo_selected_list_wrap'); sCompact = document.getElementById('promo_selected_compact'); sTooltip = document.getElementById('promo_selected_tooltip'); sResizer = document.getElementById('promo_selected_resizer');
                                    // ensure the toggle button style совпадает с «Управление» (без внутренней обводки)
                                    try{ var __tgl = sHeader ? sHeader.querySelector('button') : null; if (__tgl){ __tgl.style.boxShadow = 'none'; } }catch(__e){}
                                    if (!sResizer){ sResizer = document.createElement('div'); sResizer.id='promo_selected_resizer'; sResizer.style='position:absolute;width:14px;height:14px;right:2px;bottom:2px;cursor:nwse-resize;background:transparent;'; sResizer.innerHTML = '<svg width="14" height="14" viewBox="0 0 14 14" style="display:block"><path d="M2 12 L12 2 L12 12 Z" fill="#00000022"/></svg>'; sp.appendChild(sResizer);}            
                                }

                                // resize logic for selected items popup
                                (function(){
                                    if (!sResizer) return;
                                    if (sp.getAttribute('data-sel-resize-inited')==='1') return;
                                    sp.setAttribute('data-sel-resize-inited','1');
                                    var resizing=false, startX=0, startY=0, startW=0, startH=0;
                                    function onDown(e){
                                        resizing = true;
                                        var rect = sp.getBoundingClientRect();
                                        startX = e.clientX; startY = e.clientY;
                                        startW = rect.width; startH = rect.height;
                                        document.addEventListener('mousemove', onMove);
                                        document.addEventListener('mouseup', onUp);
                                        e.preventDefault();
                                    }
                                    function onMove(e){
                                        if (!resizing) return;
                                        var dx = e.clientX - startX;
                                        var dy = e.clientY - startY;
                                        var minW = 220, minH = 120;
                                        var maxW = Math.floor(window.innerWidth * 0.9);
                                        var maxH = Math.floor(window.innerHeight * 0.9);
                                        var newW = Math.min(Math.max(minW, startW + dx), maxW);
                                        var newH = Math.min(Math.max(minH, startH + dy), maxH);
                                        sp.style.width = newW + 'px';
                                        sp.style.height = newH + 'px';
                                        try{ recalcSelContentHeight(); }catch(_){ }
                                    }
                                    function onUp(){
                                        if (!resizing) return;
                                        resizing = false;
                                        document.removeEventListener('mousemove', onMove);
                                        document.removeEventListener('mouseup', onUp);
                                        var rect = sp.getBoundingClientRect();
                                        var st = loadSelState();
                                        st = st || {};
                                        st.width = rect.width; st.height = rect.height;
                                        saveSelState(st);
                                    }
                                    sResizer.addEventListener('mousedown', onDown);
                                    // respond to window resize
                                    window.addEventListener('resize', function(){ try{ recalcSelContentHeight(); }catch(_){ } });
                                })();

                                // Build mapping (id -> meta)
                                var __promoSelMap = {};
                                function rebuildMap(){ try{
                                    __promoSelMap = {};
                                    var rows = document.querySelectorAll('.items_container tr');
                                    rows.forEach(function(tr){
                                        try{
                                            var label = tr.querySelector('.item_input_block label');
                                            var input = tr.querySelector('.item_input_block input[type=checkbox]');
                                            if (!label || !input) return;
                                            var nameRaw = (label.innerText||'').trim();
                                            // remove date span content
                                            nameRaw = nameRaw.replace(/\(до [^)]+\)/g, '').replace(/\s+/g,' ').trim();
                                            var img = tr.querySelector('.img_item_cell img');
                                            var src = img ? img.getAttribute('src') : '';
                                            if (src && src.startsWith('//')) src = window.location.protocol + src;
                                            var descSpan = tr.querySelector('.img_item_cont span');
                                            var desc = '';
                                            if (descSpan){
                                                try { desc = (descSpan.innerText||'').replace(/\s+/g,' ').trim(); } catch(_){ desc=''; }
                                            }
                                            __promoSelMap[input.id] = { id: input.id, cb: input, name: nameRaw, img: src, desc: desc };
                                        }catch(ex){}
                                    });
                                }catch(e){} }

                                // Render list (now: ALL items, highlight selected)
                                var renderTimer = null;
                                function scheduleRender(delay){ try{ if (renderTimer){ clearTimeout(renderTimer); renderTimer=null; } renderTimer = setTimeout(renderSelectedList, typeof delay==='number'? delay: 30); }catch(e){} }
                                function renderSelectedList(){ try{
                                    var list = document.getElementById('promo_selected_list'); if (!list) return; list.innerHTML = '';
                                    // collect ALL items
                                    var items = []; Object.keys(__promoSelMap).forEach(function(id){ var m=__promoSelMap[id]; if (m && m.cb) items.push(m); });
                                    // sort by name
                                    items.sort(function(a,b){ return a.name.localeCompare(b.name, 'ru'); });
                                    // counts
                                    var selectedCount = 0; items.forEach(function(m){ if (m.cb && m.cb.checked) selectedCount++; });
                                    if (items.length===0){ var empty=document.createElement('div'); empty.style='opacity:0.7;padding:6px 4px;'; empty.textContent='Пусто'; list.appendChild(empty); }
                                    // icon block size
                                    var itemSize = 56; // tile size (outer)
                                    var cropSize = 30; // inner crop box 30x30
                                    var cropX = 45, cropY = 25; // top-left source point
                                    items.forEach(function(m){
                                        var block = document.createElement('div');
                                        block.className = 'sel_item_block';
                                        block.style = [
                                            'width:'+itemSize+'px',
                                            'height:'+itemSize+'px',
                                            'border-radius:10px',
                                            'box-shadow: inset 0 0 0 1px #E2D8C9',
                                            'background:#ffffffCC',
                                            'display:flex',
                                            'align-items:center',
                                            'justify-content:center',
                                            'cursor:pointer',
                                            'position:relative',
                                            (m.cb && m.cb.checked ? 'outline:2px solid #2c4a8d; outline-offset:-2px;' : 'outline:2px solid transparent; outline-offset:-2px;')
                                        ].join(';');

                                        // crop box 30x30 showing region X=45,Y=25 of the original 120x80 image
                                        var crop = document.createElement('div');
                                        crop.style = [
                                            'width:'+cropSize+'px',
                                            'height:'+cropSize+'px',
                                            'overflow:hidden',
                                            'border-radius:6px',
                                            'box-shadow: inset 0 0 0 1px #E2D8C9',
                                            'background:#fff',
                                            'position:relative'
                                        ].join(';');

                                        var im = document.createElement('img');
                                        im.src = m.img || '';
                                        im.alt = '';
                                        im.draggable = false;
                                        im.style = [
                                            'position:absolute',
                                            'left:-'+cropX+'px',
                                            'top:-'+cropY+'px',
                                            'image-rendering:auto',
                                            'pointer-events:none'
                                        ].join(';');
                                        crop.appendChild(im);
                                        block.appendChild(crop);

                                        // hover tooltip
                                        block.addEventListener('mouseenter', function(){
                                            try{
                                                if (!sTooltip) return;
                                                var nameHtml = '<div style="font-weight:700;color:#2c4a8d;margin-bottom:4px;">'+ (m.name||'') +'</div>';
                                                var descHtml = '';
                                                if (m.desc){ descHtml = '<div style="white-space:normal;color:#333;">'+ m.desc.replace(/\n/g,'<br>') +'</div>'; }
                                                sTooltip.innerHTML = nameHtml + descHtml;
                                                sTooltip.style.display = 'block';
                                            }catch(e){}
                                        });
                                        block.addEventListener('mousemove', function(ev){
                                            try{
                                                if (!sTooltip || sTooltip.style.display==='none') return;
                                                var pad = 12; var ttW = sTooltip.offsetWidth||320; var ttH = sTooltip.offsetHeight||80;
                                                var x = ev.clientX + pad; var y = ev.clientY + pad;
                                                if (x + ttW > window.innerWidth - 8) x = Math.max(8, ev.clientX - ttW - pad);
                                                if (y + ttH > window.innerHeight - 8) y = Math.max(8, ev.clientY - ttH - pad);
                                                sTooltip.style.left = x + 'px'; sTooltip.style.top = y + 'px';
                                            }catch(e){}
                                        });
                                        block.addEventListener('mouseleave', function(){ try{ if (sTooltip){ sTooltip.style.display='none'; sTooltip.innerHTML=''; } }catch(e){} });

                                        // click toggles selection
                                        block.onclick = function(){ try{ if (m.cb){ m.cb.checked = !m.cb.checked; try{ m.cb.dispatchEvent(new Event('change', { bubbles:true })); }catch(ex){} scheduleRender(0); } }catch(ex){} };

                                        list.appendChild(block);
                                    });
                                    // update compact title with count
                                    try{ if (sCompact){ sCompact.textContent = 'Предметы (' + selectedCount + ')'; } }catch(e){}
                                    requestAnimationFrame(function(){ recalcSelContentHeight(); });
                                }catch(e){}
                                }

                                // Public helpers for other scripts/buttons
                                window.__promoSelected_rebuildMap = rebuildMap;
                                window.__promoSelected_refresh = function(){ try{ rebuildMap(); scheduleRender(0); }catch(e){} };
                                window.__promoSelected_schedule = scheduleRender;

                                // Wire change listener (delegated)
                                try{
                                    var itemsCont = document.querySelector('.items_container');
                                    if (itemsCont && !itemsCont.getAttribute('data-selected-change-inited')){
                                        itemsCont.setAttribute('data-selected-change-inited','1');
                                        itemsCont.addEventListener('change', function(ev){ try{ if (ev && ev.target && ev.target.matches && ev.target.matches('input[type=checkbox]')){ scheduleRender(10); } }catch(e){} });
                                    }
                                }catch(e){}

                                // Initial build
                                rebuildMap(); renderSelectedList();
                            }catch(e){}
                        })();

                        // Section builders
                        function mkRow(title){
                            var row = document.createElement('div');
                            row.style = rowCss;
                            var lbl = document.createElement('div');
                            lbl.innerText = title;
                            lbl.style = 'min-width:120px; font-size:12px; font-weight:600; color:#705d4a;';
                            row.appendChild(lbl);
                            return { row: row, label: lbl };
                        }
                        function mkBtn(text, id, onClick){
                            var b = document.createElement('button');
                            b.innerText = text; if (id) b.id = id; b.style = pillBtnCss; b.onclick = function(){ try{ onClick && onClick(); }catch(e){} return false; };
                            return b;
                        }

                        // Row 0: Global actions (in one row with label, like other sections)
                        var rGlobal = mkRow('Быстрые действия');
                        rGlobal.row.append(selectAll);
                        rGlobal.row.append(clearAll);

                        // Row 1: Расходка (ранее «Хирки»)
                        var rAmu = mkRow('Расходка');
                        // Подсказка по разделу «Расходка»
                        try{ rAmu.label.title = 'Будут выбраны/убраны:\n— Амулеты (бронза/серебро/золото/платина)\n— Идолы (бронза/серебро/золото/платина)\n— Королевское особое печенье\n— Королевские особые пирожки'; }catch(e){}
                        var selectAmulets = mkBtn('Выбрать', 'selectAmuletsBtn', function(){
                            selectByLabelTextRegexes([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
                            selectByLabelTextRegexes([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
                            // Дополнительно: печенье/пирожки
                            selectByLabelTexts(['Королевское особое печенье', 'Королевские особые пирожки']);
                        });
                        var unselectAmulets = mkBtn('Убрать', 'unselectAmuletsBtn', function(){
                            unselectByLabelPatterns([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
                            unselectByLabelPatterns([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
                            // Дополнительно: печенье/пирожки
                            unselectByLabelTexts(['Королевское особое печенье', 'Королевские особые пирожки']);
                        });
                        rAmu.row.append(selectAmulets);
                        rAmu.row.append(unselectAmulets);

                        // Row 2: Проходки
                        var rPass = mkRow('Проходки');
                        try{ rPass.label.title = 'Будут выбраны/убраны:\n— Самоцвет грез'; }catch(e){}
                        var selectPass = mkBtn('Выбрать', 'selectPassBtn', function(){ selectByLabelTexts(['Самоцвет грез']); });
                        var unselectPass = mkBtn('Убрать', 'unselectPassBtn', function(){ unselectByLabelTexts(['Самоцвет грез']); });
                        rPass.row.append(selectPass);
                        rPass.row.append(unselectPass);

                        // Row 3: Подарки за подписку
                        var rSub = mkRow('Подарки за подписку');
                        try{ rSub.label.title = 'Будут выбраны/убраны:\n— Талон на золотой амулет\n— Талон на золотого идола\n— Дар из прошлого x2\n— Королевское особое печенье x200\n— Королевские особые пирожки x200'; }catch(e){}
                        var selectSubGifts = mkBtn('Выбрать', 'selectSubGiftsBtn', function(){
                            selectByLabelTexts([
                                'Талон на золотой амулет',
                                'Талон на золотого идола',
                                'Дар из прошлого x2',
                                'Королевское особое печенье x200',
                                'Королевские особые пирожки x200'
                            ]);
                        });
                        var removeSubGifts = mkBtn('Убрать', 'removeSubGiftsBtn', function(){
                            unselectByLabelTexts([
                                'Талон на золотой амулет',
                                'Талон на золотого идола',
                                'Дар из прошлого x2',
                                'Королевское особое печенье x200',
                                'Королевские особые пирожки x200'
                            ]);
                        });
                        rSub.row.append(selectSubGifts);
                        rSub.row.append(removeSubGifts);

                        // Row 4: Пилюли ДЗ
                        var rDzPills = mkRow('Пилюли ДЗ');
                        try{ rDzPills.label.title = 'Будут выбраны/убраны:\n— Небесная пилюля'; }catch(e){}
                        var selectDzPills = mkBtn('Выбрать', 'selectDzPillsBtn', function(){
                            selectByLabelTexts(['Небесная пилюля']);
                        });
                        var unselectDzPills = mkBtn('Убрать', 'unselectDzPillsBtn', function(){
                            unselectByLabelTexts(['Небесная пилюля']);
                        });
                        rDzPills.row.append(selectDzPills);
                        rDzPills.row.append(unselectDzPills);

                        // Custom search (separate block)
                        var inputCustomSearch = document.createElement('input');
                        inputCustomSearch.type = 'text';
                        inputCustomSearch.id = 'customSearchInput';
                        inputCustomSearch.placeholder = 'Введите название предмета...';
                        inputCustomSearch.style = 'padding:4px 8px; border-radius:10px; border:1px solid #C6B9A4; font-size:12px; line-height:18px; outline:none; flex:1; background:#FBF7EF; color:#333;';
                        var selectCustom = mkBtn('Выбрать', 'selectCustomBtn', function(){
                            var query = (inputCustomSearch.value||'').trim();
                            if (query.length>0) selectByLabelText(query);
                        });
                        var unselectCustom = mkBtn('Убрать', 'unselectCustomBtn', function(){
                            var query = (inputCustomSearch.value||'').trim();
                            if (query.length>0) unselectByLabelText(query);
                        });
                        var customContainer = document.createElement('div');
                        customContainer.style = 'display:flex; gap:6px; align-items:center;';
                        customContainer.append(inputCustomSearch);
                        customContainer.append(selectCustom);
                        customContainer.append(unselectCustom);

                        // Attach rows
                        buttonContainer.append(rGlobal.row);
                        var hr1 = document.createElement('div'); hr1.style='height:1px;background:#E2D8C9;margin:2px 0 3px 0;';
                        buttonContainer.append(hr1);
                        buttonContainer.append(rAmu.row);
                        buttonContainer.append(rPass.row);
                        buttonContainer.append(rSub.row);
                        buttonContainer.append(rDzPills.row);
                        var hr2 = document.createElement('div'); hr2.style='height:1px;background:#E2D8C9;margin:4px 0 3px 0;';
                        buttonContainer.append(hr2);
                        buttonContainer.append(customContainer);

                        // Mount into popup content container
                        var root = document.getElementById('promo_container');
                        if (root) {
                            root.append(buttonContainer);
                            // После того как элементы добавлены — автосайз через rAF, чтобы учесть рендер
                            setTimeout(function(){ try{ scheduleAutosize(0); requestAnimationFrame(function(){ applyContentHeight(); }); }catch(e){} }, 0);
                            // Наблюдатель за изменениями контента внутри попапа — на случай динамических мутаций
                            try{
                                if (!popup.getAttribute('data-mo-inited')){
                                    var mo = new MutationObserver(function(){
                                        var st0 = loadPopupState();
                                        // Если попап свёрнут — ничего не делаем
                                        if (st0 && st0.collapsed) return;
                                        // Всегда даём автосайзу подстроить высоту под новый контент
                                        scheduleAutosize(60);
                                    });
                                    mo.observe(root, { childList: true, subtree: true, attributes: false });
                                    popup.setAttribute('data-mo-inited','1');
                                }
                            }catch(e){}
                        }
                    }
                } catch(e) { /* ignore */ }
                """);

            var result = await Browser.ExecuteScriptAsync(
                """
                function createCharacterSelect(data) {
                    // Создаем стили через JavaScript
                    const style = $('<style></style>')
                        .html(`
                            .character-select-container {
                                font-family: Arial, sans-serif;
                                margin: 10px 0px;
                            }
                            .character-select {
                                width: 100%;
                                padding: 10px;
                                font-size: 14px;
                                border-radius: 5px;
                                background: #F0E8DC;
                                color: #333;
                                overflow: hidden;
                                overflow-y: visible;
                                max-height: none !important;
                                height: auto !important;
                            }
                            .character-select:focus {
                                outline: none;
                                border-color: #2c4a8d;
                                box-shadow: 0 0 5px rgba(74, 107, 175, 0.5);
                            }
                            .server-group {
                                font-weight: bold;
                                color: #2c4a8d;
                                background: #F6F1E7;
                                padding: 5px;
                            }
                            .character-option {
                                padding: 8px 15px;
                                border-bottom: 1px solid #f0f0f0;
                            }
                            .character-name {
                                font-weight: bold;
                                color: #333;
                            }
                            .character-info {
                                font-size: 12px;
                                color: #666;
                                margin-left: 10px;
                            }
                            .character-level {
                                float: right;
                                color: #4a6baf;
                                font-weight: bold;
                            }
                            option {
                                padding: 8px;
                                border-bottom: 1px solid #f0f0f0;
                            }
                            option:checked {
                                background: #C6B9A3;
                                color: white;
                            }
                        `);
                    
                    // Добавляем стили в head
                    $('head').append(style);
                
                    // Создаем контейнер и select
                    const container = $('<div class="character-select-container"></div>');
                    
                    // Подсчитываем общее количество опций для определения размера
                    let totalOptions = 0;
                    
                    for (const serverId in data) {
                        const server = data[serverId];
                        totalOptions += 1; // заголовок сервера
                        
                        for (const accountId in server.accounts) {
                            const account = server.accounts[accountId];
                            totalOptions += account.chars.length;
                        }
                    }
                    
                    // Создаем select с размером, равным количеству всех опций
                    const select = $('<select id="characterSelect" class="character-select"></select>')
                        .attr('size', totalOptions);
                    
                    // Проходим по всем серверам
                    for (const serverId in data) {
                        const server = data[serverId];
                        
                        // Добавляем заголовок сервера
                        select.append(`<option disabled class="server-group">─── ${server.name} ───</option>`);
                        
                        // Проходим по всем аккаунтам на сервере
                        for (const accountId in server.accounts) {
                            const account = server.accounts[accountId];
                            
                            // Добавляем всех персонажей аккаунта
                            for (const char of account.chars) {
                                const option = $('<option></option>')
                                    .val(char.id)
                                    .html(`${char.name} - ${char.occupation} (${char.level} ур.)`);
                                select.append(option);
                            }
                        }
                        
                    }
                    
                    // Обработчик выбора персонажа
                    select.change(function() {
                        const selectedId = $(this).val();
                        if (selectedId) {
                            // Находим выбранного персонажа
                            let selectedAccountId = '';
                            let selectedCharacterId = '';
                            let selectedServerId = '';
                            
                            for (const serverId in data) {
                                const server = data[serverId];
                                for (const accountId in server.accounts) {
                                    const account = server.accounts[accountId];
                                    for (const char of account.chars) {
                                        if (char.id == selectedId) {
                                            selectedAccountId = accountId;
                                            selectedCharacterId = selectedId;
                                            selectedServerId = server.id;
                                            break;
                                        }
                                    }
                                    if (selectedCharacterId) break;
                                }
                                if (selectedCharacterId) break;
                            }
                            
                            if (selectedCharacterId) {
                                var sel=document.querySelector('.js-shard');
                                sel.value=selectedServerId;
                                var e=document.createEvent('HTMLEvents');
                                e.initEvent('change',true,false);
                                sel.dispatchEvent(e);
                                
                                var sel2=document.querySelector('.js-char');
                                sel2.value=selectedAccountId + '_' + selectedServerId + '_' + selectedCharacterId;
                                var e2=document.createEvent('HTMLEvents');
                                e2.initEvent('change',true,false);
                                sel2.dispatchEvent(e);
                            }
                        }
                    });
                    
                    // Собираем всё вместе
                    container.append(select);
                    $('#characterContainer').append(container);
                }
                
                // Инициализация при загрузке документа
                $(document).ready(function() {
                    var root = document.getElementById('promo_container');
                    if (!root) return;
                    var characterContainer = document.createElement('div');
                    characterContainer.id = 'characterContainer';
                    root.append(characterContainer);
                    try { createCharacterSelect(shards); } catch(e) {}
                    var submitButton = document.createElement('div');
                    submitButton.className = 'go_items js-transfer-go';
                    root.append(submitButton);

                    // Предвыбор персонажа по последней отправке (CurrentAccount.PromoAccInfo)
                    try {
                        (function(){
                            var last = window.__pwLastAccInfo;
                            if (!last || typeof last !== 'string') return;
                            var parts = last.split('_');
                            if (parts.length < 3) return;
                            var accountId = parts[0];
                            var serverId = parts[1];
                            var characterId = parts[2];

                            function setNative(){
                                try {
                                    var shard = document.querySelector('.js-shard');
                                    if (shard) {
                                        shard.value = serverId;
                                        var ev = document.createEvent('HTMLEvents'); ev.initEvent('change', true, false); shard.dispatchEvent(ev);
                                    }
                                } catch(e){}
                                try {
                                    var jchar = document.querySelector('.js-char');
                                    if (jchar) {
                                        jchar.value = accountId + '_' + serverId + '_' + characterId;
                                        var ev2 = document.createEvent('HTMLEvents'); ev2.initEvent('change', true, false); jchar.dispatchEvent(ev2);
                                    }
                                } catch(e){}
                            }

                            function setCustom(){
                                try {
                                    var sel = document.getElementById('characterSelect');
                                    if (sel && sel.querySelector("option[value='"+characterId+"']")) {
                                        sel.value = characterId;
                                        var ev3 = document.createEvent('HTMLEvents'); ev3.initEvent('change', true, false); sel.dispatchEvent(ev3);
                                    }
                                } catch(e){}
                            }

                            function tryAll(attempt){
                                attempt = attempt || 0;
                                setNative();
                                setCustom();
                                // Повторяем несколько раз, т.к. элементы могут дорисовываться инициализацией страницы
                                if (attempt < 20) setTimeout(function(){ tryAll(attempt+1); }, 100);
                            }
                            tryAll(0);
                        })();
                    } catch(e){}
                });
                """);
            }

            // Hook submit to save data from form into host (CurrentAccount)
            try
            {
                await Browser.ExecuteScriptAsync(
                    """
                    (function(){
                        try{
                            if (window.__pwPromoHookInstalled) return; // once per page load
                            window.__pwPromoHookInstalled = true;

                            // simple debounce to avoid double send from click+submit
                            window.__pwPromoLastSentTs = 0;
                            function safePost(obj){
                                try{
                                    var now = Date.now();
                                    if (Math.abs(now - (window.__pwPromoLastSentTs||0)) < 900) return; // ~1s window
                                    window.__pwPromoLastSentTs = now;
                                    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                                        window.chrome.webview.postMessage(JSON.stringify(obj));
                                    }
                                }catch(e){}
                            }

                            function collectPromoData(form){
                                var data = { type: 'promo_form_submit', do: '', cart_items: [], acc_info: '' };
                                try{
                                    var doEl = form.querySelector("[name='do']");
                                    var doVal = doEl && typeof doEl.value !== 'undefined' ? (doEl.value||'') : '';
                                    data.do = doVal || 'process';
                                }catch(e){ data.do = 'process'; }
                                try{
                                    var accEl = form.querySelector("[name='acc_info']");
                                    if (accEl) data.acc_info = (accEl.value||'');
                                }catch(e){}
                                try{
                                    var items = form.querySelectorAll("input[name='cart_items[]']:checked");
                                    if (items && items.length){
                                        items.forEach(function(i){ if (i && i.value!=null) data.cart_items.push(String(i.value)); });
                                    }
                                }catch(e){}
                                return data;
                            }

                            function attachToForm(form){
                                if (!form || form.__pwPromoSubmitAttached) return;
                                form.__pwPromoSubmitAttached = true;
                                form.addEventListener('submit', function(ev){
                                    try{ safePost(collectPromoData(form)); }catch(e){}
                                }, true); // capture phase
                            }

                            function attachClickInitiators(root){
                                try{
                                    if (root && !root.__pwPromoClickAttached){
                                        root.__pwPromoClickAttached = true;
                                        document.addEventListener('click', function(e){
                                            try{
                                                var t = e.target;
                                                if (!t) return;
                                                // find closest element with .js-transfer-go
                                                var go = t.closest ? t.closest('.js-transfer-go') : null;
                                                if (!go) return;
                                                var form = (go.closest && go.closest('form')) || document.querySelector('form.js-transfer-form') || document.querySelector('form');
                                                if (!form) return;
                                                safePost(collectPromoData(form));
                                            }catch(_){}
                                        }, true);
                                    }
                                }catch(e){}
                            }

                            // Prefer a specific promo form
                            var promoForm = document.querySelector('form.js-transfer-form');
                            if (promoForm) attachToForm(promoForm);

                            // Fallback: attach to all forms on the page that are likely related
                            var forms = document.getElementsByTagName('form');
                            if (forms && forms.length){
                                for (var i=0;i<forms.length;i++){
                                    var f = forms[i];
                                    try{
                                        var ok = true;
                                        var act = (f.getAttribute('action')||'').toLowerCase();
                                        if (act && act.indexOf('promo_items.php') === -1){ ok = location.pathname.toLowerCase().indexOf('promo_items.php') !== -1; }
                                        if (ok) attachToForm(f);
                                    }catch(e){ attachToForm(f); }
                                }
                            }

                            // Click initiators
                            attachClickInitiators(document.documentElement || document.body);

                            // Also observe dynamically added forms/buttons
                            try{
                                var mo = new MutationObserver(function(muts){
                                    muts.forEach(function(m){
                                        (m.addedNodes||[]).forEach(function(n){
                                            try{
                                                if (!n) return;
                                                if (n.tagName && n.tagName.toLowerCase() === 'form') attachToForm(n);
                                                var innerForms = n.querySelectorAll ? n.querySelectorAll('form') : [];
                                                if (innerForms && innerForms.length){ innerForms.forEach(attachToForm); }
                                            }catch(e){}
                                        });
                                    });
                                });
                                mo.observe(document.documentElement || document.body, { childList:true, subtree:true });
                            }catch(e){}

                        }catch(e){}
                    })();
                    """
                );
            }
            catch { }

            // Also send ping separately (fallback) in case the big block above fails early
            try
            {
                await Browser.ExecuteScriptAsync(
                    """
                    try{ if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){ window.chrome.webview.postMessage('{"type":"promo_ping2","ts":"'+Date.now()+'"}'); } }catch(_){}
                    """
                );
            }
            catch { }

            
            
            // Подключаем расширенный JS для работы с промо‑страницей (в т.ч. всплывающее окно сундуков)
            try
            {
                var chestsJs = GetPromoChestsPopupJs();
                if (!string.IsNullOrWhiteSpace(chestsJs))
                {
                    await Browser.ExecuteScriptAsync(chestsJs);
                }
            }
            catch
            {
                // игнорируем ошибки загрузки вспомогательного скрипта
            }
            // await Browser.ExecuteScriptAsync("$('.description-items').remove();");
        }
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        try { Wv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }
        try {
            // Enable web messages and subscribe to receive messages from injected scripts
            Wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
        } catch { }
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
            try { core.AddScriptToExecuteOnDocumentCreatedAsync(helper); } catch { }
        }
        catch { }
        try { Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged; } catch { }
        try { Wv.CoreWebView2.WebMessageReceived -= CoreWebView2OnWebMessageReceived; } catch { }
        try { Wv.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived; } catch { }
        try { UpdateNavUi(); } catch { }
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }
        Browser.SetCookieAsync([]);
    }


    private async void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try { IsLoading = false; } catch { }
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
            try { payloadStr = e.TryGetWebMessageAsString(); } catch { }
            if (string.IsNullOrWhiteSpace(payloadStr))
            {
                try { payloadStr = e.WebMessageAsJson; } catch { }
            }
            if (string.IsNullOrWhiteSpace(payloadStr)) return;

            try { System.Diagnostics.Debug.WriteLine("[WebMessage] raw=" + (payloadStr?.Length > 300 ? payloadStr.Substring(0,300)+"..." : payloadStr)); } catch { }

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
                catch { }
            }
            HandleWebPayload(root);
        }
        catch { }
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
                    try { Wv.CoreWebView2?.PostWebMessageAsJson(resp); } catch { }
                }
                catch { }
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
                    try { Wv.CoreWebView2?.PostWebMessageAsJson(resp); } catch { }
                    try { _ = appConfig.SaveAsync(); } catch { }
                }
                catch { }
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
                        catch { }
                    }
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[promo_popup] event={evName ?? "(null)"}, ts={ts}, data={dataStr ?? "null"}");
                    }
                    catch { }
                }
                catch { }
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
            var accInfo = root.TryGetProperty("acc_info", out var accEl) ? accEl.GetString() ?? string.Empty : string.Empty;

            List<string> cartItems = new();
            if (root.TryGetProperty("cart_items", out var cartEl) && cartEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in cartEl.EnumerateArray())
                {
                    try { cartItems.Add(it.ToString()); } catch { }
                }
            }

            void apply()
            {
                var acc = AccountManager?.CurrentAccount;
                if (acc == null) return;
                try { acc.PromoDo = doVal; } catch { }
                try { acc.PromoAccInfo = accInfo; } catch { }
                try { acc.PromoCartItems = cartItems; } catch { }
                try { acc.PromoLastSubmittedAt = DateTime.Now; } catch { }

                using var db = new AppDbContext();
                db.Update(acc);
                db.SaveChanges();
            }

            if (!Dispatcher.CheckAccess()) Dispatcher.Invoke(apply); else apply();
        }
        catch { }
    }

    private void CoreWebView2OnHistoryChanged(object? sender, object e)
    {
        try { UpdateNavUi(); } catch { }
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
        catch { }

        try { AddressBox.Text = Wv?.Source?.ToString() ?? AddressBox.Text; } catch { }
    }

    private void BtnBack_OnClick(object sender, RoutedEventArgs e)
    {
        try { if (Wv?.CoreWebView2?.CanGoBack == true) Wv.CoreWebView2.GoBack(); } catch { }
        try { UpdateNavUi(); } catch { }
    }

    private void BtnForward_OnClick(object sender, RoutedEventArgs e)
    {
        try { if (Wv?.CoreWebView2?.CanGoForward == true) Wv.CoreWebView2.GoForward(); } catch { }
        try { UpdateNavUi(); } catch { }
    }

    private void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        try { if (Wv?.CoreWebView2 != null) Wv.CoreWebView2.Reload(); } catch { }
        try { UpdateNavUi(); } catch { }
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
        catch { }
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
        catch { }
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
        catch { }
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
        catch { }
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
        catch { return null; }
    }
}