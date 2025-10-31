using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис исполнения Lua-модулей. Инкапсулирует создание окна логов,
/// подписки на прогресс/логи, обработку остановки и завершения.
/// Позволяет вызывать выполнение из ViewModel/окон без дублирования логики.
/// </summary>
public interface ILuaExecutionService
{
    /// <summary>
    /// Запускает выполнение модуля в раннере с отображением окна логов.
    /// Метод сам создаёт и показывает окно логов как модальное и возвращается после закрытия окна.
    /// </summary>
    /// <param name="runner">Экземпляр LuaScriptRunner, сконфигурированный внешним кодом.</param>
    /// <param name="module">Определение модуля для запуска.</param>
    /// <param name="args">Собранные аргументы запуска.</param>
    /// <param name="owner">Владелец окна логов (обычно текущее окно).</param>
    Task RunAsync(LuaScriptRunner runner, ModuleDefinition module, Dictionary<string, object> args, Window owner);
}
