using System.Collections.Generic;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис управления отрядами и аккаунтами (CRUD) и загрузки данных для навигационного дерева.
/// Централизует доступ к БД (EF Core), чтобы ViewModel оставалась «тонкой».
/// </summary>
public interface IAccountsService
{
    /// <summary>
    /// Загружает все отряды с аккаунтами, упорядоченные по OrderIndex/Name.
    /// </summary>
    List<Squad> LoadSquads();

    /// <summary>
    /// Создаёт новый отряд с указанным именем и корректным OrderIndex (в конец списка).
    /// </summary>
    Task<Squad> CreateSquadAsync(string name);

    /// <summary>
    /// Обновляет имя отряда.
    /// </summary>
    Task UpdateSquadAsync(Squad squad, string newName);

    /// <summary>
    /// Удаляет отряд со всеми аккаунтами.
    /// </summary>
    Task DeleteSquadAsync(Squad squad);

    /// <summary>
    /// Создаёт аккаунт в указанном отряде с корректным OrderIndex (в конец отряда).
    /// </summary>
    Task<Account> CreateAccountAsync(Squad squad, string accountName);

    /// <summary>
    /// Обновляет имя аккаунта.
    /// </summary>
    Task UpdateAccountAsync(Account account, string newName);

    /// <summary>
    /// Удаляет аккаунт.
    /// </summary>
    Task DeleteAccountAsync(Account account);
}