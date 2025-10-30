using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Infrastructure.Telegram;

public sealed class TelegramBotHostedService : BackgroundService
{
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private ITelegramBotClient? _bot;

    public TelegramBotHostedService(ILogger<TelegramBotHostedService> logger, IServiceProvider services, IConfiguration config)
    {
        _logger = logger;
        _services = services;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read bot token from env or config
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                    ?? _config["Telegram:BotToken"];

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Telegram bot token is not configured. Set TELEGRAM_BOT_TOKEN or Telegram:BotToken. Bot will not start.");
            return; // do not start without token
        }

        _bot = new TelegramBotClient(token);

        try
        {
            var me = await _bot.GetMeAsync(stoppingToken);
            _logger.LogInformation("Telegram bot connected as @{Username} (id={Id})", me.Username, me.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Telegram bot: {Message}", ex.Message);
            return;
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message },
            ThrowPendingUpdates = true
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cts.Token);

        _logger.LogInformation("Telegram bot receiver started.");

        try
        {
            // Wait until the service is stopped
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // normal on shutdown
        }
        finally
        {
            cts.Cancel();
            _logger.LogInformation("Telegram bot receiver stopped.");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message) return;
        var msg = update.Message;
        if (msg == null) return;
        if (msg.From == null) return;

        try
        {
            if (msg.Text != null)
            {
                await HandleTextMessage(bot, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private async Task HandleTextMessage(ITelegramBotClient bot, Message msg, CancellationToken ct)
    {
        var text = (msg.Text ?? string.Empty).Trim();
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var state = parts[1];
                await TryLinkByState(bot, msg, state, ct);
                return;
            }

            await bot.SendTextMessageAsync(chatId: msg.Chat.Id,
                text: "Чтобы привязать аккаунт, откройте ссылку из сайта или отправьте команду /start <state>.",
                replyToMessageId: msg.MessageId,
                cancellationToken: ct);
            return;
        }

        if (text.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendTextMessageAsync(chatId: msg.Chat.Id,
                text: "Доступные команды:\n/start <state> — привязать аккаунт к сайту.",
                replyToMessageId: msg.MessageId,
                cancellationToken: ct);
            return;
        }

        // Ignore other text, but provide a hint
        await bot.SendTextMessageAsync(chatId: msg.Chat.Id,
            text: "Я помогу привязать аккаунт. Введите /start <state> или используйте ссылку с сайта.",
            replyToMessageId: msg.MessageId,
            cancellationToken: ct);
    }

    private async Task TryLinkByState(ITelegramBotClient bot, Message msg, string state, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ModulesDbContext>();

        var now = DateTimeOffset.UtcNow;
        var entry = await db.TelegramLinkStates.FirstOrDefaultAsync(s => s.State == state, ct);
        if (entry == null)
        {
            await bot.SendTextMessageAsync(msg.Chat.Id, "Неверный или просроченный код (state). Попробуйте сгенерировать новую ссылку в профиле.", replyToMessageId: msg.MessageId, cancellationToken: ct);
            return;
        }

        if (entry.ExpiresAt <= now)
        {
            await bot.SendTextMessageAsync(msg.Chat.Id, "Срок действия кода истёк. Сгенерируйте новый в профиле.", replyToMessageId: msg.MessageId, cancellationToken: ct);
            return;
        }

        if (entry.ConsumedAt != null)
        {
            await bot.SendTextMessageAsync(msg.Chat.Id, "Этот код уже использован. Сгенерируйте новый в профиле.", replyToMessageId: msg.MessageId, cancellationToken: ct);
            return;
        }

        var telegramId = msg.From!.Id;
        var telegramUsername = msg.From!.Username;

        // Ensure this telegramId is not linked to another user
        var existingWithTelegram = await db.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId, ct);
        if (existingWithTelegram != null && existingWithTelegram.Id != entry.UserId)
        {
            await bot.SendTextMessageAsync(msg.Chat.Id, "Этот Telegram уже привязан к другому аккаунту.", replyToMessageId: msg.MessageId, cancellationToken: ct);
            return;
        }

        // Load target user
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId, ct);
        if (user == null)
        {
            await bot.SendTextMessageAsync(msg.Chat.Id, "Пользователь не найден. Попробуйте заново сгенерировать ссылку.", replyToMessageId: msg.MessageId, cancellationToken: ct);
            return;
        }

        user.TelegramId = telegramId;
        user.TelegramUsername = telegramUsername;
        user.TelegramLinkedAt = DateTimeOffset.UtcNow;

        entry.ConsumedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save Telegram linking for user {UserId}", user.Id);
            await bot.SendTextMessageAsync(msg.Chat.Id, "Не удалось завершить привязку из-за ошибки сервера. Попробуйте позже.", replyToMessageId: msg.MessageId, cancellationToken: ct);
            return;
        }

        await bot.SendTextMessageAsync(msg.Chat.Id,
            $"Готово! Аккаунт успешно привязан к пользователю {user.Username}.",
            cancellationToken: ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error [{apiEx.ErrorCode}]: {apiEx.Message}",
            _ => exception.ToString()
        };
        _logger.LogError("Telegram receiver error: {Error}", errorMessage);
        return Task.CompletedTask;
    }
}
