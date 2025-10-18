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

    // Persist a single Lua state for the lifetime of the runner to keep callbacks alive
    private readonly Lua _lua;
    private readonly LuaIntegration _integration;

    public LuaScriptRunner(IAccountManager accountManager, IBrowser browser)
    {
        _accountManager = accountManager;
        _browser = browser;
        _lua = new Lua();
        _lua.State.Encoding = Encoding.UTF8;
        _integration = new LuaIntegration(_accountManager, _browser);
        _integration.Register(_lua);
    }

    public void SetPrintSink(Action<string>? sink)
    {
        _integration.SetPrintSink(sink);
    }

    public async Task RunAsync(string scriptFileName, string? selectedAccountId = null)
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

    public Task RunCodeAsync(string code, string? selectedAccountId = null)
    {
        try
        {
            // Provide a few globals per run
            if (!string.IsNullOrWhiteSpace(selectedAccountId))
            {
                _lua["selectedAccountId"] = selectedAccountId;
            }
            else
            {
                _lua["selectedAccountId"] = null;
            }

            _lua.DoString(code);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка во время выполнения Lua-скрипта: {ex.Message}", "Lua", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return Task.CompletedTask;
    }

    public async Task<string?> RunModuleAsync(ModuleDefinition module, Dictionary<string, object?> args)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptPath = module.Script;
            if (!Path.IsPathRooted(scriptPath))
            {
                // try absolute by combining with baseDir first; also support Scripts folder
                var direct = Path.Combine(baseDir, scriptPath);
                var inScripts = Path.Combine(baseDir, "Scripts", scriptPath);
                if (File.Exists(direct)) scriptPath = direct; else scriptPath = inScripts;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Скрипт модуля не найден: {module.Script}", "Модули", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Prepare args as global table 'args'
            _lua.NewTable("args");
            var tbl = (LuaTable)_lua["args"];
            foreach (var kv in args)
            {
                tbl[kv.Key] = kv.Value;
            }

            var code = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);
            var results = _lua.DoString(code);
            if (results != null && results.Length > 0)
            {
                return results[0]?.ToString();
            }
            return null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при выполнении модуля: {ex.Message}", "Модули", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }
}
