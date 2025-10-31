using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис управления порядком (OrderIndex) отрядов и аккаунтов.
/// Отвечает за перестановку элементов при drag&drop и сохранение порядка в БД.
/// </summary>
public interface IOrderingService
{
    /// <summary>
    /// Переносит отряд в новое место среди отрядов и пересчитывает OrderIndex.
    /// </summary>
    /// <param name="moved">Перетаскиваемый отряд.</param>
    /// <param name="target">Отряд-цель (относительно которого вставляем).</param>
    /// <param name="insertAfter">Если true — вставить после целевого, иначе перед.</param>
    Task ReorderSquadAsync(Squad moved, Squad target, bool insertAfter);

    /// <summary>
    /// Перемещает аккаунт внутри отряда или между отрядами, пересчитывая OrderIndex.
    /// </summary>
    /// <param name="moved">Перетаскиваемый аккаунт.</param>
    /// <param name="targetSquad">Целевой отряд.</param>
    /// <param name="targetAccount">Аккаунт-цель в отряде (может быть null для вставки в конец).</param>
    /// <param name="insertAfter">Если true — вставить после targetAccount, иначе перед.</param>
    Task ReorderAccountAsync(Account moved, Squad targetSquad, Account? targetAccount, bool insertAfter);
}
