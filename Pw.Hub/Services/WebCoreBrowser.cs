using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class WebCoreBrowser(WebView2 webView) : IBrowser
{
    public async Task<string> ExecuteScriptAsync(string script)
    {
        var result = await webView.ExecuteScriptAsync(script);
        return result.Trim('"');
    }

    public async Task NavigateAsync(string url)
    {
        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.Navigate(url);
    }

    public async Task ReloadAsync()
    {
        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.Reload();
    }

    public async Task<bool> ElementExistsAsync(string selector)
    {
        var script = $@"
            (function() {{
                return document.querySelector('{selector}') !== null;
            }})()";
        var result = await webView.ExecuteScriptAsync(script);
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

    public async Task<Cookie[]> GetCookiesAsync()
    {
        await webView.EnsureCoreWebView2Async();
        var cookieManager = webView.CoreWebView2.CookieManager;
        var coreCookies = await cookieManager.GetCookiesAsync(null);
        return coreCookies.Select(Cookie.FromCoreWebView2Cookie).ToArray();
    }

    public async Task SetCookieAsync(Cookie[] cookie)
    {
        await webView.EnsureCoreWebView2Async();
        var cookieManager = webView.CoreWebView2.CookieManager;
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
    }

    public Uri Source => webView.Source;
}