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
                    // Create floating popup if not exists
                    if (!document.getElementById('promo_popup')) {
                        var popup = document.createElement('div');
                        popup.id = 'promo_popup';
                        popup.style = [
                            'position: fixed',
                            'right: 16px',
                            'bottom: 16px',
                            'z-index: 2147483647',
                            'background: #F6F1E7',
                            'border: 1px solid #E2D8C9',
                            'border-radius: 12px',
                            'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
                            'max-height: 60vh',
                            'overflow: hidden',
                            'color: #333',
                            'font-family: Arial, sans-serif'
                        ].join(';');

                        var header = document.createElement('div');
                        header.style = 'display:flex;align-items:center;justify-content:space-between;padding:8px 12px;background:#EDE4D6;border-bottom:1px solid #E2D8C9;';
                        var hTitle = document.createElement('div');
                        hTitle.innerHTML = 'У<span class="lower">правление</span>';
                        hTitle.style = 'font-weight:700;color:#2c4a8d;';
                        var toggleBtn = document.createElement('button');
                        toggleBtn.innerText = '−';
                        toggleBtn.title = 'Свернуть';
                        toggleBtn.style = 'border:none;background:#D2C0BE;color:#333;border-radius:16px;padding:2px 8px;cursor:pointer;';

                        var contentWrap = document.createElement('div');
                        contentWrap.id = 'promo_popup_content';
                        contentWrap.style = 'padding:10px;overflow:auto;max-height:calc(60vh - 42px)';

                        var container = document.createElement('div');
                        container.className = 'promo_container_content_body';
                        container.id = 'promo_container';

                        contentWrap.appendChild(container);
                        header.appendChild(hTitle);
                        header.appendChild(toggleBtn);
                        popup.appendChild(header);
                        popup.appendChild(contentWrap);
                        document.body.appendChild(popup);

                        // Toggle handler with local state
                        var collapsed = false;
                        toggleBtn.onclick = function(){
                            collapsed = !collapsed;
                            contentWrap.style.display = collapsed ? 'none' : 'block';
                            toggleBtn.innerText = collapsed ? '+' : '−';
                        };
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