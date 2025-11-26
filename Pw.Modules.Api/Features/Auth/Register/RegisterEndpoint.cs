using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Domain;
using Pw.Modules.Api.Features.Modules;

namespace Pw.Modules.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/register", Handle).WithName("Register");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, RegisterRequest req)
    {
        ModuleMetrics.AuthRegisterAttempts.Add(1);
        var sw = Stopwatch.StartNew();
        var username = (req.Username ?? string.Empty).Trim();
        var password = (req.Password ?? string.Empty).Trim();
        if (username.Length < 3 || password.Length < 3)
        {
            ModuleMetrics.AuthRegisterFailure.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "invalid_input"));
            ModuleMetrics.AuthRegisterDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"));
            return Results.BadRequest(new { message = "Неверные имя пользователя или пароль" });
        }

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
        {
            ModuleMetrics.AuthRegisterFailure.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "user_exists"));
            ModuleMetrics.AuthRegisterDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"));
            return Results.Conflict(new { message = "Пользователь уже существует" });
        }

        var (hash, salt) = Features.Auth.AuthUtils.HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Developer = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);

        var token = Features.Auth.AuthUtils.NewToken();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Token = token,
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var resp = new AuthResponse
            { UserId = user.Id, Username = user.Username, Developer = user.Developer, Token = token };
        ModuleMetrics.AuthRegisterSuccess.Add(1);
        ModuleMetrics.AuthRegisterDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "success"));
        return Results.Ok(resp);
    }
}