using Microsoft.AspNetCore.Mvc;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Infrastructure.Telegram;

namespace Pw.Modules.Api.Features.Auth.Telegram;

/// <summary>
/// Эндпоинт для отправки сообщения самому себе в Telegram (только текущему аутентифицированному пользователю).
/// Требуется заголовок X-Auth-Token. Тело: { message: string }.
/// </summary>
public static class SendMessageEndpoint
{
    public sealed class SendMessageRequest { public string? Message { get; set; } }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/telegram/send", Handle)
            .WithName("Auth_Telegram_Send")
            .WithDescription("Отправить сообщение себе в Telegram. Требуется X-Auth-Token. Пользователь должен быть привязан к Telegram.");
    }

    public static async Task<IResult> Handle(HttpRequest request, [FromServices] ModulesDbContext db, [FromServices] ITelegramSender sender, [FromBody] SendMessageRequest req)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();

        var text = (req.Message ?? string.Empty).Trim();
        if (text.Length == 0)
            return Results.BadRequest(new { message = "message обязателен" });

        if (user.TelegramId == null)
            return Results.BadRequest(new { message = "Telegram не привязан к аккаунту" });

        var ok = await sender.SendAsync(user.TelegramId.Value, text);
        if (!ok)
            return Results.Problem("Не удалось отправить сообщение в Telegram", statusCode: 502);

        return Results.Ok(new { success = true });
    }
}
