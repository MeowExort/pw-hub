using System.Threading;
using System.Threading.Tasks;

namespace Pw.Modules.Api.Infrastructure.Telegram;

/// <summary>
/// Сервис-обёртка для отправки сообщений через Telegram-бота.
/// Использует тот же токен, что и хост-сервис бота. Нужен для явной отправки сообщений из API.
/// </summary>
public interface ITelegramSender
{
    /// <summary>
    /// Отправляет текстовое сообщение пользователю по его TelegramId.
    /// </summary>
    /// <param name="telegramId">Числовой идентификатор пользователя Telegram (chat id).</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="ct">Токен отмены.</param>
    Task<bool> SendAsync(long telegramId, string message, CancellationToken ct = default);
}
