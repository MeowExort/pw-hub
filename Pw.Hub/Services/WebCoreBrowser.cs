using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;
using System.Windows.Threading;
using System.Windows;

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

    private async Task EnsureCoreAndSetBackgroundAsync()
    {
        await _webView.EnsureCoreWebView2Async();
        try
        {
            // Ensure about:blank (and any transparent area) is dark
            _webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30);
        }
        catch
        {
        }
    }

    public Task<string> ExecuteScriptAsync(string script)
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
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
            await EnsureCoreAndSetBackgroundAsync();
            _webView.CoreWebView2.Navigate(url);
        });
    }

    public Task ReloadAsync()
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
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
            await EnsureCoreAndSetBackgroundAsync();
            var cookieManager = _webView.CoreWebView2.CookieManager;
            var coreCookies = await cookieManager.GetCookiesAsync(null);
            return coreCookies.Select(Cookie.FromCoreWebView2Cookie).ToArray();
        });
    }

    public Task SetCookieAsync(Cookie[] cookie)
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
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
            var newWv = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    IsInPrivateModeEnabled = true
                }
            };

            // Prepare new control hidden and dark before it ever becomes visible
            try { newWv.Visibility = Visibility.Hidden; } catch { }
            try { newWv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }

            // Preload hidden into visual tree (old remains visible)
            await _host.PreloadAsync(newWv);

            // Initialize core and enforce dark background again post-init
            try { await newWv.EnsureCoreWebView2Async(); } catch { }
            try { newWv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }

            // Swap internal reference so subsequent Browser calls use the new control
            _webView = newWv;

            // Finalize swap when first navigation of the new control starts, ensuring we never show about:blank
            var finalized = false;
            void EnsureFinalize()
            {
                if (finalized) return;
                finalized = true;
                _ = OnUiAsync(async () =>
                {
                    try { await _host.FinalizeSwapAsync(newWv); } catch { }
                });
            }

            try
            {
                newWv.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    EnsureFinalize();
                };
            }
            catch
            {
                // If we cannot hook event (unlikely), finalize immediately to avoid hidden control
                EnsureFinalize();
            }

            // Safety fallback: finalize after short delay if no navigation started (e.g., caller navigates later)
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                EnsureFinalize();
            });
        });
    }

    public Uri Source => _webView.Source;
}