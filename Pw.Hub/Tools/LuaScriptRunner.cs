using System.Text;
using System.Windows;
using System.IO;
using NLua;
using Pw.Hub.Abstractions;

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
}
