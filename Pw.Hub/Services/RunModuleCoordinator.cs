using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация координатора запуска Lua-модулей.
/// Инкапсулирует показ диалога параметров, сохранение последних аргументов,
/// вызов сервиса исполнения и инкремент счётчика запусков в Modules API.
/// </summary>
public sealed class RunModuleCoordinator : IRunModuleCoordinator
{
    private readonly IWindowService _windows;
    private readonly ILuaExecutionService _exec;
    private readonly ModuleService _moduleService;

    public RunModuleCoordinator(IWindowService windows, ILuaExecutionService exec)
    {
        _windows = windows ?? new WindowService();
        _exec = exec ?? new LuaExecutionService();
        _moduleService = new ModuleService();
    }

    /// <summary>
    /// Выполняет полный сценарий запуска выбранного модуля: запрашивает параметры у пользователя,
    /// сохраняет их как "последние" в локальном каталоге модулей, затем запускает выполнение
    /// через сервис исполнения с окном логов. Инкремент запуска отправляется в API фоново.
    /// </summary>
    public async Task RunAsync(ModuleDefinition module)
    {
        if (module == null) return;

        // 1) Диалог параметров модуля
        var dlg = new Pw.Hub.Windows.ModuleArgsWindow(module);
        var ok = _windows.ShowDialog(dlg);
        if (ok != true) return;

        // 2) Сохранить последние строковые аргументы в локальном файле модулей
        try
        {
            module.LastArgs = dlg.StringValues;
            _moduleService.AddOrUpdateModule(module);
        }
        catch { /* не прерываем запуск */ }

        // 3) Подготовка раннера и запуск через сервис исполнения
        try
        {
            // Получаем текущее главное окно и необходимые зависимости раннера
            var owner = Application.Current?.MainWindow as Window;
            if (owner is not Pw.Hub.MainWindow mw)
            {
                // Если по какой-то причине MainWindow недоступно — просто выходим
                return;
            }

            var runner = new LuaScriptRunner(mw.AccountPage.AccountManager, mw.AccountPage.Browser);
            _ = IncrementRunIfApiModuleAsync(module); // fire-and-forget
            await _exec.RunAsync(runner, module, dlg.Values ?? new Dictionary<string, object>(), owner);
        }
        catch
        {
            // Ошибки отображаются окном логов/уведомлениями, здесь подавляем
        }
    }

    private static async Task IncrementRunIfApiModuleAsync(ModuleDefinition module)
    {
        try
        {
            if (Guid.TryParse(module.Id, out var id))
            {
                var client = new ModulesApiClient();
                await client.IncrementRunAsync(id);
            }
        }
        catch { }
    }
}
