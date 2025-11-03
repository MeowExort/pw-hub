using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;

namespace Pw.Modules.Api.Features.Auth.Telegram;

public static class UnlinkTelegramEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/telegram/unlink", Handle)
            .WithName("UnlinkTelegram")
            .WithSummary("Отвязывает Telegram от аккаунта пользователя")
            .WithDescription("Требуется заголовок X-Auth-Token. Очищает поля TelegramId/TelegramUsername/TelegramLinkedAt у пользователя и возвращает обновлённого пользователя.");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();

        // Idempotent: if already unlinked, just return current state
        var changed = user.TelegramId != null || user.TelegramUsername != null || user.TelegramLinkedAt != null;
        if (changed)
        {
            user.TelegramId = null;
            user.TelegramUsername = null;
            user.TelegramLinkedAt = null;
            await db.SaveChangesAsync();

            // Metrics: successful Telegram unlink
            ModuleMetrics.TelegramUnlinked.Add(1);
        }

        return Results.Ok(new UserDto
        {
            UserId = user.Id,
            Username = user.Username,
            Developer = user.Developer,
            TelegramId = user.TelegramId,
            TelegramUsername = user.TelegramUsername,
            TelegramLinkedAt = user.TelegramLinkedAt
        });
    }
}
