using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;
using System.Windows.Threading;

namespace Pw.Hub.Services;

public class WebCoreBrowser(IWebViewHost host) : IBrowser
{
    private WebView2 _webView = host.Current;
    private readonly IWebViewHost _host = host;

    private async Task<T> OnUiAsync<T>(Func<Task<T>> func)
    {
        if (_webView.Dispatcher.CheckAccess())
            return await func();
        return await _webView.Dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task.Unwrap();
    }

    private async Task OnUiAsync(Func<Task> func)
    {
        if (_webView.Dispatcher.CheckAccess())
        {
            await func();
            return;
        }
        await _webView.Dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task;
    }

    public Task<string> ExecuteScriptAsync(string script)
    {
        return OnUiAsync(async () =>
        {
            await _webView.EnsureCoreWebView2Async();
            var result = await _webView.ExecuteScriptAsync(script);
            return result.Trim('"');
        });
    }

    public Task NavigateAsync(string url)
    {
        return OnUiAsync(async () =>
        {
            var uri = new Uri(url);
            if (uri.Host != "pwonline.ru")
                return;
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Navigate(url);
        });
    }

    public Task ReloadAsync()
    {
        return OnUiAsync(async () =>
        {
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Reload();
        });
    }

    public async Task<bool> ElementExistsAsync(string selector)
    {
        var script = $@"
            (function() {{
                return document.querySelector('{selector}') !== null;
            }})()";
        var result = await ExecuteScriptAsync(script);
        return result.Trim().ToLower() == "true";
    }

    public async Task<bool> WaitForElementExistsAsync(string selector, int timeoutMs = 5000)
    {
        var elapsed = 0;
        var interval = 50;
        while (elapsed < timeoutMs)
        {
            var exists = await ElementExistsAsync(selector);
            if (exists)
                return true;
            await Task.Delay(interval);
            elapsed += interval;
        }

        return false;
    }

    public Task<Cookie[]> GetCookiesAsync()
    {
        return OnUiAsync(async () =>
        {
            await _webView.EnsureCoreWebView2Async();
            var cookieManager = _webView.CoreWebView2.CookieManager;
            var coreCookies = await cookieManager.GetCookiesAsync(null);
            return coreCookies.Select(Cookie.FromCoreWebView2Cookie).ToArray();
        });
    }

    public Task SetCookieAsync(Cookie[] cookie)
    {
        return OnUiAsync(async () =>
        {
            await _webView.EnsureCoreWebView2Async();
            var cookieManager = _webView.CoreWebView2.CookieManager;
            cookieManager.DeleteAllCookies();
            foreach (var c in cookie)
            {
                var coreCookie = cookieManager.CreateCookie(
                    c.Name,
                    c.Value,
                    c.Domain,
                    c.Path);
                coreCookie.Expires = c.Expires;
                coreCookie.IsHttpOnly = c.IsHttpOnly;
                coreCookie.IsSecure = c.IsSecure;
                coreCookie.SameSite = c.SameSite;
                cookieManager.AddOrUpdateCookie(coreCookie);
            }
        });
    }

    public Task CreateNewSessionAsync()
    {
        return OnUiAsync(async () =>
        {
            // Create a completely new WebView2 instance in InPrivate mode and replace the control via host delegate
            var previousUri = _webView?.Source ?? new Uri("https://pwonline.ru");
            var newWv = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    IsInPrivateModeEnabled = true
                },
                Source = previousUri
            };

            // Ask host (UI) to swap controls safely (detach/attach handlers, keep grid placement)
            await _host.ReplaceAsync(newWv);

            // Update internal reference and ensure core is ready
            _webView = newWv;
            try { await _webView.EnsureCoreWebView2Async(); } catch { }
        });
    }

    public Uri Source => _webView.Source;
}