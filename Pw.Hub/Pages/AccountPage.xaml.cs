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
    public readonly IAccountManager AccountManager;
    public readonly IBrowser Browser;
    public LuaScriptRunner LuaRunner;

    public bool IsCoreInitialized => Wv?.CoreWebView2?.CookieManager != null;

    public AccountPage()
    {
        InitializeComponent();
        Wv.Source = new Uri("https://pwonline.ru/promo_items.php");
        Wv.NavigationCompleted += WvOnNavigationCompleted;
        Browser = new WebCoreBrowser(Wv);
        AccountManager = new AccountManager(Browser);
        LuaRunner = new LuaScriptRunner(AccountManager, Browser);
    }

    private void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
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
                var element = document.createElement('div');
                element.innerHTML = '&nbsp;';
                element.className = 'top-news';
                element.id = 'promo_separator';

                var breakLine = document.createElement('br');

                $('.promo_container_content_body')[0].after(element);
                $('.promo_container_content_body')[0].after(breakLine);
                """);
            Browser.ExecuteScriptAsync(
                """
                var container = document.createElement('div');
                container.className = 'promo_container_content_body';

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
                """);
        }
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        Browser.SetCookieAsync([]);
    }


    private async void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
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
}