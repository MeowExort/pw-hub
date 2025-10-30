using System.Security.Cryptography;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth.Telegram;

public static class GenerateTelegramLinkEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/telegram/link", Handle)
            .WithName("GenerateTelegramLink")
            .WithSummary("Генерирует ссылку для Telegram-бота с предзаполненным state")
            .WithDescription("Требуется заголовок X-Auth-Token. Возвращает deep-link на бота вида https://t.me/<bot>?start=<state> вместе с самим state и временем истечения.");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, IConfiguration config)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();

        // 1) Get bot username from env/config
        var botUsername = Environment.GetEnvironmentVariable("TELEGRAM_BOT_USERNAME")
                          ?? config["Telegram:BotUsername"];
        if (string.IsNullOrWhiteSpace(botUsername))
        {
            return Results.Problem(
                detail: "Имя Telegram-бота не настроено. Установите переменную окружения TELEGRAM_BOT_USERNAME или ключ конфигурации Telegram:BotUsername.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Telegram-бот не сконфигурирован");
        }

        // 2) Generate short-lived state and save
        var state = GenerateSecureState(24); // 24 bytes -> 32 base64url chars, ок для start-параметра
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(15);

        db.TelegramLinkStates.Add(new Domain.TelegramLinkState
        {
            Id = Guid.NewGuid(),
            State = state,
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = expires,
            ConsumedAt = null
        });
        await db.SaveChangesAsync();

        // 3) Compose deep link
        var link = $"https://t.me/{botUsername}?start={state}";

        return Results.Ok(new { link, state, botUsername, expiresAt = expires });
    }

    private static string GenerateSecureState(int bytesLength)
    {
        Span<byte> bytes = stackalloc byte[bytesLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
