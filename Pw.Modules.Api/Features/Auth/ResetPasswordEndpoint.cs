using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Infrastructure.Telegram;

namespace Pw.Modules.Api.Features.Auth;

/// <summary>
/// Сброс пароля текущего пользователя. Требуется заголовок X-Auth-Token.
/// Если к аккаунту привязан Telegram, новый пароль отправляется туда и не возвращается в ответе.
/// Если Telegram не привязан или отправка не удалась — пароль возвращается в ответе.
/// </summary>
public static class ResetPasswordEndpoint
{
    public sealed class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public bool SentToTelegram { get; set; }
        public string? Password { get; set; }
        public string? Message { get; set; }
    }
    
    public sealed class ResetPasswordRequest
    {
        public string Username { get; set; }
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/password/reset", Handle)
            .WithName("ResetPassword")
            .WithSummary("Сброс пароля текущего пользователя")
            .WithDescription("Требуется X-Auth-Token. Если Telegram привязан — пароль отправляется в Telegram и не возвращается в теле ответа.");
    }

    public static async Task<IResult> Handle(
        HttpRequest request,
        [FromServices] ModulesDbContext db,
        [FromServices] ITelegramSender telegram, ResetPasswordRequest req)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var currentUser = await AuthUtils.GetUserByTokenAsync(db, token);
        if (currentUser == null) return Results.Unauthorized();
        if (currentUser.Id != "ba791076c430460eaf6cd1c7391d1d9b") return Results.Forbid();
        
        var username = (req.Username ?? string.Empty).Trim();
        if (username.Length == 0)
            return Results.BadRequest(new { message = "Введите имя пользователя" });
        
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        // 1) Сгенерировать новый пароль
        var newPassword = GeneratePassword(14);

        // 2) Обновить hash/salt у пользователя
        var (hash, salt) = AuthUtils.HashPassword(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        await db.SaveChangesAsync();

        // 3) Попробовать отправить в Telegram, если привязан
        var sentToTelegram = false;
        if (user.TelegramId != null)
        {
            var text = $"Ваш пароль для Pw.Helper был сброшен.\nЛогин: {user.Username}\nНовый пароль: {newPassword}\n\nРекомендуем поменять пароль после входа.";
            try
            {
                sentToTelegram = await telegram.SendAsync(user.TelegramId.Value, text);
            }
            catch
            {
                sentToTelegram = false;
            }
        }

        // 4) Сформировать ответ
        if (sentToTelegram)
        {
            return Results.Ok(new ResetPasswordResponse
            {
                Success = true,
                SentToTelegram = true,
                Password = null,
                Message = "Новый пароль отправлен в Telegram"
            });
        }
        else
        {
            return Results.Ok(new ResetPasswordResponse
            {
                Success = true,
                SentToTelegram = false,
                Password = newPassword,
                Message = user.TelegramId == null
                    ? "Telegram не привязан — пароль возвращён в ответе"
                    : "Не удалось отправить в Telegram — пароль возвращён в ответе"
            });
        }
    }

    private static string GeneratePassword(int length)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // без I/O
        const string lower = "abcdefghijkmnpqrstuvwxyz"; // без l
        const string digits = "23456789"; // без 0/1
        const string symbols = "!@#$%^&*";
        var all = upper + lower + digits + symbols;

        Span<byte> buf = stackalloc byte[length];
        RandomNumberGenerator.Fill(buf);

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            var b = buf[i];
            chars[i] = all[b % all.Length];
        }

        // Обеспечим присутствие базовых категорий
        chars[0] = upper[buf[0] % upper.Length];
        chars[1 % length] = lower[buf[1 % length] % lower.Length];
        chars[2 % length] = digits[buf[2 % length] % digits.Length];
        chars[3 % length] = symbols[buf[3 % length] % symbols.Length];

        return new string(chars);
    }
}
