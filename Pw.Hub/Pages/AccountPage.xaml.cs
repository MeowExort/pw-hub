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

    private void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try { IsLoading = false; } catch { }
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }
        try { UpdateNavUi(); } catch { }
        if (Browser.Source.AbsoluteUri.Contains("promo_items.php"))
        {
            if (!Browser.Source.AbsoluteUri.Contains("do=activate"))
            {
                Browser.ExecuteScriptAsync(
                    """
                    $('.items_container input[type=checkbox]').unbind('click');
                    """);
            }

            Browser.ExecuteScriptAsync(
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
            Browser.ExecuteScriptAsync(
                """
                var hasElement = !!document.getElementById('promo_container')
                if (!hasElement)
                {
                    var container = document.createElement('div');
                    container.className = 'promo_container_content_body';
                    container.id = 'promo_container'
    
                    var buttonContainer = document.createElement('div');
                    buttonContainer.style = 'display: flex; gap: 8px; margin-top: 8px; flex-wrap: wrap;';
    
                    var title = document.createElement('h4');
                    title.innerHTML = 'У<span class="lower">правление</span>';
    
                    var selectAll = document.createElement('button');
                    selectAll.innerText = 'Выбрать все';
                    selectAll.id = 'selectAllBtn';
                    selectAll.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                    selectAll.onclick = function() {
                        var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
                        checkboxes.forEach(function(checkbox) {
                            checkbox.checked = true;
                        });
                        return false;
                    };
    
                    var clearAll = document.createElement('button');
                    clearAll.innerText = 'Снять все';
                    clearAll.id = 'clearAllBtn';
                    clearAll.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                    clearAll.onclick = function() {
                        var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
                        checkboxes.forEach(function(checkbox) {
                            checkbox.checked = false;
                        });
                        return false;
                    };
    
                    var selectByLabelTextRegex = function(pattern) {
                        var labels = document.querySelectorAll('.items_container label');
                        labels.forEach(function(label) {
                            if (pattern.test(label.innerText.toLowerCase())) {
                                var checkboxId = label.getAttribute('for');
                                var checkbox = document.getElementById(checkboxId);
                                if (checkbox && checkbox.type === 'checkbox') {
                                    checkbox.checked = true;
                                }
                            }
                        });
                    };
    
                    var selectByLabelTextRegexes = function(patterns) {
                        patterns.forEach(function(pattern) {
                            selectByLabelTextRegex(pattern);
                        });
                    };
    
                    var selectByLabelText = function(text) {
                        var labels = document.querySelectorAll('.items_container label');
                        labels.forEach(function(label) {
                            if (label.innerText.toLowerCase().includes(text.toLowerCase())) {
                                var checkboxId = label.getAttribute('for');
                                var checkbox = document.getElementById(checkboxId);
                                if (checkbox && checkbox.type === 'checkbox') {
                                    checkbox.checked = true;
                                }
                            }
                        });
                    };
    
                    var selectByLabelTexts = function(texts) {
                        texts.forEach(function(text) {
                            selectByLabelText(text);
                        });
                    };
    
                    var selectAmulets = document.createElement('button');
                    selectAmulets.innerText = 'Хирки';
                    selectAmulets.id = 'selectAmuletsBtn';
                    selectAmulets.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                    selectAmulets.onclick = function() {
                        selectByLabelTextRegexes([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
                        selectByLabelTextRegexes([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
                        return false;
                    };
    
                    var selectPass = document.createElement('button');
                    selectPass.innerText = 'Проходки';
                    selectPass.id = 'selectPassBtn';
                    selectPass.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                    selectPass.onclick = function() {
                        selectByLabelTexts(['Самоцвет грез']);
                        return false;
                    }
    
                    var inputCustomSearch = document.createElement('input');
                    inputCustomSearch.type = 'text';
                    inputCustomSearch.id = 'customSearchInput';
                    inputCustomSearch.placeholder = 'Поиск...';
                    inputCustomSearch.style = 'padding: 8px 16px; border-radius: 24px; border: 1px solid #ccc; font-size: 14px; line-height: 24px; outline: none; flex-grow: 1;';
    
                    var selectByCustomSearch = function() {
                        var query = inputCustomSearch.value.trim();
                        if (query.length > 0) {
                            selectByLabelText(query);
                        }
                    };
    
                    var selectCustom = document.createElement('button');
                    selectCustom.innerText = 'Выбрать';
                    selectCustom.id = 'selectCustomBtn';
                    selectCustom.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                    selectCustom.onclick = function() {
                        selectByCustomSearch();
                        return false;
                    };
    
                    var customContainer = document.createElement('div');
                    customContainer.style = 'display: flex; gap: 8px; flex-grow: 1;';
                    customContainer.append(inputCustomSearch);
                    customContainer.append(selectCustom);
    
    
                    buttonContainer.append(selectAll);
                    buttonContainer.append(clearAll);
                    buttonContainer.append(selectAmulets);
                    buttonContainer.append(selectPass);
                    buttonContainer.append(customContainer);
    
                    container.append(title);
                    container.append(buttonContainer);
    
                    $('#promo_separator')[0].after(container);
                }
                """);
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