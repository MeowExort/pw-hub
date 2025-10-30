using System.Security.Cryptography;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth.Telegram;

public static class GenerateTelegramStateEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/telegram/state", Handle)
            .WithName("GenerateTelegramState")
            .WithSummary("Генерирует state для привязки аккаунта к Telegram-боту")
            .WithDescription("Требуется заголовок X-Auth-Token. Возвращает короткоживущий state, по которому бот сможет связать аккаунт пользователя.");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();

        var state = GenerateSecureState(24);
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

        return Results.Ok(new { state, expiresAt = expires });
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