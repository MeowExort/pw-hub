using System.Text;
using System.Windows;
using System.IO;
using NLua;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;

namespace Pw.Hub.Tools;

public class LuaScriptRunner
{
    private readonly IAccountManager _accountManager;
    private readonly IBrowser _browser;

    private Lua _currentLua;
    private TaskCompletionSource<string> _currentTcs;
    private readonly LuaIntegration _integration;

    public LuaScriptRunner(IAccountManager accountManager, IBrowser browser)
    {
        _accountManager = accountManager;
        _browser = browser;
        _integration = new LuaIntegration(_accountManager, _browser);
    }

    public void SetPrintSink(Action<string> sink)
    {
        _integration.SetPrintSink(sink);
    }

    public void SetProgressSink(Action<int, string> sink)
    {
        _integration.SetProgressSink(sink);
    }

    public async Task RunAsync(string scriptFileName, string selectedAccountId = null)
    {
        // Load script text from embedded Scripts folder in output directory
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptPath = Path.Combine(baseDir, "Scripts", scriptFileName);
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Скрипт {scriptFileName} не найден по пути: {scriptPath}", "Lua", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var code = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);
            await RunCodeAsync(code, selectedAccountId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка во время выполнения Lua-скрипта: {ex.Message}", "Lua", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public Task RunCodeAsync(string code, string selectedAccountId = null)
    {
        try
        {
            using var lua = new Lua();
            lua.State.Encoding = Encoding.UTF8;
            _integration.Register(lua);

            if (!string.IsNullOrWhiteSpace(selectedAccountId))
                lua["selectedAccountId"] = selectedAccountId;
            else
                lua["selectedAccountId"] = null;

            lua.DoString(code);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка во время выполнения Lua-скрипта: {ex.Message}", "Lua", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return Task.CompletedTask;
    }

    public async Task<string> RunModuleAsync(ModuleDefinition module, Dictionary<string, object> args)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptPath = module.Script;
            if (!Path.IsPathRooted(scriptPath))
            {
                var direct = Path.Combine(baseDir, scriptPath);
                var inScripts = Path.Combine(baseDir, "Scripts", scriptPath);
                if (File.Exists(direct)) scriptPath = direct; else scriptPath = inScripts;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Скрипт модуля не найден: {module.Script}", "Модули", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            _currentLua = new Lua();
            _currentLua.State.Encoding = Encoding.UTF8;
            _integration.Register(_currentLua);

            // Prepare args as global table 'args'
            _currentLua.NewTable("args");
            var tbl = (LuaTable)_currentLua["args"];
            foreach (var kv in args)
            {
                tbl[kv.Key] = kv.Value;
            }

            // Register completion bridge for async/callback-based scripts
            _currentTcs = new TaskCompletionSource<string>();
            var bridge = new LuaCompleteBridge(_currentTcs);
            var mi = typeof(LuaCompleteBridge).GetMethod(nameof(LuaCompleteBridge.Complete), new[] { typeof(object) });
            if (mi != null)
            {
                _currentLua.RegisterFunction("Complete", bridge, mi);
            }

            var code = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);

            try
            {
                var results = await Task.Run(() => _currentLua!.DoString(code));
                if (results != null && results.Length > 0 && results[0] != null)
                {
                    return results[0]?.ToString();
                }
            }
            catch (ObjectDisposedException)
            {
                return null; // stopped
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении модуля: {ex.Message}", "Модули", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            // Await until script calls Complete(result) or stop
            return await _currentTcs.Task.ConfigureAwait(true);
        }
        finally
        {
            try { _currentLua?.Dispose(); } catch { }
            _currentLua = null;
            _currentTcs = null;
        }
    }

    public void Stop()
    {
        try
        {
            _currentTcs?.TrySetResult(null);
            _currentLua?.Dispose();
        }
        catch { }
    }

    private class LuaCompleteBridge
    {
        private readonly TaskCompletionSource<string> _tcs;
        public LuaCompleteBridge(TaskCompletionSource<string> tcs) { _tcs = tcs; }
        public void Complete(object value)
        {
            try { _tcs.TrySetResult(value?.ToString()); } catch { _tcs.TrySetResult(null); }
        }
    }
}
