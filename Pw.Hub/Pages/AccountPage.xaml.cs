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

            await Browser.ExecuteScriptAsync(
                """
                var hasElement = !!document.getElementById('promo_separator')
                if (!hasElement) 
                {
                    var element = document.createElement('div');
                    element.innerHTML = '&nbsp;';
                    element.className = 'top-news';
                    element.id = 'promo_separator';
                    
                    var breakLine = document.createElement('br');
                    
                    $('.promo_container_content_body')[0].after(element);
                    $('.promo_container_content_body')[0].after(breakLine);
                }
                """);
            
            await Browser.ExecuteScriptAsync(
                """
                try {
                    // helpers to persist/restore state
                    function savePopupState(el, state){
                        try{ localStorage.setItem('promo_popup_state', JSON.stringify(state)); }catch(e){}
                    }
                    function loadPopupState(){
                        try{ var s = localStorage.getItem('promo_popup_state'); return s ? JSON.parse(s) : null; }catch(e){ return null; }
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
                                popup.style.minWidth = '260px';
                                popup.style.minHeight = '140px';
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
                            'right: 16px', // default placement
                            'bottom: 16px',
                            'z-index: 2147483647',
                            'background: #F6F1E7',
                            'border: 1px solid #E2D8C9',
                            'border-radius: 12px',
                            'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
                            'overflow: hidden',
                            'color: #333',
                            'font-family: Arial, sans-serif',
                            'width: 380px',
                            'min-width: 260px',
                            'max-width: 50vw',
                            'min-height: 140px',
                            'max-height: 80vh'
                        ].join(';');

                        header = document.createElement('div');
                        header.style = 'display:flex;align-items:center;justify-content:space-between;padding:8px 12px;background:#EDE4D6;border-bottom:1px solid #E2D8C9;cursor:move;user-select:none;';
                        var hTitle = document.createElement('div');
                        hTitle.innerHTML = 'У<span class="lower">правление</span>';
                        hTitle.style = 'font-weight:700;color:#2c4a8d;';
                        var toggleBtn = document.createElement('button');
                        toggleBtn.innerText = '−';
                        toggleBtn.title = 'Свернуть';
                        toggleBtn.style = 'border:none;background:#D2C0BE;color:#333;border-radius:16px;padding:2px 8px;cursor:pointer;';

                        contentWrap = document.createElement('div');
                        contentWrap.id = 'promo_popup_content';
                        contentWrap.style = 'padding:10px;overflow:auto;';

                        var container = document.createElement('div');
                        container.className = 'promo_container_content_body';
                        container.id = 'promo_container';

                        // resize handle
                        var resizer = document.createElement('div');
                        resizer.id = 'promo_popup_resizer';
                        resizer.style = 'position:absolute;width:14px;height:14px;right:2px;bottom:2px;cursor:nwse-resize;background:transparent;';
                        // small visual triangle
                        resizer.innerHTML = '<svg width="14" height="14" viewBox="0 0 14 14" style="display:block"><path d="M2 12 L12 2 L12 12 Z" fill="#00000022"/></svg>';

                        // compact view (as a tiny movable button)
                        compact = document.createElement('div');
                        compact.id = 'promo_popup_compact';
                        compact.style = 'display:none; margin:6px; padding:6px 12px; background:#EDE4D6; color:#2c4a8d; font-weight:700; border-radius:16px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
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
                            compact.style = 'display:none; margin:6px; padding:6px 12px; background:#EDE4D6; color:#2c4a8d; font-weight:700; border-radius:16px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
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

                        // Авторазмер при первом запуске: подгоняем так, чтобы контент помещался без скролла
                        function autosizePopupIfNoState(force){
                            try{
                                if (!popup || !contentWrap) return;
                                var st0 = loadPopupState();
                                // Если уже есть сохранённые размеры — не трогаем
                                if (!force && st0 && (st0.width || st0.height)) return;
                                // Если свёрнуто — не трогаем (развернётся — пересчитаем)
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

                                // Высота = шапка + внутренняя прокрутка контента + вертикальные паддинги
                                var headH = header ? header.offsetHeight : 0;
                                var cScrollH = inner.scrollHeight + padV;
                                var desiredH = headH + cScrollH;
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
                                if (moved && compact){
                                    compact.dataset.dragJustDragged = '1';
                                    setTimeout(function(){ delete compact.dataset.dragJustDragged; delete compact.dataset.dragMoved; }, 120);
                                }
                            }
                            if (header) header.addEventListener('mousedown', onMouseDown);
                            if (compact) compact.addEventListener('mousedown', onMouseDown);
                        })();

                        // resize logic (by handle)
                        (function(){
                            var handle = document.getElementById('promo_popup_resizer');
                            if (!handle) return;
                            var resizing=false, startX=0, startY=0, startW=0, startH=0;
                            function onDown(e){
                                resizing = true;
                                var rect = popup.getBoundingClientRect();
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
                                var newW = Math.min(Math.max(260, startW + dx), Math.floor(window.innerWidth * 0.9));
                                var newH = Math.min(Math.max(140, startH + dy), Math.floor(window.innerHeight * 0.9));
                                popup.style.width = newW + 'px';
                                popup.style.height = newH + 'px';
                                applyContentHeight();
                            }
                            function onUp(){
                                if (!resizing) return;
                                resizing = false;
                                document.removeEventListener('mousemove', onMove);
                                document.removeEventListener('mouseup', onUp);
                                var rect = popup.getBoundingClientRect();
                                var st = loadPopupState() || {};
                                st.width = rect.width; st.height = rect.height;
                                savePopupState(popup, st);
                            }
                            handle.addEventListener('mousedown', onDown);
                            // ensure initial content sizing
                            scheduleAutosize(0);
                            requestAnimationFrame(function(){ applyContentHeight(); });
                            window.addEventListener('resize', function(){
                                // При ресайзе окна убедимся, что контент влезает максимально корректно
                                applyContentHeight();
                                // если пользователь ещё не сохранял размеры — подстрахуемся автосайзом
                                var st0 = loadPopupState();
                                if (!(st0 && (st0.width || st0.height)) && !(st0 && st0.collapsed)){
                                    scheduleAutosize(50);
                                }
                            });
                        })();
                    }

                    // Build controls only once inside promo_container
                    if (!document.getElementById('selectAllBtn')) {
                        var buttonContainer = document.createElement('div');
                        buttonContainer.style = 'display: flex; gap: 8px; margin-top: 8px; flex-wrap: wrap;';

                        var selectAll = document.createElement('button');
                        selectAll.innerText = 'Выбрать все';
                        selectAll.id = 'selectAllBtn';
                        selectAll.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                        selectAll.onclick = function() {
                            var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
                            checkboxes.forEach(function(checkbox) { checkbox.checked = true; });
                            return false;
                        };

                        var clearAll = document.createElement('button');
                        clearAll.innerText = 'Снять все';
                        clearAll.id = 'clearAllBtn';
                        clearAll.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                        clearAll.onclick = function() {
                            var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
                            checkboxes.forEach(function(checkbox) { checkbox.checked = false; });
                            return false;
                        };

                        var selectByLabelTextRegex = function(pattern) {
                            var labels = document.querySelectorAll('.items_container label');
                            labels.forEach(function(label) {
                                if (pattern.test((label.innerText||'').toLowerCase())) {
                                    var checkboxId = label.getAttribute('for');
                                    var checkbox = document.getElementById(checkboxId);
                                    if (checkbox && checkbox.type === 'checkbox') {
                                        checkbox.checked = true;
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
                                        checkbox.checked = true;
                                    }
                                }
                            });
                        };

                        var selectByLabelTexts = function(texts) { texts.forEach(function(text){ selectByLabelText(text); }); };

                        var selectAmulets = document.createElement('button');
                        selectAmulets.innerText = 'Хирки';
                        selectAmulets.id = 'selectAmuletsBtn';
                        selectAmulets.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                        selectAmulets.onclick = function() {
                            selectByLabelTextRegexes([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
                            selectByLabelTextRegexes([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
                            return false;
                        };

                        var selectPass = document.createElement('button');
                        selectPass.innerText = 'Проходки';
                        selectPass.id = 'selectPassBtn';
                        selectPass.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                        selectPass.onclick = function() { selectByLabelTexts(['Самоцвет грез']); return false; };

                        var inputCustomSearch = document.createElement('input');
                        inputCustomSearch.type = 'text';
                        inputCustomSearch.id = 'customSearchInput';
                        inputCustomSearch.placeholder = 'Поиск...';
                        inputCustomSearch.style = 'padding: 8px 16px; border-radius: 24px; border: 1px solid #ccc; font-size: 14px; line-height: 24px; outline: none; flex-grow: 1;';

                        var selectByCustomSearch = function() {
                            var query = (inputCustomSearch.value||'').trim();
                            if (query.length > 0) { selectByLabelText(query); }
                        };

                        var selectCustom = document.createElement('button');
                        selectCustom.innerText = 'Выбрать';
                        selectCustom.id = 'selectCustomBtn';
                        selectCustom.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                        selectCustom.onclick = function() { selectByCustomSearch(); return false; };

                        var customContainer = document.createElement('div');
                        customContainer.style = 'display: flex; gap: 8px; flex-grow: 1;';
                        customContainer.append(inputCustomSearch);
                        customContainer.append(selectCustom);

                        // Attach buttons
                        buttonContainer.append(selectAll);
                        buttonContainer.append(clearAll);
                        buttonContainer.append(selectAmulets);
                        buttonContainer.append(selectPass);
                        buttonContainer.append(customContainer);

                        // Mount into popup content container
                        var root = document.getElementById('promo_container');
                        if (root) {
                            // Optional title inside content for context
                            var innerTitle = document.createElement('div');
                            innerTitle.innerHTML = '<b>Быстрые действия</b>';
                            innerTitle.style = 'margin-top:4px;margin-bottom:4px;color:#2c4a8d;';
                            root.append(innerTitle);
                            root.append(buttonContainer);
                            // После того как элементы добавлены — автосайз через rAF, чтобы учесть рендер
                            setTimeout(function(){ try{ scheduleAutosize(0); requestAnimationFrame(function(){ applyContentHeight(); }); }catch(e){} }, 0);
                            // Наблюдатель за изменениями контента внутри попапа — на случай динамических мутаций
                            try{
                                if (!popup.getAttribute('data-mo-inited')){
                                    var mo = new MutationObserver(function(){
                                        var st0 = loadPopupState();
                                        if (!(st0 && (st0.width || st0.height)) && !(st0 && st0.collapsed)){
                                            scheduleAutosize(60);
                                        }
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
                });
                """);

            // await Browser.ExecuteScriptAsync("$('.description-items').remove();");
        }
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        try { Wv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }
        try { Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged; } catch { }
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