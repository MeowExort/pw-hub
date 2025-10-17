using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Tools;

namespace Pw.Hub.Pages;

public partial class AccountPage
{
    public Account Account { get; set; }

    private const string CookieFileExtension = ".json";
    private const string CookieFolder = "Cookies";

    private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public bool IsCoreInitialized => Wv?.CoreWebView2?.CookieManager != null;

    public AccountPage()
    {
        InitializeComponent();
        Wv.Source = new Uri("https://pwonline.ru/promo_items.php");
        Wv.NavigationCompleted += WvOnNavigationCompleted;
    }

    private void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Wv.ExecuteScriptAsync(
            """
            $('.items_container input[type=checkbox]').unbind('click');
            """);
        if (Wv.Source.AbsoluteUri.Contains("promo_items.php"))
        {
            Wv.ExecuteScriptAsync(
                """
                var element = document.createElement('div');
                element.innerHTML = '&nbsp;';
                element.className = 'top-news';
                element.id = 'promo_separator';

                var breakLine = document.createElement('br');

                $('.promo_container_content_body')[0].after(element);
                $('.promo_container_content_body')[0].after(breakLine);
                """);
            Wv.ExecuteScriptAsync(
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

    private string GetCookieFilePath()
    {
        var path = Path.Combine(CookieFolder, $"{Account.Id:N}{CookieFileExtension}");
        if (!Directory.Exists(CookieFolder))
            Directory.CreateDirectory(CookieFolder);
        return path;
    }

    private async void Wv_OnUnloaded(object sender, RoutedEventArgs e)
    {
        await SaveCookies();
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (Wv?.CoreWebView2?.CookieManager is not { } cm)
            return;

        cm.DeleteAllCookies();
    }

    private async Task ResetCookies()
    {
        try
        {
            if (Wv?.CoreWebView2?.CookieManager is not { } cm)
                return;

            cm.DeleteAllCookies();

            if (Account == null)
                return;

            if (!File.Exists(GetCookieFilePath()))
                return;

            var json = await File.ReadAllTextAsync(GetCookieFilePath());
            var cookies = JsonSerializer.Deserialize<Cookie[]>(json, JsonSerializerOptions.Web);
            if (cookies == null)
                return;

            foreach (var cookie in cookies)
            {
                var coreCookie = cm.CreateCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path);
                coreCookie.Expires = cookie.Expires;
                coreCookie.IsHttpOnly = cookie.IsHttpOnly;
                coreCookie.IsSecure = cookie.IsSecure;
                coreCookie.SameSite = cookie.SameSite;
                cm.AddOrUpdateCookie(coreCookie);
            }
        }
        finally
        {
            if (Wv?.CoreWebView2 != null)
            {
                Wv.Reload();
                await WaitForElementAsync();
            }
        }
    }

    private async Task SaveCookies()
    {
        if (Account == null)
            return;
        var coreCookies = await Wv.CoreWebView2.CookieManager.GetCookiesAsync("");
        var cookies = coreCookies.Select(Cookie.FromCoreWebView2Cookie).ToList();
        var json = JsonSerializer.Serialize(cookies, JsonSerializerOptions.Web);
        await File.WriteAllTextAsync(GetCookieFilePath(), json);
    }

    public async Task<bool> ChangeAccount(Account account)
    {
        await _semaphoreSlim.WaitAsync();
        await SaveCookies();
        Account = account;
        DataContext = account;
        await ResetCookies();
        var result = await CheckAuth();
        await using var db = new AppDbContext();
        account.LastVisit = DateTime.UtcNow;
        db.Update(account);
        await db.SaveChangesAsync();
        _semaphoreSlim.Release();
        return result;
    }
    
    public async Task<bool> CheckPageLoaded()
    {
        if (Wv?.CoreWebView2 == null)
            return false;
        var result = await Wv.ExecuteScriptAsync("$('. > h2')[0].innerText");
        result = result.Trim('"').Replace("null", "");
        return !string.IsNullOrEmpty(result);
    }

    public async Task<bool> CheckAuth()
    {
        var result = await GetAccount();
        return !string.IsNullOrEmpty(result);
    }

    private async void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (Account == null)
            return;
        if (!string.IsNullOrEmpty(Account.ImageSource))
            return;
        var isAuth = await CheckAuth();
        if (!isAuth)
            return;
        var imgSrc = await Wv.ExecuteScriptAsync("document.querySelector(\"div.user_photo > span > a > img\").src");
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

    public async Task<string> GetAccount()
    {
        var result = await Wv.ExecuteScriptAsync("$('.auth_h > h2 > a > strong')[0].innerText");
        return result.Trim('"').Replace("null", "");
    }

    private async Task WaitForElementAsync()
    {
        var length = 0;
        do
        {
            await Task.Delay(50);
            var result = await Wv.ExecuteScriptAsync("$('.main_menu').length");
            result = result.Trim('"').Replace("null", "");
            if (int.TryParse(result, out var len) && len != length)
            {
                length = len;
            }
        } while (length == 0);
    }
}