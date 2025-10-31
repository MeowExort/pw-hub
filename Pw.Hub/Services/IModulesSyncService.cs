using System;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис синхронизации модулей с сервером и управления локальными установками.
/// Инкапсулирует бизнес-логику, ранее дублированную в окнах/VM.
/// </summary>
public interface IModulesSyncService
{
    /// <summary>
    /// Синхронизирует локально установленные модули со списком, привязанным к текущему пользователю на сервере.
    /// Возвращает true, если локальный набор модулей был изменён (что требует обновления UI-списка).
    /// </summary>
    Task<bool> SyncInstalledAsync();

    /// <summary>
    /// Устанавливает/обновляет модуль локально на основании данных с сервера.
    /// </summary>
    void InstallModuleLocally(ModuleDto module);

    /// <summary>
    /// Удаляет модуль локально по идентификатору.
    /// </summary>
    void RemoveModuleLocally(Guid id);
}
