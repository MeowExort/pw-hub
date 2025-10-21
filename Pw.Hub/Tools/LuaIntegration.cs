using System.Text.Json;
using System.Windows;
using NLua;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;

namespace Pw.Hub.Tools;

public class LuaIntegration
{
    private readonly IAccountManager _accountManager;
    private readonly IBrowser _browser;
    private readonly TaskCompletionSource<string> _tcs;
    private Lua _lua; // keep reference to current Lua state for creating tables
    private Action<string> _printSink; // optional sink for Print routing
    private Action<int, string> _progressSink; // optional sink for progress reporting

    public LuaIntegration(IAccountManager accountManager)
    {
        _accountManager = accountManager;
        _browser = null!; // for legacy usage
    }

    public LuaIntegration(IAccountManager accountManager, IBrowser browser)
    {
        _accountManager = accountManager;
        _browser = browser;
    }

    public LuaIntegration(IAccountManager accountManager, TaskCompletionSource<string> tcs)
    {
        _accountManager = accountManager;
        _tcs = tcs;
        _browser = null!; // not used in this overload
    }

    // Register all methods into NLua state
    public void Register(Lua lua)
    {
        _lua = lua;
        // Callback variants for Account
        lua.RegisterFunction("Account_GetAccountCb", this, GetType().GetMethod(nameof(Account_GetAccountCb), new[] { typeof(LuaFunction) }));
        lua.RegisterFunction("Account_IsAuthorizedCb", this, GetType().GetMethod(nameof(Account_IsAuthorizedCb), new[] { typeof(LuaFunction) }));
        lua.RegisterFunction("Account_GetAccountsJsonCb", this, GetType().GetMethod(nameof(Account_GetAccountsJsonCb), new[] { typeof(LuaFunction) }));
        lua.RegisterFunction("Account_GetAccountsCb", this, GetType().GetMethod(nameof(Account_GetAccountsCb), new[] { typeof(LuaFunction) }));
        lua.RegisterFunction("Account_ChangeAccountCb", this, GetType().GetMethod(nameof(Account_ChangeAccountCb), new[] { typeof(string), typeof(LuaFunction) }));

        // Browser related
        if (_browser != null)
        {
            // Callback variants
            lua.RegisterFunction("Browser_NavigateCb", this, GetType().GetMethod(nameof(Browser_NavigateCb), new[] { typeof(string), typeof(LuaFunction) }));
            lua.RegisterFunction("Browser_ReloadCb", this, GetType().GetMethod(nameof(Browser_ReloadCb), new[] { typeof(LuaFunction) }));
            lua.RegisterFunction("Browser_ExecuteScriptCb", this, GetType().GetMethod(nameof(Browser_ExecuteScriptCb), new[] { typeof(string), typeof(LuaFunction) }));
            lua.RegisterFunction("Browser_ElementExistsCb", this, GetType().GetMethod(nameof(Browser_ElementExistsCb), new[] { typeof(string), typeof(LuaFunction) }));
            lua.RegisterFunction("Browser_WaitForElementCb", this, GetType().GetMethod(nameof(Browser_WaitForElementCb), new[] { typeof(string), typeof(int), typeof(LuaFunction) }));
        }

        // Helpers
        lua.RegisterFunction("Print", this, GetType().GetMethod(nameof(Print)));
        lua.RegisterFunction("DelayCb", this, GetType().GetMethod(nameof(DelayCb), new[] { typeof(int), typeof(LuaFunction) }));

        // Progress reporting helpers
        lua.RegisterFunction("ReportProgress", this, GetType().GetMethod(nameof(ReportProgress), new[] { typeof(int) }));
        lua.RegisterFunction("ReportProgressMsg", this, GetType().GetMethod(nameof(ReportProgressMsg), new[] { typeof(int), typeof(string) }));
    }

    public void SetPrintSink(Action<string> sink)
    {
        _printSink = sink;
    }

    public void SetProgressSink(Action<int, string> sink)
    {
        _progressSink = sink;
    }

