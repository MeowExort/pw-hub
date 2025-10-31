using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис загрузки данных о серверах и персонажах аккаунта из браузера ЛК.
/// Инкапсулирует сценарий навигации, парсинга DOM через JavaScript и сохранения данных в БД.
/// Выделение в сервис позволяет вызывать логику из ViewModel/окон без дублирования.
/// </summary>
public interface ICharactersLoadService
{
    /// <summary>
    /// Загружает и сохраняет список серверов и персонажей для указанного аккаунта.
    /// Показывает окно логов процесса и возвращается по завершении (после закрытия окна).
    /// </summary>
    /// <param name="account">Аккаунт, для которого требуется загрузка.</param>
    /// <param name="accountPage">Экземпляр страницы аккаунтов с доступом к браузеру и менеджеру аккаунтов.</param>
    /// <param name="owner">Окно-владелец для модального окна логов.</param>
    Task LoadForAccountAsync(Account account, Pages.AccountPage accountPage, Window owner);
}
