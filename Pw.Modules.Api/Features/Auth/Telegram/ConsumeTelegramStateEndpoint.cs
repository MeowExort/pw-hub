using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth.Telegram;

public static class ConsumeTelegramStateEndpoint
{
    public sealed class ConsumeRequest
    {
        public string? State { get; set; }
        public long TelegramId { get; set; }
        public string? TelegramUsername { get; set; }
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/telegram/consume", Handle)
            .WithName("ConsumeTelegramState")
            .WithSummary("Потребляет state и привязывает Telegram к пользователю")
            .WithDescription("Для Telegram-бота. Требуется заголовок X-Bot-Token. Тело: { state, telegramId, telegramUsername }.");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, IConfiguration config, ConsumeRequest body)
    {
        // 1) Authenticate bot
        var botTokenHeader = request.Headers["X-Bot-Token"].FirstOrDefault();
        var expectedToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                             ?? config["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return Results.Problem(
                detail: "Токен Telegram-бота не настроен. Установите TELEGRAM_BOT_TOKEN или Telegram:BotToken",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Конфигурация отсутствует");
        }
        if (!string.Equals(botTokenHeader, expectedToken, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        // 2) Validate input
        var state = (body.State ?? string.Empty).Trim();
        if (state.Length == 0 || body.TelegramId == 0)
        {
            return Results.BadRequest(new { message = "state и telegramId обязательны" });
        }

        // 3) Load state
        var now = DateTimeOffset.UtcNow;
        var linkState = await db.TelegramLinkStates.FirstOrDefaultAsync(s => s.State == state);
        if (linkState == null)
            return Results.BadRequest(new { message = "state не найден" });
        if (linkState.ExpiresAt <= now)
            return Results.BadRequest(new { message = "state истёк" });
        if (linkState.ConsumedAt != null)
            return Results.BadRequest(new { message = "state уже использован" });

        // 4) Ensure telegramId is not linked to another user
        var existingWithTelegram = await db.Users
            .Where(u => u.TelegramId == body.TelegramId)
            .Select(u => new { u.Id, u.Username })
            .FirstOrDefaultAsync();
        if (existingWithTelegram != null && existingWithTelegram.Id != linkState.UserId)
        {
            return Results.Conflict(new { message = "Этот Telegram уже привязан к другому аккаунту" });
        }

        // 5) Link to user
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == linkState.UserId);
        if (user == null)
            return Results.NotFound(new { message = "Пользователь не найден" });

        user.TelegramId = body.TelegramId;
        user.TelegramUsername = string.IsNullOrWhiteSpace(body.TelegramUsername) ? user.TelegramUsername : body.TelegramUsername?.Trim();
        user.TelegramLinkedAt = now;

        // 6) Consume state
        linkState.ConsumedAt = now;

        await db.SaveChangesAsync();

        return Results.Ok(new { userId = user.Id, username = user.Username, linkedAt = user.TelegramLinkedAt });
    }
}
