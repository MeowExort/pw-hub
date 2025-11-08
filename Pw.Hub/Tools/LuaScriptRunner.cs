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

    private Guid? _runId;
    public void SetRunId(Guid runId)
    {
        _runId = runId;
        try { _integration.SetRunId(runId); } catch { }
        try { Pw.Hub.Infrastructure.RunLifetimeTracker.SetActive(runId); } catch { }
    }

    // Debug support types
    public delegate bool DebugBreakHandler(int line, IDictionary<string, object> locals,
        IDictionary<string, object> globals);

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

    public async Task RunAsync(string scriptFileName)
    {
        // Load script text from embedded Scripts folder in output directory
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptPath = Path.Combine(baseDir, "Scripts", scriptFileName);
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Скрипт {scriptFileName} не найден по пути: {scriptPath}", "Lua", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var code = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);
            await RunCodeAsync(code);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка во время выполнения Lua-скрипта: {ex.Message}", "Lua", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public Task RunCodeAsync(string code)
    {
        return RunCodeAsync(code, null);
    }

    public Task RunCodeAsync(string code, Dictionary<string, object> args)
    {
        // Keep Lua VM alive so callback-based APIs (…Cb) can invoke into the same state
        _currentLua?.Dispose(); // dispose previous editor session if any
        _currentLua = new Lua();
        _currentLua.State.Encoding = Encoding.UTF8;
        _integration.Register(_currentLua);

        // Inject args table if provided
        if (args != null)
        {
            try
            {
                _currentLua.NewTable("args");
                var tbl = (LuaTable)_currentLua["args"];
                foreach (var kv in args)
                {
                    try
                    {
                        var luaValue = _integration.ConvertToLuaValue(kv.Value);
                        tbl[kv.Key] = luaValue;
                    }
                    catch
                    {
                        tbl[kv.Key] = kv.Value;
                    }
                }
            }
            catch { }
        }

        // Bridge completion so editor can keep Stop enabled until script signals completion
        _currentTcs = new TaskCompletionSource<string>();
        var bridge = new LuaCompleteBridge(_currentTcs);
        var mi = typeof(LuaCompleteBridge).GetMethod(nameof(LuaCompleteBridge.Complete), new[] { typeof(object) });
        if (mi != null)
        {
            _currentLua.RegisterFunction("Complete", bridge, mi);
        }

        // Execute user code on a worker to avoid blocking UI; async callbacks will continue to use _currentLua
        return Task.Run(() =>
        {
            try
            {
                _currentLua!.DoString(code);
                // Не завершаем выполнение автоматически: скрипт может быть асинхронным и завершится через Complete()
                // Запускаем сторожевой монитор: как только все async-операции данного запуска завершатся и
                // некоторое время сохранится тишина — завершаем ран.
                var runId = _runId;
                if (runId.HasValue && _currentTcs != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Ждём опустошения счётчика + короткий grace период на всплески
                            var graceMs = 400;
                            var quietSince = DateTime.UtcNow;
                            var lastCount = -1;
                            while (true)
                            {
                                int count = Pw.Hub.Infrastructure.RunLifetimeTracker.GetCount(runId.Value);
                                if (count == 0)
                                {
                                    if ((DateTime.UtcNow - quietSince).TotalMilliseconds >= graceMs)
                                        break;
                                }
                                else
                                {
                                    quietSince = DateTime.UtcNow; // сбросить таймер тишины
                                }
                                // Если счётчик менялся — обнулим отсчёт тишины
                                if (count != lastCount)
                                {
                                    lastCount = count;
                                    if (count != 0) quietSince = DateTime.UtcNow;
                                }
                                await Task.Delay(50).ConfigureAwait(false);
                                if (_currentTcs == null) return; // уже завершено через Complete/Stop
                            }
                            _currentTcs?.TrySetResult(null);
                        }
                        catch { }
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                // Stopped while running — consider completed
                _currentTcs?.TrySetResult(null);
            }
            catch (Exception ex)
            {
                // Surface runtime error to user and complete
                MessageBox.Show($"Ошибка Lua: {ex.Message}", "Lua", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentTcs?.TrySetResult(null);
            }
        }).ContinueWith(t => _currentTcs!.Task).Unwrap();
    }

    public Task RunCodeWithBreakpointsAsync(string code, IEnumerable<int> breakpoints, DebugBreakHandler onBreak,
        string selectedAccountId = null)
    {
        return RunCodeWithBreakpointsAsync(code, breakpoints, onBreak, null, selectedAccountId);
    }

    public Task RunCodeWithBreakpointsAsync(string code, IEnumerable<int> breakpoints, DebugBreakHandler onBreak,
        Dictionary<string, object> args, string selectedAccountId = null)
    {
        try
        {
            _currentLua?.Dispose();
            _currentLua = new Lua();
            _currentLua.State.Encoding = Encoding.UTF8;
            _integration.Register(_currentLua);

            // Inject args table if provided
            if (args != null)
            {
                try
                {
                    _currentLua.NewTable("args");
                    var tbl = (LuaTable)_currentLua["args"];
                    foreach (var kv in args)
                    {
                        try
                        {
                            var luaValue = _integration.ConvertToLuaValue(kv.Value);
                            tbl[kv.Key] = luaValue;
                        }
                        catch
                        {
                            tbl[kv.Key] = kv.Value;
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(selectedAccountId))
                _currentLua["selectedAccountId"] = selectedAccountId;
            else
                _currentLua["selectedAccountId"] = null;

            // Build breakpoints table
            _currentLua.NewTable("__pw_breakpoints");
            var bpTable = (LuaTable)_currentLua["__pw_breakpoints"];
            foreach (var line in breakpoints?.Distinct() ?? Enumerable.Empty<int>())
            {
                bpTable[line] = true;
            }

            // Register C# break callback
            var bridge = new DebugBridge(onBreak);
            var mi = typeof(DebugBridge).GetMethod(nameof(DebugBridge.OnBreak));
            _currentLua.RegisterFunction("__pw_onbreak", bridge, mi);

            // Also bridge completion for debug sessions
            _currentTcs = new TaskCompletionSource<string>();
            var cmi = typeof(LuaCompleteBridge).GetMethod(nameof(LuaCompleteBridge.Complete), new[] { typeof(object) });
            if (cmi != null)
            {
                _currentLua.RegisterFunction("Complete", new LuaCompleteBridge(_currentTcs), cmi);
            }

            // Inject Lua debug prelude
            var prelude = @"
-- Snapshot current globals to distinguish user-defined ones later
__pw_initial_globals = {}
for k, _ in pairs(_G) do __pw_initial_globals[k] = true end

-- Snapshot of top-level (chunk) locals captured during initial script run
__pw_chunk_locals = {}

local function __pw_collect_locals(level)
  local t = {}
  local i = 1
  while true do
    local name, value = debug.getlocal(level+1, i)
    if not name then break end
    if name ~= '(*temporary)' then
      t[name] = value
    end
    i = i + 1
  end
  return t
end

local function __pw_collect_upvalues(level)
  local t = {}
  local info = debug.getinfo(level+1, 'f')
  if info and info.func then
    local i = 1
    while true do
      local name, value = debug.getupvalue(info.func, i)
      if not name then break end
      if name ~= '(*temporary)' then
        t[name] = value
      end
      i = i + 1
    end
  end
  return t
end

local function __pw_merge(a, b)
  local t = {}
  for k, v in pairs(a) do t[k] = v end
  for k, v in pairs(b) do if t[k] == nil then t[k] = v end end
  return t
end

local function __pw_collect_user_globals()
  local t = {}
  for k, v in pairs(_G) do
    if not __pw_initial_globals[k] then
      t[k] = v
    end
  end
  return t
end

local function __pw_hook(event, line)
  -- Continuously snapshot locals of the main chunk while it is running
  do
    local info = debug.getinfo(2, 'S')
    if info and info.what == 'main' then
      local i = 1
      while true do
        local name, value = debug.getlocal(2, i)
        if not name then break end
        if name ~= '(*temporary)' then __pw_chunk_locals[name] = value end
        i = i + 1
      end
    end
  end

  if event == 'line' and __pw_breakpoints[line] then
    -- level mapping: [1]=__pw_hook, [2]=running chunk/function (caller of hook)
    local locals = __pw_collect_locals(2)
    local ups = __pw_collect_upvalues(2)
    local merged = __pw_merge(locals, ups)
    local userg = __pw_merge(__pw_collect_user_globals(), __pw_chunk_locals)
    __pw_onbreak(line, merged, userg)
  end
end

debug.sethook(__pw_hook, 'l')
";

            _currentLua.DoString(prelude);
            _currentLua.DoString(code);
        }
        catch (ObjectDisposedException)
        {
            _currentTcs?.TrySetResult(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка во время отладки Lua-скрипта: {ex.Message}", "Lua Debug", MessageBoxButton.OK,
                MessageBoxImage.Error);
            _currentTcs?.TrySetResult(null);
        }

        return _currentTcs?.Task ?? Task.CompletedTask;
    }

    private class DebugBridge
    {
        private readonly DebugBreakHandler _onBreak;

        public DebugBridge(DebugBreakHandler onBreak)
        {
            _onBreak = onBreak;
        }

        public void OnBreak(object lineObj, object localsObj, object globalsObj)
        {
            try
            {
                int line = 0;
                if (lineObj is double d) line = (int)d;
                else if (lineObj is int i) line = i;
                else if (lineObj != null) int.TryParse(lineObj.ToString(), out line);
                var visited1 = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var locals = ConvertTable(localsObj, maxDepth: 7, currentDepth: 0, visited: visited1);
                var visited2 = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var globals =
                    ConvertTable(globalsObj, maxDepth: 6, currentDepth: 0, visited: visited2); // don't explode
                _onBreak?.Invoke(line, locals, globals);
            }
            catch
            {
            }
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static object ConvertValue(object val, int maxDepth, int currentDepth, ISet<object> visited)
    {
        try
        {
            if (val == null) return null;

            // Primitive-like types: keep as-is
            var type = val.GetType();
            if (type.IsPrimitive || val is string || val is decimal || val is DateTime || val is Guid)
                return val;

            // Stop deep expansion
            if (currentDepth >= maxDepth)
            {
                if (val is NLua.LuaTable)
                    return new Dictionary<string, object>(); // don't expand further
                return val?.ToString();
            }

            // NLua table: let ConvertTable handle cycle detection for tables
            if (val is NLua.LuaTable luaTable)
            {
                // Important: do not increment depth here; ConvertTable already accounts for one level when it
                // called ConvertValue(value, currentDepth + 1). Incrementing again would skip levels and flatten children.
                return ConvertTable(luaTable, maxDepth, currentDepth, visited);
            }

            // Cycle detection by reference for non-table complex values
            if (visited != null)
            {
                if (!IsSimple(val))
                {
                    if (visited.Contains(val)) return "<circular>";
                    visited.Add(val);
                }
            }

            // IEnumerable (but not string): present as indexed entries
            if (val is System.Collections.IEnumerable enumerable && val is not string)
            {
                var listDict = new Dictionary<string, object>();
                int idx = 1;
                foreach (var item in enumerable)
                {
                    listDict[idx.ToString()] = ConvertValue(item, maxDepth, currentDepth + 1, visited);
                    idx++;
                    if (idx > 200) break; // safety cap
                }

                listDict["Count"] = idx - 1;
                return listDict;
            }

            // General CLR object: reflect public readable properties
            var objDict = new Dictionary<string, object>();
            objDict["__type"] = type.Name;
            foreach (var p in type.GetProperties(System.Reflection.BindingFlags.Instance |
                                                 System.Reflection.BindingFlags.Public))
            {
                if (!p.CanRead) continue;
                object v;
                try
                {
                    v = p.GetValue(val, null);
                }
                catch
                {
                    v = null;
                }

                objDict[p.Name] = ConvertValue(v, maxDepth, currentDepth + 1, visited);
            }

            // Also include public fields if any
            foreach (var f in type.GetFields(System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Public))
            {
                object v;
                try
                {
                    v = f.GetValue(val);
                }
                catch
                {
                    v = null;
                }

                objDict[f.Name] = ConvertValue(v, maxDepth, currentDepth + 1, visited);
            }

            return objDict;
        }
        catch
        {
            return val;
        }
    }

    private static bool IsSimple(object val)
    {
        return val == null || val.GetType().IsPrimitive || val is string || val is decimal || val is DateTime ||
               val is Guid;
    }

    private static IDictionary<string, object> ConvertTable(object tableObj, int maxDepth = 2, int currentDepth = 0,
        ISet<object> visited = null)
    {
        var dict = new Dictionary<string, object>();
        try
        {
            if (tableObj is NLua.LuaTable t)
            {
                // Cycle detection
                if (visited != null)
                {
                    if (visited.Contains(t)) return dict; // already seen, return empty
                    visited.Add(t);
                }

                // Depth cap
                if (currentDepth >= maxDepth)
                    return dict; // do not expand further

                // Enumerate entries without creating Keys collection; cap entries
                var enumerator = t.GetEnumerator();
                int processed = 0;
                while (enumerator.MoveNext())
                {
                    if (processed++ > 200) break;
                    var key = enumerator.Key;
                    var value = enumerator.Value;
                    string keyStr;
                    try
                    {
                        keyStr = key?.ToString() ?? "<nil>";
                    }
                    catch
                    {
                        keyStr = "<key>";
                    }

                    dict[keyStr] = ConvertValue(value, maxDepth, currentDepth + 1, visited);
                }
            }
        }
        catch
        {
        }

        return dict;
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
                if (File.Exists(direct)) scriptPath = direct;
                else scriptPath = inScripts;
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Скрипт модуля не найден: {module.Script}", "Модули", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                try
                {
                    var luaValue = _integration.ConvertToLuaValue(kv.Value);
                    tbl[kv.Key] = luaValue;
                }
                catch
                {
                    tbl[kv.Key] = kv.Value;
                }
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
                MessageBox.Show($"Ошибка при выполнении модуля: {ex.Message}", "Модули", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }

            // Await until script calls Complete(result) or stop
            return await _currentTcs.Task.ConfigureAwait(true);
        }
        finally
        {
            try
            {
                _currentLua?.Dispose();
            }
            catch
            {
            }

            _currentLua = null;
            _currentTcs = null;
        }
    }

    public void Stop()
    {
        try
        {
            _currentTcs?.TrySetResult(null);
            try { if (_runId.HasValue) Pw.Hub.Infrastructure.RunLifetimeTracker.Reset(_runId.Value); } catch { }
            _currentLua?.Dispose();
        }
        catch
        {
        }
    }

    private class LuaCompleteBridge
    {
        private readonly TaskCompletionSource<string> _tcs;

        public LuaCompleteBridge(TaskCompletionSource<string> tcs)
        {
            _tcs = tcs;
        }

        public void Complete(object value)
        {
            try
            {
                _tcs.TrySetResult(value?.ToString());
            }
            catch
            {
                _tcs.TrySetResult(null);
            }
        }
    }
}