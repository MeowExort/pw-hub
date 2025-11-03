using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Pw.Modules.Api.Features.Modules;

namespace Pw.Modules.Api.Infrastructure.Telegram;

/// <summary>
/// Реализация отправителя сообщений в Telegram. Создаёт клиента по токену из env/config.
/// </summary>
public class TelegramSender : ITelegramSender
{
    private readonly ILogger<TelegramSender> _logger;
    private readonly IConfiguration _config;

    public TelegramSender(ILogger<TelegramSender> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<bool> SendAsync(long telegramId, string message, CancellationToken ct = default)
    {
        try
        {
            var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                        ?? _config["Telegram:BotToken"];
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Telegram bot token is not configured. Cannot send message.");
                return false;
            }

            var bot = new TelegramBotClient(token);
            await bot.SendTextMessageAsync(telegramId, message, parseMode: ParseMode.Markdown, cancellationToken: ct);

            // Metrics: successful Telegram message sent
            ModuleMetrics.TelegramMessagesSent.Add(1);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message: {Message}", ex.Message);
            return false;
        }
    }
}