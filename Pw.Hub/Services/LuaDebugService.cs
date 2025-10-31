using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса отладки Lua-кода. Делегирует выполнение раннеру LuaScriptRunner,
/// обеспечивая единый точечный вход для View/ViewModel и последующую замену реализации при необходимости.
/// </summary>
public class LuaDebugService : ILuaDebugService
{
    /// <inheritdoc />
    public async Task RunWithBreakpointsAsync(
        LuaScriptRunner runner,
        string code,
        ISet<int> breakpoints,
        LuaScriptRunner.DebugBreakHandler onBreak,
        Dictionary<string, object> args)
    {
        if (runner == null) throw new ArgumentNullException(nameof(runner));
        code ??= string.Empty;
        breakpoints ??= new HashSet<int>();
        args ??= new Dictionary<string, object>();

        // В текущей архитектуре LuaScriptRunner уже поддерживает запуск с брейкпоинтами.
        // Сервис просто проксирует вызов, оставляя место для будущего расширения (логирование, телеметрия, отмена и т.п.).
        await runner.RunCodeWithBreakpointsAsync(code, breakpoints, onBreak, args, null);
    }
}
