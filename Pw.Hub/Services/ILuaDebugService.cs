using System.Collections.Generic;
using System.Threading.Tasks;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис отладки Lua-кода с поддержкой точек останова.
/// Инкапсулирует запуск кода с брейкпоинтами и обратным вызовом при остановке.
/// Позволяет вызывать отладку из View/VM без прямой зависимости от конкретной реализации раннера.
/// </summary>
public interface ILuaDebugService
{
    /// <summary>
    /// Запускает отладку Lua-кода с указанными точками останова.
    /// </summary>
    /// <param name="runner">Экземпляр LuaScriptRunner.</param>
    /// <param name="code">Текст Lua-кода.</param>
    /// <param name="breakpoints">Множество строк (1-based), на которых нужно остановиться.</param>
    /// <param name="onBreak">Колбэк, вызываемый при остановке на брейкпоинте (line, locals, globals). Возвращает продолжать ли выполнение.</param>
    /// <param name="args">Аргументы выполнения.</param>
    Task RunWithBreakpointsAsync(LuaScriptRunner runner, string code, ISet<int> breakpoints, LuaScriptRunner.DebugBreakHandler onBreak, Dictionary<string, object> args);
}