    // Helpers
    public void Sleep(int ms) => Thread.Sleep(ms);
    public void Print(string text)
    {
        try
        {
            if (_printSink != null)
            {
                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { _printSink?.Invoke(text); }
                        catch
                        {
                            // ignored
                        }
                    }));
                }
                else
                {
                    try { _printSink?.Invoke(text); }
                    catch
                    {
                        // ignored
                    }
                }
                return;
            }
        }
        catch { }
        // Fallback UI
        MessageBox.Show($"[Lua] {text}");
    }

    // Non-blocking delay helper: schedules callback after ms without freezing UI
    public void DelayCb(int ms, LuaFunction callback)
    {
        try
        {
            Task.Delay(ms).ContinueWith(_ =>
            {
                try { CallLuaVoid(callback); } catch { }
            });
        }
        catch { }
    }

    // Progress reporters
    public void ReportProgress(int percent)
    {
        try
        {
            var sink = _progressSink;
            if (sink == null) return;
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { sink(percent, null); } catch { }
                }));
            }
            else
            {
                try { sink(percent, null); } catch { }
            }
        }
        catch { }
    }

    public void ReportProgressMsg(int percent, string message)
    {
        try
        {
            var sink = _progressSink;
            if (sink == null) return;
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { sink(percent, message); } catch { }
                }));
            }
            else
            {
                try { sink(percent, message); } catch { }
            }
        }
        catch { }
    }

    // Internal helper: ensure Lua callback is executed on UI thread (no return expected)
    private static void CallLuaVoid(LuaFunction callback, params object[] args)
    {
        if (callback == null) return;
        try
        {
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { callback.Call(args); } catch { }
                }));
            }
            else
            {
                // Fallback: call directly
                try { callback.Call(args); } catch { }
            }
        }
        catch { }
    }

    // Internal helper: execute Lua callback on UI thread and capture first return value
    private static object CallLuaWithReturn(LuaFunction callback, params object[] args)
    {
        if (callback == null) return null;
        try
        {
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                return app.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var ret = callback.Call(args);
                        return (ret != null && ret.Length > 0) ? ret[0] : null;
                    }
                    catch
                    {
                        return null;
                    }
                });
            }
            // Fallback if no dispatcher
            try
            {
                var ret = callback.Call(args);
                return (ret != null && ret.Length > 0) ? ret[0] : null;
            }
            catch { return null; }
        }
        catch { return null; }
    }

    // Account API
    public string Account_GetAccount() => _accountManager.GetAccount();
    public bool Account_IsAuthorized() => _accountManager.IsAuthorizedAsync().GetAwaiter().GetResult();
    public string Account_GetAccountsJson()
    {
        var list = _accountManager.GetAccountsAsync().GetAwaiter().GetResult();
        return JsonSerializer.Serialize(list, JsonSerializerOptions.Web);
    }
    public Account[] Account_GetAccounts()
    {
        var list = _accountManager.GetAccountsAsync().GetAwaiter().GetResult();
        return list ?? [];
    }
    public void Account_ChangeAccount(string accountId)
    {
        if (Guid.TryParse(accountId, out var id))
        {
            _accountManager.ChangeAccountAsync(id).GetAwaiter().GetResult();
        }
        else
        {
            throw new ArgumentException("Invalid account id");
        }
    }

    // Browser API
    public void Browser_Navigate(string url) => _browser.NavigateAsync(url).GetAwaiter().GetResult();
    public void Browser_Reload() => _browser.ReloadAsync().GetAwaiter().GetResult();
    public string Browser_ExecuteScript(string script) => _browser.ExecuteScriptAsync(script).GetAwaiter().GetResult();
    public bool Browser_ElementExists(string selector) => _browser.ElementExistsAsync(selector).GetAwaiter().GetResult();
    public bool Browser_WaitForElement(string selector, int timeoutMs) => _browser.WaitForElementExistsAsync(selector, timeoutMs).GetAwaiter().GetResult();
    public string Browser_GetCookiesJson()
    {
        var cookies = _browser.GetCookiesAsync().GetAwaiter().GetResult();
        return JsonSerializer.Serialize(cookies, JsonSerializerOptions.Web);
    }
    public void Browser_SetCookiesJson(string json)
    {
        var cookies = JsonSerializer.Deserialize<Pw.Hub.Models.Cookie[]>(json, JsonSerializerOptions.Web) ?? Array.Empty<Pw.Hub.Models.Cookie>();
        _browser.SetCookieAsync(cookies).GetAwaiter().GetResult();
    }

    // Existing callback support for demo
    public void GetAccountAsyncCallback(Action<string> callback)
    {
        var task = _accountManager.GetAccountAsync();
        task.ContinueWith(t =>
        {
            try
            {
                var acc = t.IsCompletedSuccessfully ? t.Result : string.Empty;
                callback(acc);
            }
            catch
            {
                // ignored
            }
        });
    }

    public void GetAccountAsyncCallback(LuaFunction callback)
    {
        var task = _accountManager.GetAccountAsync();
        task.ContinueWith(t =>
        {
            try
            {
                var acc = t.IsCompletedSuccessfully ? t.Result : string.Empty;
                var first = CallLuaWithReturn(callback, acc)?.ToString() ?? string.Empty;
                _tcs?.TrySetResult(first);
            }
            catch (Exception ex)
            {
                _tcs?.TrySetException(ex);
            }
        });
    }

    // Callback-based non-blocking APIs for Account
    public void Account_GetAccountCb(LuaFunction callback)
    {
        _accountManager.GetAccountAsync().ContinueWith(t =>
        {
            try
            {
                var acc = t.IsCompletedSuccessfully ? t.Result : string.Empty;
                CallLuaVoid(callback, acc);
            }
            catch
            {
                // ignored
            }
        });
    }

    public void Account_IsAuthorizedCb(LuaFunction callback)
    {
        _accountManager.IsAuthorizedAsync().ContinueWith(t =>
        {
            try
            {
                var ok = t.IsCompletedSuccessfully && t.Result;
                CallLuaVoid(callback, ok);
            }
            catch { }
        });
    }

    public void Account_GetAccountsJsonCb(LuaFunction callback)
    {
        _accountManager.GetAccountsAsync().ContinueWith(t =>
        {
            try
            {
                var list = t.IsCompletedSuccessfully ? t.Result : [];
                var json = JsonSerializer.Serialize(list, JsonSerializerOptions.Web);
                CallLuaVoid(callback, json);
            }
            catch { }
        });
    }

    public void Account_GetAccountsCb(LuaFunction callback)
    {
        _accountManager.GetAccountsAsync().ContinueWith((Task<Account[]> t) =>
        {
            try
            {
                var list = t.IsCompletedSuccessfully ? t.Result : Array.Empty<Account>();
                if (_lua != null)
                {
                    // Pack into Lua table to avoid NLua expanding array into multiple arguments
                    var tableName = $"__accounts_{Guid.NewGuid():N}";
                    _lua.NewTable(tableName);
                    var table = (LuaTable)_lua[tableName];
                    int i = 1; // Lua is 1-based
                    foreach (var acc in list)
                    {
                        table[i++] = acc;
                    }
                    CallLuaVoid(callback, table);
                }
                else
                {
                    // Fallback: pass as single object (may expand in NLua)
                    CallLuaVoid(callback, new object[] { list });
                }
            }
            catch { }
        });
    }

    public void Account_ChangeAccountCb(string accountId, LuaFunction callback)
    {
        if (!Guid.TryParse(accountId, out var id))
        {
            CallLuaVoid(callback, false);
            return;
        }
        _accountManager.ChangeAccountAsync(id).ContinueWith(t =>
        {
            try
            {
                var ok = t.IsCompletedSuccessfully && (t.Exception == null);
                CallLuaVoid(callback, ok);
            }
            catch { }
        });
    }

    // Callback-based non-blocking APIs for Browser
    public void Browser_NavigateCb(string url, LuaFunction callback)
    {
        _browser.NavigateAsync(url).ContinueWith(t =>
        {
            try { CallLuaVoid(callback, t.IsCompletedSuccessfully); } catch { }
        });
    }

    public void Browser_ReloadCb(LuaFunction callback)
    {
        _browser.ReloadAsync().ContinueWith(t =>
        {
            try { CallLuaVoid(callback, t.IsCompletedSuccessfully); } catch { }
        });
    }

    public void Browser_ExecuteScriptCb(string script, LuaFunction callback)
    {
        _browser.ExecuteScriptAsync(script).ContinueWith(t =>
        {
            try
            {
                var result = t.IsCompletedSuccessfully ? (t.Result ?? string.Empty) : string.Empty;
                CallLuaVoid(callback, result);
            }
            catch { }
        });
    }

    public void Browser_ElementExistsCb(string selector, LuaFunction callback)
    {
        _browser.ElementExistsAsync(selector).ContinueWith(t =>
        {
            try { CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result); } catch { }
        });
    }

    public void Browser_WaitForElementCb(string selector, int timeoutMs, LuaFunction callback)
    {
        _browser.WaitForElementExistsAsync(selector, timeoutMs).ContinueWith(t =>
        {
            try { CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result); } catch { }
        });
    }

    public void Browser_GetCookiesJsonCb(LuaFunction callback)
    {
        _browser.GetCookiesAsync().ContinueWith(t =>
        {
            try
            {
                var cookies = t.IsCompletedSuccessfully ? t.Result : Array.Empty<Pw.Hub.Models.Cookie>();
                var json = JsonSerializer.Serialize(cookies, JsonSerializerOptions.Web);
                CallLuaVoid(callback, json);
            }
            catch { }
        });
    }

    public void Browser_SetCookiesJsonCb(string json, LuaFunction callback)
    {
        try
        {
            var cookies = JsonSerializer.Deserialize<Pw.Hub.Models.Cookie[]>(json, JsonSerializerOptions.Web) ?? Array.Empty<Pw.Hub.Models.Cookie>();
            _browser.SetCookieAsync(cookies).ContinueWith(t =>
            {
                try { CallLuaVoid(callback, t.IsCompletedSuccessfully); } catch { }
            });
        }
        catch
        {
            try { CallLuaVoid(callback, false); } catch { }
        }
    }

}
