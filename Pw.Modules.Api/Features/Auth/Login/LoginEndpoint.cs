using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;

namespace Pw.Modules.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/login", Handle).WithName("Login");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, LoginRequest req)
    {
        ModuleMetrics.AuthLoginAttempts.Add(1);
        var sw = Stopwatch.StartNew();
        var username = (req.Username ?? string.Empty).Trim();
        var password = (req.Password ?? string.Empty).Trim();
        if (username.Length == 0 || password.Length == 0)
        {
            ModuleMetrics.AuthLoginFailure.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "invalid_input"));
            ModuleMetrics.AuthLoginDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"));
            return Results.BadRequest(new { message = "Введите имя пользователя и пароль" });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
        {
            ModuleMetrics.AuthLoginFailure.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "user_not_found"));
            ModuleMetrics.AuthLoginDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"));
            return Results.Unauthorized();
        }
        var hash = Features.Auth.AuthUtils.HashWithSalt(password, user.PasswordSalt);
        if (!string.Equals(hash, user.PasswordHash, StringComparison.Ordinal))
        {
            ModuleMetrics.AuthLoginFailure.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "invalid_credentials"));
            ModuleMetrics.AuthLoginDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"));
            return Results.Unauthorized();
        }

        var token = Features.Auth.AuthUtils.NewToken();
        db.Sessions.Add(new Domain.Session
        {
            Id = Guid.NewGuid(),
            Token = token,
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var resp = new AuthResponse
            { UserId = user.Id, Username = user.Username, Developer = user.Developer, Token = token };
        ModuleMetrics.AuthLoginSuccess.Add(1);
        ModuleMetrics.AuthLoginDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "success"));
        return Results.Ok(resp);
    }
}