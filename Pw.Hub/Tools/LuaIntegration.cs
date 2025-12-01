using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Windows;
using NLua;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;
using Pw.Hub;

using Pw.Hub.Infrastructure;

namespace Pw.Hub.Tools;

public class LuaIntegration
{
    // Dedicated single-thread dispatcher for all NLua callbacks for this integration instance
    private sealed class LuaCallbackDispatcher : IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<Action> _queue = new(new System.Collections.Concurrent.ConcurrentQueue<Action>());
        private readonly Thread _thread;
        private volatile bool _stopping;

        public LuaCallbackDispatcher(string name = "LuaWorker")
        {
            _thread = new Thread(ThreadProc)
            {
                IsBackground = true,
                Name = name
            };
            _thread.Start();
        }

        private void ThreadProc()
        {
            try
            {
                foreach (var act in _queue.GetConsumingEnumerable())
                {
                    try { act(); } catch { }
                    if (_stopping && _queue.Count == 0) break;
                }
            }
            catch { }
        }

        public void Post(Action act)
        {
            if (!_queue.IsAddingCompleted)
            {
                try { _queue.Add(act); } catch { }
            }
        }

        public void Dispose()
        {
            try
            {
                _stopping = true;
                _queue.CompleteAdding();
                if (!_thread.Join(500))
                {
                    try { _thread.Interrupt(); } catch { }
                }
            }
            catch { }
        }
    }

    private LuaCallbackDispatcher _luaWorker;
    private readonly IAccountManager _accountManager;
    private readonly IBrowser _browser;
    private readonly HttpClient _client = new();
    private readonly TaskCompletionSource<string> _tcs;
    private Lua _lua; // keep reference to current Lua state for creating tables
    private Action<string> _printSink; // optional sink for Print routing
    private Action<int, string> _progressSink; // optional sink for progress reporting

    private Guid? _runId; // explicit run context for BrowserV2 handle tracking

    public void SetRunId(Guid runId)
    {
        _runId = runId;
        try { Pw.Hub.Infrastructure.RunContextTracker.SetActive(runId); } catch { }
        // Ensure single-threaded Lua worker exists for this run (isolation per integration instance)
        try { _luaWorker ??= new LuaCallbackDispatcher($"LuaWorker-{runId.ToString().Substring(0,8)}"); } catch { }
    }

    // Allow explicit disposal from runner
    public void DisposeWorker()
    {
        try { _luaWorker?.Dispose(); } catch { }
        _luaWorker = null;
    }

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

        var entries = new List<LuaApiRegistry.Entry>();
        void Add(string name, string version, string category, string signature, string description, string snippet)
        {
            entries.Add(new LuaApiRegistry.Entry
            {
                Name = name,
                Version = version,
                Category = category,
                Signature = signature,
                Description = description,
                Snippet = snippet
            });
        }

        // Account (v1 shared)
        lua.RegisterFunction("Account_GetAccountCb", this,
            GetType().GetMethod(nameof(Account_GetAccountCb), new[] { typeof(LuaFunction) }));
        Add("Account_GetAccountCb", "v1", "Account", "Account_GetAccountCb(function(acc) ... end)", "Получить текущий аккаунт в callback(acc)", "Account_GetAccountCb(function(acc)\n    __CURSOR__\nend)");

        lua.RegisterFunction("Account_IsAuthorizedCb", this,
            GetType().GetMethod(nameof(Account_IsAuthorizedCb), new[] { typeof(LuaFunction) }));
        Add("Account_IsAuthorizedCb", "v1", "Account", "Account_IsAuthorizedCb(function(isAuth) ... end)", "Проверить авторизацию аккаунта", "Account_IsAuthorizedCb(function(isAuth)\n    __CURSOR__\nend)");

        lua.RegisterFunction("Account_GetAccountsCb", this,
            GetType().GetMethod(nameof(Account_GetAccountsCb), new[] { typeof(LuaFunction) }));
        Add("Account_GetAccountsCb", "v1", "Account", "Account_GetAccountsCb(function(accounts) ... end)", "Список аккаунтов (таблица)", "Account_GetAccountsCb(function(accounts)\n    __CURSOR__\nend)");

        lua.RegisterFunction("Account_ChangeAccountCb", this,
            GetType().GetMethod(nameof(Account_ChangeAccountCb), new[] { typeof(string), typeof(LuaFunction) }));
        Add("Account_ChangeAccountCb", "v1", "Account", "Account_ChangeAccountCb(accountId, function(ok) ... end)", "Сменить активный аккаунт", "Account_ChangeAccountCb(accountId, function(ok)\n    __CURSOR__\nend)");

        // Browser related (v1 — совместимость: регистрируем только если доступен базовый браузер)
        if (_browser != null)
        {
            lua.RegisterFunction("Browser_NavigateCb", this,
                GetType().GetMethod(nameof(Browser_NavigateCb), new[] { typeof(string), typeof(LuaFunction) }));
            Add("Browser_NavigateCb", "v1", "Browser", "Browser_NavigateCb(url, function() ... end)", "Открыть URL", "Browser_NavigateCb(url, function()\n    __CURSOR__\nend)");

            lua.RegisterFunction("Browser_ReloadCb", this,
                GetType().GetMethod(nameof(Browser_ReloadCb), new[] { typeof(LuaFunction) }));
            Add("Browser_ReloadCb", "v1", "Browser", "Browser_ReloadCb(function() ... end)", "Перезагрузить страницу", "Browser_ReloadCb(function()\n    __CURSOR__\nend)");

            lua.RegisterFunction("Browser_ExecuteScriptCb", this,
                GetType().GetMethod(nameof(Browser_ExecuteScriptCb), new[] { typeof(string), typeof(LuaFunction) }));
            Add("Browser_ExecuteScriptCb", "v1", "Browser", "Browser_ExecuteScriptCb(jsCode, function(result) ... end)", "Выполнить JS и вернуть результат", "Browser_ExecuteScriptCb(jsCode, function(result)\n    __CURSOR__\nend)");

            lua.RegisterFunction("Browser_ElementExistsCb", this,
                GetType().GetMethod(nameof(Browser_ElementExistsCb), new[] { typeof(string), typeof(LuaFunction) }));
            Add("Browser_ElementExistsCb", "v1", "Browser", "Browser_ElementExistsCb(selector, function(exists) ... end)", "Проверить наличие элемента", "Browser_ElementExistsCb(selector, function(exists)\n    __CURSOR__\nend)");

            lua.RegisterFunction("Browser_WaitForElementCb", this,
                GetType().GetMethod(nameof(Browser_WaitForElementCb), new[] { typeof(string), typeof(int), typeof(LuaFunction) }));
            Add("Browser_WaitForElementCb", "v1", "Browser", "Browser_WaitForElementCb(selector, timeoutMs, function(found) ... end)", "Ждать появления элемента", "Browser_WaitForElementCb(selector, timeoutMs, function(found)\n    __CURSOR__\nend)");
        }

        // BrowserV2 — новая версия Lua API для мульти-браузеров (без cookies/фокуса/видимости)
        try
        {
            lua.RegisterFunction("BrowserV2_Create", this,
                GetType().GetMethod(nameof(BrowserV2_Create), new[] { typeof(object), typeof(LuaFunction) }));
            Add("BrowserV2_Create", "v2", "BrowserV2", "BrowserV2_Create(options, function(handle) ... end)", "Создать новый браузер и вернуть дескриптор", "BrowserV2_Create({}, function(handle)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_Close", this,
                GetType().GetMethod(nameof(BrowserV2_Close), new[] { typeof(int), typeof(LuaFunction) }));
            Add("BrowserV2_Close", "v2", "BrowserV2", "BrowserV2_Close(handle, function(ok) ... end)", "Закрыть созданный браузер", "BrowserV2_Close(handle, function(ok)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_Navigate", this,
                GetType().GetMethod(nameof(BrowserV2_Navigate), new[] { typeof(int), typeof(string), typeof(LuaFunction) }));
            Add("BrowserV2_Navigate", "v2", "BrowserV2", "BrowserV2_Navigate(handle, url, function(ok) ... end)", "Открыть URL в указанном браузере", "BrowserV2_Navigate(handle, url, function(ok)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_Reload", this,
                GetType().GetMethod(nameof(BrowserV2_Reload), new[] { typeof(int), typeof(LuaFunction) }));
            Add("BrowserV2_Reload", "v2", "BrowserV2", "BrowserV2_Reload(handle, function(ok) ... end)", "Перезагрузить страницу", "BrowserV2_Reload(handle, function(ok)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_ExecuteScript", this,
                GetType().GetMethod(nameof(BrowserV2_ExecuteScript), new[] { typeof(int), typeof(string), typeof(LuaFunction) }));
            Add("BrowserV2_ExecuteScript", "v2", "BrowserV2", "BrowserV2_ExecuteScript(handle, jsCode, function(result) ... end)", "Выполнить JS в браузере v2", "BrowserV2_ExecuteScript(handle, jsCode, function(result)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_ElementExists", this,
                GetType().GetMethod(nameof(BrowserV2_ElementExists), new[] { typeof(int), typeof(string), typeof(LuaFunction) }));
            Add("BrowserV2_ElementExists", "v2", "BrowserV2", "BrowserV2_ElementExists(handle, selector, function(exists) ... end)", "Проверить наличие элемента", "BrowserV2_ElementExists(handle, selector, function(exists)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_WaitForElement", this,
                GetType().GetMethod(nameof(BrowserV2_WaitForElement), new[] { typeof(int), typeof(string), typeof(int), typeof(LuaFunction) }));
            Add("BrowserV2_WaitForElement", "v2", "BrowserV2", "BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found) ... end)", "Ждать появления элемента", "BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_ChangeAccount", this,
                GetType().GetMethod(nameof(BrowserV2_ChangeAccount), new[] { typeof(int), typeof(string), typeof(LuaFunction) }));
            Add("BrowserV2_ChangeAccount", "v2", "BrowserV2", "BrowserV2_ChangeAccount(handle, accountId, function(ok) ... end)", "Сменить аккаунт для указанного браузера v2", "BrowserV2_ChangeAccount(handle, accountId, function(ok)\n    __CURSOR__\nend)");

            lua.RegisterFunction("BrowserV2_GetCurrentAccount", this,
                GetType().GetMethod(nameof(BrowserV2_GetCurrentAccount), new[] { typeof(int), typeof(LuaFunction) }));
            Add("BrowserV2_GetCurrentAccount", "v2", "BrowserV2", "BrowserV2_GetCurrentAccount(handle, function(acc) ... end)", "Вернуть Lua‑таблицу текущего аккаунта", "BrowserV2_GetCurrentAccount(handle, function(acc)\n    __CURSOR__\nend)");
        }
        catch { }

        // Helpers
        lua.RegisterFunction("Print", this, GetType().GetMethod(nameof(Print)));
        Add("Print", "v1", "Helpers", "Print(value)", "Вывести текст в лог редактора", "Print('text')");

        lua.RegisterFunction("DelayCb", this,
            GetType().GetMethod(nameof(DelayCb), new[] { typeof(int), typeof(LuaFunction) }));
        Add("DelayCb", "v1", "Helpers", "DelayCb(ms, function() ... end)", "Задержка с колбэком", "DelayCb(1000, function()\n    __CURSOR__\nend)");

        // Progress reporting helpers
        lua.RegisterFunction("ReportProgress", this,
            GetType().GetMethod(nameof(ReportProgress), new[] { typeof(int) }));
        Add("ReportProgress", "v1", "Helpers", "ReportProgress(percent)", "Обновить прогресс выполнения", "ReportProgress(50)");

        lua.RegisterFunction("ReportProgressMsg", this,
            GetType().GetMethod(nameof(ReportProgressMsg), new[] { typeof(int), typeof(string) }));
        Add("ReportProgressMsg", "v1", "Helpers", "ReportProgressMsg(percent, message)", "Обновить прогресс с сообщением", "ReportProgressMsg(25, 'Старт')");

        // Net api
        lua.RegisterFunction("Net_PostJsonCb", this,
            GetType().GetMethod(nameof(NetPostJsonSb),
                new[] { typeof(string), typeof(string), typeof(string), typeof(LuaFunction) }));
        Add("Net_PostJsonCb", "v1", "Net", "Net_PostJsonCb(url, jsonBody, contentType, function(res) ... end)", "HTTP POST JSON, вернуть ответ в res", "Net_PostJsonCb(url, json, 'application/json', function(res)\n    __CURSOR__\nend)");

        // Telegram API
        lua.RegisterFunction("Telegram_SendMessageCb", this,
            GetType().GetMethod(nameof(Telegram_SendMessageCb), new[] { typeof(string), typeof(LuaFunction) }));
        Add("Telegram_SendMessageCb", "v1", "Telegram", "Telegram_SendMessageCb(text, function(ok) ... end)", "Отправить сообщение в Telegram", "Telegram_SendMessageCb('текст', function(ok)\n    __CURSOR__\nend)");

        // Publish registry for editor/AI
        try { LuaApiRegistry.ReplaceAll(entries); } catch { }
    }

    public void SetPrintSink(Action<string> sink)
    {
        _printSink = sink;
    }

    public void SetProgressSink(Action<int, string> sink)
    {
        _progressSink = sink;
    }

    [LuaApiFunction(Name="Print", Version="v1", Category="Helpers", Signature="Print(value)", Description="Вывести текст в лог редактора", Snippet="Print('text')")]
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
                        try
                        {
                            _printSink?.Invoke(text);
                        }
                        catch
                        {
                            // ignored
                        }
                    }));
                }
                else
                {
                    try
                    {
                        _printSink?.Invoke(text);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return;
            }
        }
        catch
        {
        }

        // Fallback UI
        MessageBox.Show($"[Lua] {text}");
    }

    // Non-blocking delay helper: schedules callback after ms without freezing UI
    [LuaApiFunction(Name="DelayCb", Version="v1", Category="Helpers", Signature="DelayCb(ms, function() ... end)", Description="Задержка с колбэком", Snippet="DelayCb(1000, function()\n    __CURSOR__\nend)")]
    public void DelayCb(int ms, LuaFunction callback)
    {
        try
        {
            Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
            Task.Delay(ms).ContinueWith(_ =>
            {
                try
                {
                    try { CallLuaVoid(callback); } finally { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); }
                }
                catch
                {
                    try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { }
                }
            });
        }
        catch
        {
            try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { }
        }
    }

    // Progress reporters
    [LuaApiFunction(Name="ReportProgress", Version="v1", Category="Helpers", Signature="ReportProgress(percent)", Description="Обновить прогресс выполнения", Snippet="ReportProgress(50)")]
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
                    try
                    {
                        sink(percent, null);
                    }
                    catch
                    {
                    }
                }));
            }
            else
            {
                try
                {
                    sink(percent, null);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    [LuaApiFunction(Name="ReportProgressMsg", Version="v1", Category="Helpers", Signature="ReportProgressMsg(percent, message)", Description="Обновить прогресс с сообщением", Snippet="ReportProgressMsg(25, 'Старт')")]
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
                    try
                    {
                        sink(percent, message);
                    }
                    catch
                    {
                    }
                }));
            }
            else
            {
                try
                {
                    sink(percent, message);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    // UI thread helpers
    private static void UiInvoke(Action action)
    {
        try
        {
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.Invoke(() =>
                {
                    try { action(); } catch { }
                });
            }
            else
            {
                try { action(); } catch { }
            }
        }
        catch { }
    }

    private static T UiInvoke<T>(Func<T> func)
    {
        try
        {
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                return app.Dispatcher.Invoke(() =>
                {
                    try { return func(); } catch { return default; }
                });
            }

            try { return func(); } catch { return default; }
        }
        catch { return default; }
    }

    // Internal helper: execute Lua callback on a dedicated Lua worker thread (no return expected)
    private void CallLuaVoid(LuaFunction callback, params object[] args)
    {
        if (callback == null) return;
        try
        {
            var worker = _luaWorker;
            if (worker != null)
            {
                worker.Post(() => { try { callback.Call(args); } catch { } });
            }
            else
            {
                // Fallback: call synchronously (still single thread from caller)
                try { callback.Call(args); } catch { }
            }
        }
        catch
        {
        }
    }

    // Internal helper: execute Lua callback on the dedicated Lua worker and capture first return value
    private object CallLuaWithReturn(LuaFunction callback, params object[] args)
    {
        if (callback == null) return null;
        object result = null;
        var ev = new System.Threading.ManualResetEventSlim(false);
        try
        {
            var worker = _luaWorker;
            if (worker != null)
            {
                worker.Post(() =>
                {
                    try
                    {
                        var ret = callback.Call(args);
                        result = (ret != null && ret.Length > 0) ? ret[0] : null;
                    }
                    catch { }
                    finally { try { ev.Set(); } catch { } }
                });
                // Wait with a sane timeout to avoid hangs
                ev.Wait(5000);
                return result;
            }
            else
            {
                // Fallback: call synchronously on current thread
                try
                {
                    var ret = callback.Call(args);
                    return (ret != null && ret.Length > 0) ? ret[0] : null;
                }
                catch { return null; }
            }
        }
        catch { return null; }
        finally { try { ev.Dispose(); } catch { } }
    }

    // Public converter for passing .NET values into Lua args table
    public object ConvertToLuaValue(object value)
    {
        try
        {
            if (value == null) return null;
            if (value is Squad squad)
                return ToLuaSquad(squad);
            if (value is Account account)
                return ToLuaAccount(account, includeSquad: true);
            // Handle collections (IEnumerable)
            if (value is System.Collections.IEnumerable en && value is not string)
            {
                var table = CreateLuaTable("list");
                int i = 1;
                foreach (var item in en)
                {
                    object converted;
                    if (item is Squad sq)
                        converted = ToLuaSquad(sq);
                    else if (item is Account acc)
                        converted = ToLuaAccount(acc, includeSquad: true);
                    else
                        converted = item;
                    table[i++] = converted;
                }
                return table;
            }
            // pass through other primitive and complex types; NLua can marshal common CLR types
            return value;
        }
        catch
        {
            return null;
        }
    }

    // Callback-based non-blocking APIs for Account
    [LuaApiFunction(Name="Account_GetAccountCb", Version="v1", Category="Account", Signature="Account_GetAccountCb(function(acc) ... end)", Description="Получить текущий аккаунт в callback(acc)", Snippet="Account_GetAccountCb(function(acc)\n    __CURSOR__\nend)")]
    public void Account_GetAccountCb(LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _accountManager.GetAccountAsync().ContinueWith(t =>
        {
            try
            {
                var acc = t.IsCompletedSuccessfully ? t.Result : null;
                CallLuaVoid(callback, acc);
            }
            catch
            {
                // ignored
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    [LuaApiFunction(Name="Account_IsAuthorizedCb", Version="v1", Category="Account", Signature="Account_IsAuthorizedCb(function(isAuth) ... end)", Description="Проверить авторизацию аккаунта", Snippet="Account_IsAuthorizedCb(function(isAuth)\n    __CURSOR__\nend)")]
    public void Account_IsAuthorizedCb(LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _accountManager.IsAuthorizedAsync().ContinueWith(t =>
        {
            try
            {
                var ok = t.IsCompletedSuccessfully && t.Result;
                CallLuaVoid(callback, ok);
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    [LuaApiFunction(Name="Account_GetAccountsCb", Version="v1", Category="Account", Signature="Account_GetAccountsCb(function(accounts) ... end)", Description="Список аккаунтов (таблица)", Snippet="Account_GetAccountsCb(function(accounts)\n    __CURSOR__\nend)")]
    public void Account_GetAccountsCb(LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _accountManager.GetAccountsAsync().ContinueWith((Task<Account[]> t) =>
        {
            try
            {
                var list = t.IsCompletedSuccessfully ? t.Result : Array.Empty<Account>();
                if (_lua != null)
                {
                    // Build Lua table of accounts with nested Servers and Characters as Lua tables
                    var accountsTable = CreateLuaTable("accounts");
                    int i = 1; // Lua is 1-based
                    foreach (var acc in list)
                    {
                        // Include Squad (slim, without Accounts) for each account
                        accountsTable[i++] = ToLuaAccount(acc, includeSquad: true);
                    }

                    CallLuaVoid(callback, accountsTable);
                }
                else
                {
                    // Fallback: pass as single object (may expand in NLua)
                    CallLuaVoid(callback, new object[] { list });
                }
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    private LuaTable CreateLuaTable(string prefix)
    {
        return UiInvoke(() =>
        {
            if (_lua == null) return null;
            var name = $"__{prefix}_{Guid.NewGuid():N}";
            _lua.NewTable(name);
            var table = (LuaTable)_lua[name];
            // Remove the temporary global reference to avoid polluting _G (and debugger Globals)
            try
            {
                _lua[name] = null;
            }
            catch
            {
            }

            return table;
        });
    }

    private LuaTable ToLuaAccount(Account acc)
    {
        return ToLuaAccount(acc, includeSquad: false);
    }

    private LuaTable ToLuaAccount(Account acc, bool includeSquad)
    {
        var t = CreateLuaTable("account");
        try
        {
            // Primitive/simple fields
            t["Id"] = acc?.Id;
            t["Name"] = acc?.Name;
            t["SiteId"] = acc?.SiteId;
            t["ImageSource"] = acc?.ImageSource;
            t["LastVisit"] = acc?.LastVisit.ToString("o"); // ISO string for DateTime
            t["ImageUri"] = acc?.ImageUri?.ToString();
            t["SquadId"] = acc?.SquadId; // avoid ToString on null
            t["OrderIndex"] = acc?.OrderIndex;

            // Optional: include slim Squad info without Accounts to avoid cycles
            if (includeSquad)
            {
                var squadSlim = CreateLuaTable("squad");
                try
                {
                    if (acc?.Squad != null)
                    {
                        squadSlim["Id"] = acc.Squad.Id;
                        squadSlim["Name"] = acc.Squad.Name;
                        squadSlim["OrderIndex"] = acc.Squad.OrderIndex;
                    }
                    else
                    {
                        // If only SquadId is known, return minimal stub
                        squadSlim["Id"] = acc?.SquadId;
                    }
                }
                catch { }
                t["Squad"] = squadSlim;
            }

            // Servers -> table of server tables
            var serversTable = CreateLuaTable("servers");
            int i = 1;
            if (acc?.Servers != null)
            {
                foreach (var s in acc.Servers)
                {
                    serversTable[i++] = ToLuaServer(s);
                }
            }

            t["Servers"] = serversTable;
        }
        catch
        {
        }

        return t;
    }

    private LuaTable ToLuaServer(AccountServer s)
    {
        var t = CreateLuaTable("server");
        try
        {
            t["Id"] = s?.Id;
            t["OptionId"] = s?.OptionId;
            t["Name"] = s?.Name;
            t["DefaultCharacterOptionId"] = s?.DefaultCharacterOptionId;
            t["AccountId"] = s?.AccountId;

            // Characters -> table of character tables
            var charsTable = CreateLuaTable("characters");
            int i = 1;
            if (s?.Characters != null)
            {
                foreach (var c in s.Characters)
                {
                    charsTable[i++] = ToLuaCharacter(c);
                }
            }

            t["Characters"] = charsTable;
        }
        catch
        {
        }

        return t;
    }

    private LuaTable ToLuaCharacter(AccountCharacter c)
    {
        var t = CreateLuaTable("character");
        try
        {
            t["Id"] = c?.Id;
            t["OptionId"] = c?.OptionId;
            t["Name"] = c?.Name;
            t["ServerId"] = c?.ServerId;
            // Avoid including back-reference to Server to prevent deep cycles
        }
        catch
        {
        }

        return t;
    }

    private LuaTable ToLuaSquad(Squad s)
    {
        return ToLuaSquad(s, includeAccounts: true);
    }

    private LuaTable ToLuaSquad(Squad s, bool includeAccounts)
    {
        var t = CreateLuaTable("squad");
        try
        {
            t["Id"] = s?.Id;
            t["Name"] = s?.Name;
            t["OrderIndex"] = s?.OrderIndex;

            if (includeAccounts)
            {
                // Include Accounts with nested Servers and Characters
                var accountsTable = CreateLuaTable("accounts");
                int i = 1;
                if (s?.Accounts != null)
                {
                    foreach (var acc in s.Accounts)
                    {
                        // For accounts inside squad, include a slim Squad reference (without Accounts)
                        accountsTable[i++] = ToLuaAccount(acc, includeSquad: true);
                    }
                }
                t["Accounts"] = accountsTable;
            }
        }
        catch
        {
        }

        return t;
    }

    [LuaApiFunction(Name="Account_ChangeAccountCb", Version="v1", Category="Account", Signature="Account_ChangeAccountCb(accountId, function(ok) ... end)", Description="Сменить активный аккаунт", Snippet="Account_ChangeAccountCb(accountId, function(ok)\n    __CURSOR__\nend)")]
    public void Account_ChangeAccountCb(string accountId, LuaFunction callback)
    {
        // Политика V1: создаём НОВУЮ сессию через перегрузку AccountManager.ChangeAccountAsync с опциями
        // (атомарно внутри менеджера: новая сессия -> куки -> навигация -> проверка авторизации).
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        Task.Run(async () =>
        {
            try
            {
                try { System.Diagnostics.Debug.WriteLine($"[LuaV1] Switch -> ChangeAccountAsync(opts: CreateFreshSession=TRUE, SeparateProfile), accountId={accountId}"); } catch { }

                var am = _accountManager as Pw.Hub.Services.AccountManager;
                if (am != null)
                {
                    var opts = new Pw.Hub.Services.AccountSwitchOptions(true, BrowserSessionIsolationMode.SeparateProfile);
                    await am.ChangeAccountAsync(accountId, opts);
                }
                else
                {
                    // Фолбэк на старую схему, если IAccountManager не наш тип
                    if (_browser != null)
                    {
                        try { await _browser.CreateNewSessionAsync(BrowserSessionIsolationMode.SeparateProfile); } catch { }
                    }
                    await _accountManager.ChangeAccountAsync(accountId);
                }

                try { System.Diagnostics.Debug.WriteLine($"[LuaV1] Switch -> ChangeAccountAsync done, accountId={accountId}"); } catch { }
                CallLuaVoid(callback, true);
            }
            catch (Exception ex)
            {
                try { System.Diagnostics.Debug.WriteLine($"[LuaV1] Switch FAILED: {ex.Message}"); } catch { }
                try { CallLuaVoid(callback, false); } catch { }
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    // Callback-based non-blocking APIs for Browser
    [LuaApiFunction(Name="Browser_NavigateCb", Version="v1", Category="Browser", Signature="Browser_NavigateCb(url, function() ... end)", Description="Открыть URL", Snippet="Browser_NavigateCb(url, function()\n    __CURSOR__\nend)")]
    public void Browser_NavigateCb(string url, LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _browser.NavigateAsync(url).ContinueWith(t =>
        {
            try
            {
                CallLuaVoid(callback, t.IsCompletedSuccessfully);
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    [LuaApiFunction(Name="Browser_ReloadCb", Version="v1", Category="Browser", Signature="Browser_ReloadCb(function() ... end)", Description="Перезагрузить страницу", Snippet="Browser_ReloadCb(function()\n    __CURSOR__\nend)")]
    public void Browser_ReloadCb(LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _browser.ReloadAsync().ContinueWith(t =>
        {
            try
            {
                CallLuaVoid(callback, t.IsCompletedSuccessfully);
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    [LuaApiFunction(Name="Browser_ExecuteScriptCb", Version="v1", Category="Browser", Signature="Browser_ExecuteScriptCb(jsCode, function(result) ... end)", Description="Выполнить JS и вернуть результат", Snippet="Browser_ExecuteScriptCb(jsCode, function(result)\n    __CURSOR__\nend)")]
    public void Browser_ExecuteScriptCb(string script, LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _browser.ExecuteScriptAsync(script).ContinueWith(t =>
        {
            try
            {
                var result = t.IsCompletedSuccessfully ? (t.Result ?? string.Empty) : string.Empty;
                CallLuaVoid(callback, result);
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    [LuaApiFunction(Name="Browser_ElementExistsCb", Version="v1", Category="Browser", Signature="Browser_ElementExistsCb(selector, function(exists) ... end)", Description="Проверить наличие элемента", Snippet="Browser_ElementExistsCb(selector, function(exists)\n    __CURSOR__\nend)")]
    public void Browser_ElementExistsCb(string selector, LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _browser.ElementExistsAsync(selector).ContinueWith(t =>
        {
            try
            {
                CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result);
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    [LuaApiFunction(Name="Browser_WaitForElementCb", Version="v1", Category="Browser", Signature="Browser_WaitForElementCb(selector, timeoutMs, function(found) ... end)", Description="Ждать появления элемента", Snippet="Browser_WaitForElementCb(selector, timeoutMs, function(found)\n    __CURSOR__\nend)")]
    public void Browser_WaitForElementCb(string selector, int timeoutMs, LuaFunction callback)
    {
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        _browser.WaitForElementExistsAsync(selector, timeoutMs).ContinueWith(t =>
        {
            try
            {
                CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result);
            }
            catch
            {
            }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    /// <summary>
    /// Асинхронно отправляет сообщение пользователю в Telegram через Modules API и вызывает Lua-колбэк с флагом успеха.
    /// Использование из Lua:
    ///   Telegram_SendMessageCb("текст", function(ok)
    ///       if ok then Print('Отправлено') else Print('Ошибка отправки') end
    ///   end)
    /// </summary>
    [LuaApiFunction(Name="Telegram_SendMessageCb", Version="v1", Category="Telegram", Signature="Telegram_SendMessageCb(text, function(ok) ... end)", Description="Отправить сообщение в Telegram", Snippet="Telegram_SendMessageCb('текст', function(ok)\n    __CURSOR__\nend)")]
    public void Telegram_SendMessageCb(string text, LuaFunction callback)
    {
        try
        {
            var msg = (text ?? string.Empty).Trim();
            if (msg.Length == 0)
            {
                CallLuaVoid(callback, false);
                return;
            }
            var api = new Pw.Hub.Services.ModulesApiClient();
            Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
            api.SendTelegramMessageAsync(msg).ContinueWith(t =>
            {
                try
                {
                    var ok = t.IsCompletedSuccessfully && t.Result;
                    CallLuaVoid(callback, ok);
                }
                catch { }
                finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
            });
        }
        catch
        {
            try { CallLuaVoid(callback, false); } catch { }
        }
    }

    [LuaApiFunction(Name="Net_PostJsonCb", Version="v1", Category="Net", Signature="Net_PostJsonCb(url, jsonBody, contentType, function(res) ... end)", Description="HTTP POST JSON, вернуть ответ в res", Snippet="Net_PostJsonCb(url, json, 'application/json', function(res)\n    __CURSOR__\nend)")]
    public void NetPostJsonSb(string url, string jsonBody, string contentType, LuaFunction callback)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var content = new StringContent(jsonBody, Encoding.UTF8, contentType);
        request.Content = content;
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);

        void DisposeAll()
        {
            try { content.Dispose(); } catch { }
            try { request.Dispose(); } catch { }
            try { client.Dispose(); } catch { }
        }

        object CreateResponseContainer()
        {
            if (_lua != null)
            {
                return CreateLuaTable("response");
            }
            // Fallback when _lua is not set: use a .NET dictionary
            return new Dictionary<string, object?>();
        }

        void SetField(object tableOrDict, string key, object? value)
        {
            try
            {
                if (tableOrDict is LuaTable lt)
                {
                    lt[key] = value;
                }
                else if (tableOrDict is IDictionary<string, object?> ds)
                {
                    ds[key] = value;
                }
                else if (tableOrDict is System.Collections.IDictionary d)
                {
                    d[key] = value;
                }
            }
            catch { }
        }

        client.SendAsync(request).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode)
            {
                t.Result.Content.ReadAsStringAsync().ContinueWith(r =>
                {
                    try
                    {
                        var res = CreateResponseContainer();
                        SetField(res, "Success", t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode);
                        SetField(res, "ResponseBody", r.IsCompletedSuccessfully ? r.Result : "");
                        SetField(res, "Error", null);
                        CallLuaVoid(callback, res);
                    }
                    finally
                    {
                        DisposeAll();
                        try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { }
                    }
                });
            }
            else
            {
                var res = CreateResponseContainer();
                SetField(res, "Success", t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode);
                SetField(res, "ResponseBody", null);
                SetField(res, "Error", t.IsCompletedSuccessfully ? t.Result.ReasonPhrase : (t.IsFaulted ? t.Exception?.GetBaseException()?.Message : "Cancelled"));
                CallLuaVoid(callback, res);
                DisposeAll();
            }
        });
    }

    #region Lua API v2: многобраузерный интерфейс без cookies/фокуса/видимости

    // Вспомогательный метод: получить BrowserManager из главного окна
    private Services.BrowserManager? GetBrowserManager()
    {
        try
        {
            var app = Application.Current;
            if (app == null) return null;

            // Доступ к MainWindow всегда выполняем через Dispatcher, чтобы избежать проблем с потоками UI
            MainWindow mw = null;
            if (app.Dispatcher?.CheckAccess() == true)
            {
                mw = app.MainWindow as MainWindow ?? app.Windows?.OfType<MainWindow>()?.FirstOrDefault();
            }
            else
            {
                mw = app.Dispatcher?.Invoke(() => app.MainWindow as MainWindow
                                              ?? app.Windows?.OfType<MainWindow>()?.FirstOrDefault());
            }
            if (mw == null) return null;

            // Если менеджер уже инициализирован в окне — возвращаем его
            var mgr = mw.BrowserManager;
            if (mgr != null) return mgr;

            // Фолбэк: окно создано, но менеджер ещё не сконструирован (или занулён).
            // Пытаемся создать и присвоить через приватный setter с помощью рефлексии.
            try
            {
                var newMgr = new Services.BrowserManager(mw);
                var prop = typeof(MainWindow).GetProperty("BrowserManager",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var setMethod = prop?.GetSetMethod(true);
                setMethod?.Invoke(mw, new object[] { newMgr });
                return newMgr;
            }
            catch
            {
                // В крайнем случае возвращаем null — вызов в Lua получит 0/false и не упадёт.
                return null;
            }
        }
        catch { return null; }
    }

    /// <summary>
    /// BrowserV2.Create(options, cb) — создаёт новый браузер, добавляет его в рабочее пространство и возвращает дескриптор (int handle).
    /// options — Lua-таблица (необязательно): { StartUrl = "https://pwonline.ru/" }
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_Create", Version="v2", Category="BrowserV2", Signature="BrowserV2_Create(options, function(handle) ... end)", Description="Создать новый браузер и вернуть дескриптор", Snippet="BrowserV2_Create({}, function(handle)\n    __CURSOR__\nend)")]
    public void BrowserV2_Create(object options, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null)
        {
            CallLuaVoid(callback, 0); // 0 = ошибка
            return;
        }

        var opts = new Services.BrowserManager.CreateOptions();
        try
        {
            if (options is LuaTable t)
            {
                var startUrl = t["StartUrl"] as string;
                if (!string.IsNullOrWhiteSpace(startUrl)) opts.StartUrl = startUrl;
                var proxy = t["Proxy"] as string;
                if (!string.IsNullOrWhiteSpace(proxy)) opts.Proxy = proxy;
            }
        }
        catch { }

        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        mgr.CreateAsync(opts).ContinueWith((Task<int> t) =>
        {
            try
            {
                var handle = t.IsCompletedSuccessfully ? t.Result : 0;
                if (handle > 0)
                {
                    try
                    {
                        if (_runId.HasValue)
                            Pw.Hub.Infrastructure.RunContextTracker.RegisterHandle(handle, _runId.Value);
                        else
                            Pw.Hub.Infrastructure.RunContextTracker.RegisterHandle(handle);
                    }
                    catch { }
                }
                CallLuaVoid(callback, handle);
            }
            catch { }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    /// <summary>
    /// BrowserV2.Close(handle, cbOk)
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_Close", Version="v2", Category="BrowserV2", Signature="BrowserV2_Close(handle, function(ok) ... end)", Description="Закрыть созданный браузер", Snippet="BrowserV2_Close(handle, function(ok)\n    __CURSOR__\nend)")]
    public void BrowserV2_Close(int handle, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null)
        {
            CallLuaVoid(callback, false);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        var ok = mgr.Close(handle);
        try
        {
            if (ok)
            {
                if (_runId.HasValue)
                    Pw.Hub.Infrastructure.RunContextTracker.UnregisterHandle(handle, _runId.Value);
                else
                    Pw.Hub.Infrastructure.RunContextTracker.UnregisterHandle(handle);
            }
        }
        catch { }
        finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        CallLuaVoid(callback, ok);
    }

    /// <summary>
    /// BrowserV2.Navigate(handle, url, cbOk)
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_Navigate", Version="v2", Category="BrowserV2", Signature="BrowserV2_Navigate(handle, url, function(ok) ... end)", Description="Открыть URL в указанном браузере", Snippet="BrowserV2_Navigate(handle, url, function(ok)\n    __CURSOR__\nend)")]
    public void BrowserV2_Navigate(int handle, string url, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var browser, out var _))
        {
            CallLuaVoid(callback, false);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        browser.NavigateAsync(url).ContinueWith(t => { try { CallLuaVoid(callback, t.IsCompletedSuccessfully); } catch { } finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } } });
    }

    /// <summary>
    /// BrowserV2.Reload(handle, cbOk)
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_Reload", Version="v2", Category="BrowserV2", Signature="BrowserV2_Reload(handle, function(ok) ... end)", Description="Перезагрузить страницу", Snippet="BrowserV2_Reload(handle, function(ok)\n    __CURSOR__\nend)")]
    public void BrowserV2_Reload(int handle, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var browser, out var _))
        {
            CallLuaVoid(callback, false);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        browser.ReloadAsync().ContinueWith(t => { try { CallLuaVoid(callback, t.IsCompletedSuccessfully); } catch { } finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } } });
    }

    /// <summary>
    /// BrowserV2.ExecuteScript(handle, script, cbResult)
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_ExecuteScript", Version="v2", Category="BrowserV2", Signature="BrowserV2_ExecuteScript(handle, jsCode, function(result) ... end)", Description="Выполнить JS в браузере v2", Snippet="BrowserV2_ExecuteScript(handle, jsCode, function(result)\n    __CURSOR__\nend)")]
    public void BrowserV2_ExecuteScript(int handle, string script, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var browser, out var _))
        {
            CallLuaVoid(callback, string.Empty);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        browser.ExecuteScriptAsync(script).ContinueWith(t =>
        {
            try
            {
                var res = t.IsCompletedSuccessfully ? t.Result ?? string.Empty : string.Empty;
                CallLuaVoid(callback, res);
            }
            catch { }
            finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } }
        });
    }

    /// <summary>
    /// BrowserV2.ElementExists(handle, selector, cbBool)
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_ElementExists", Version="v2", Category="BrowserV2", Signature="BrowserV2_ElementExists(handle, selector, function(exists) ... end)", Description="Проверить наличие элемента", Snippet="BrowserV2_ElementExists(handle, selector, function(exists)\n    __CURSOR__\nend)")]
    public void BrowserV2_ElementExists(int handle, string selector, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var browser, out var _))
        {
            CallLuaVoid(callback, false);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        browser.ElementExistsAsync(selector).ContinueWith(t => { try { CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result); } catch { } finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } } });
    }

    /// <summary>
    /// BrowserV2.WaitForElement(handle, selector, timeoutMs, cbBool)
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_WaitForElement", Version="v2", Category="BrowserV2", Signature="BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found) ... end)", Description="Ждать появления элемента", Snippet="BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found)\n    __CURSOR__\nend)")]
    public void BrowserV2_WaitForElement(int handle, string selector, int timeoutMs, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var browser, out var _))
        {
            CallLuaVoid(callback, false);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        browser.WaitForElementExistsAsync(selector, timeoutMs).ContinueWith(t => { try { CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result); } catch { } finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } } });
    }

    /// <summary>
    /// BrowserV2.ChangeAccount(handle, accountId, cbOk) — меняет аккаунт для конкретного браузера v2.
    /// Всегда создаётся новая InPrivate-сессия (см. AccountManager.EnsureNewSessionBeforeSwitchAsync).
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_ChangeAccount", Version="v2", Category="BrowserV2", Signature="BrowserV2_ChangeAccount(handle, accountId, function(ok) ... end)", Description="Сменить аккаунт для указанного браузера v2", Snippet="BrowserV2_ChangeAccount(handle, accountId, function(ok)\n    __CURSOR__\nend)")]
    public void BrowserV2_ChangeAccount(int handle, string accountId, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var _, out var am))
        {
            CallLuaVoid(callback, false);
            return;
        }
        Pw.Hub.Infrastructure.RunLifetimeTracker.BeginOp(_runId);
        am.ChangeAccountAsync(accountId).ContinueWith(t => { try { CallLuaVoid(callback, t.IsCompletedSuccessfully && (t.Exception == null)); } catch { } finally { try { Pw.Hub.Infrastructure.RunLifetimeTracker.EndOp(_runId); } catch { } } });
    }

    /// <summary>
    /// BrowserV2.GetCurrentAccount(handle, cbAccountTable) — возвращает Lua-таблицу с данными текущего аккаунта данного браузера.
    /// </summary>
    [LuaApiFunction(Name="BrowserV2_GetCurrentAccount", Version="v2", Category="BrowserV2", Signature="BrowserV2_GetCurrentAccount(handle, function(acc) ... end)", Description="Вернуть Lua‑таблицу текущего аккаунта", Snippet="BrowserV2_GetCurrentAccount(handle, function(acc)\n    __CURSOR__\nend)")]
    public void BrowserV2_GetCurrentAccount(int handle, LuaFunction callback)
    {
        var mgr = GetBrowserManager();
        if (mgr == null || !mgr.TryGet(handle, out var _, out var am))
        {
            CallLuaVoid(callback, null);
            return;
        }
        am.GetAccountAsync().ContinueWith(t =>
        {
            try
            {
                var acc = t.IsCompletedSuccessfully ? t.Result : null;
                var table = ToLuaAccount(acc, includeSquad: true);
                CallLuaVoid(callback, table);
            }
            catch { }
        });
    }

    #endregion
}

