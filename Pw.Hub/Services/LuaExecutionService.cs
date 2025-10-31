using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса исполнения Lua-модулей.
/// Оборачивает настройку LuaScriptRunner и ScriptLogWindow, обеспечивая единый сценарий запуска.
/// </summary>
public class LuaExecutionService : ILuaExecutionService
{
    /// <summary>
    /// Запускает выполнение модуля и показывает окно логов как модальное до завершения.
    /// </summary>
    public async Task RunAsync(LuaScriptRunner runner, ModuleDefinition module, Dictionary<string, object> args, Window owner)
    {
        if (runner == null) throw new ArgumentNullException(nameof(runner));
        if (module == null) throw new ArgumentNullException(nameof(module));
        owner ??= Application.Current?.MainWindow;

        var logWindow = new Windows.ScriptLogWindow(module.Name) { Owner = owner };

        // Подписки на вывод/прогресс от раннера
        runner.SetPrintSink(text => logWindow.AppendLog(text));
        runner.SetProgressSink((percent, message) => logWindow.ReportProgress(percent, message));

        // Кнопка остановки в лог-окне
        logWindow.SetStopAction(() =>
        {
            try
            {
                runner.Stop();
                logWindow.AppendLog("[Остановлено пользователем]");
            }
            catch { }
        });

        logWindow.SetRunning(true);

        string result = null;
        try
        {
            // Запускаем выполнение (асинхронно) и по завершению закрываем окно
            var task = runner.RunModuleAsync(module, args).ContinueWith(t =>
            {
                try
                {
                    result = t.IsCompletedSuccessfully ? t.Result : null;
                    logWindow.MarkCompleted(result);

                    // Ненавязчивое уведомление через трей, если доступно
                    if (Application.Current is App app)
                    {
                        var title = $"Модуль: {module.Name}";
                        var text = string.IsNullOrWhiteSpace(result) ? "Выполнено" : result;
                        app.NotifyIcon?.ShowBalloonTip(5, title, text, System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
                catch { }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // Показываем окно логов как модальное до завершения
            logWindow.ShowDialog();

            // Дожидаемся завершения, если требуется
            await Task.Yield();
        }
        catch
        {
            // Ошибки отображаются в окне логов/уведомлениях, здесь подавляем
        }
    }
}
