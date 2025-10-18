using Microsoft.Web.WebView2.Core;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Windows;

namespace Pw.Hub.Pages;

public partial class AccountPage
{
    public Account Account { get; set; }

    public readonly IAccountManager _accountManager;
    public readonly IBrowser _browser;
    private LuaScriptRunner _luaRunner;

    public bool IsCoreInitialized => Wv?.CoreWebView2?.CookieManager != null;

    public AccountPage()
    {
        InitializeComponent();
        Wv.Source = new Uri("https://pwonline.ru/promo_items.php");
        Wv.NavigationCompleted += WvOnNavigationCompleted;
        _browser = new WebCoreBrowser(Wv);
        _accountManager = new AccountManager(_browser);
        _luaRunner = new LuaScriptRunner(_accountManager, _browser);
    }

    private void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _browser.ExecuteScriptAsync(
            """
            $('.items_container input[type=checkbox]').unbind('click');
            """);
        if (_browser.Source.AbsoluteUri.Contains("promo_items.php"))
        {
            _browser.ExecuteScriptAsync(
                """
                var element = document.createElement('div');
                element.innerHTML = '&nbsp;';
                element.className = 'top-news';
                element.id = 'promo_separator';

                var breakLine = document.createElement('br');

                $('.promo_container_content_body')[0].after(element);
                $('.promo_container_content_body')[0].after(breakLine);
                """);
            _browser.ExecuteScriptAsync(
                """
                var container = document.createElement('div');
                container.className = 'promo_container_content_body';

                var buttonContainer = document.createElement('div');
                buttonContainer.style = 'display: flex; gap: 8px; margin-top: 8px; flex-wrap: wrap;';

                var title = document.createElement('h4');
                title.innerHTML = 'У<span class="lower">правление</span>';

                var selectAll = document.createElement('button');
                selectAll.innerText = 'Выбрать все';
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
                selectAmulets.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                selectAmulets.onclick = function() {
                    selectByLabelTextRegexes([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
                    selectByLabelTextRegexes([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
                    return false;
                };

                var selectPass = document.createElement('button');
                selectPass.innerText = 'Проходки';
                selectPass.style = 'margin: 0; font-size: 14px; line-height: 24px; font-weight: 500; border: none; cursor: pointer; padding: 8px 16px; border-radius: 24px; -webkit-appearance: button; -webkit-writing-mode: horizontal-tb !important; text-rendering: auto; display: inline-block; text-align: center; white-space: nowrap; background-color: #D2C0BE;';
                selectPass.onclick = function() {
                    selectByLabelTexts(['Самоцвет грез']);
                    return false;
                }

                var inputCustomSearch = document.createElement('input');
                inputCustomSearch.type = 'text';
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
                """);
        }
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        _browser.SetCookieAsync([]);
    }


    private async void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (Account == null)
            return;
        if (!string.IsNullOrEmpty(Account.ImageSource))
            return;
        var isAuth = await _accountManager.IsAuthorizedAsync();
        if (!isAuth)
            return;
        var imgSrc =
            await _browser.ExecuteScriptAsync("document.querySelector(\"div.user_photo > span > a > img\").src");
        if (string.IsNullOrEmpty(imgSrc))
            return;
        if (imgSrc.Contains("null"))
            return;
        Account.ImageSource = imgSrc.Trim('"');
        Account.LastVisit = DateTime.UtcNow;
        await using var db = new AppDbContext();
        db.Update(Account);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ChangeAccount(Account account)
    {
        Account = account;
        await _accountManager.ChangeAccountAsync(account.Id);
        return await _accountManager.IsAuthorizedAsync();
    }

    // LUA buttons handlers
    private async void OnLuaBrowserNavigate(object sender, RoutedEventArgs e)
    {
        await _luaRunner.RunAsync("browser_navigate.lua");
    }
    private async void OnLuaBrowserExecJs(object sender, RoutedEventArgs e)
    {
        await _luaRunner.RunAsync("browser_exec_js.lua");
    }
    private async void OnLuaBrowserReload(object sender, RoutedEventArgs e)
    {
        await _luaRunner.RunAsync("browser_reload.lua");
    }
    private async void OnLuaAccountGet(object sender, RoutedEventArgs e)
    {
        await _luaRunner.RunAsync("account_get_account.lua");
    }
    private async void OnLuaAccountIsAuthorized(object sender, RoutedEventArgs e)
    {
        await _luaRunner.RunAsync("account_is_authorized.lua");
    }
    private async void OnLuaAccountGetAccounts(object sender, RoutedEventArgs e)
    {
        await _luaRunner.RunAsync("account_get_accounts.lua");
    }
    private async void OnLuaAccountChange(object sender, RoutedEventArgs e)
    {
        await using var db = new AppDbContext();
        var accounts = await db.Accounts.ToArrayAsync();
        var id = accounts[0].Id.ToString();
        await _luaRunner.RunAsync("account_change_account.lua", id);
    }

    private void OnOpenLuaEditor(object sender, RoutedEventArgs e)
    {
        var selectedId = Account?.Id.ToString();
        var wnd = new LuaEditorWindow(_luaRunner, selectedId);
        wnd.Show();
        wnd.Activate();
    }
}