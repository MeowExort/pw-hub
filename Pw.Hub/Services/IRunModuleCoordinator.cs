using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

/// <summary>
/// Координатор запуска Lua-модулей из ViewModel.
/// Инкапсулирует сбор параметров, сохранение последних аргументов и вызов сервиса исполнения.
/// </summary>
public interface IRunModuleCoordinator
{
    /// <summary>
    /// Выполняет полный сценарий запуска выбранного модуля с UI-диалогом параметров.
    /// </summary>
    Task RunAsync(ModuleDefinition module);
}