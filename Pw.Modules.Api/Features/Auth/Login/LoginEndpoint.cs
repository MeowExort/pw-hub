using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/login", Handle).WithName("Login");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, LoginRequest req)
    {
        var username = (req.Username ?? string.Empty).Trim();
        var password = (req.Password ?? string.Empty).Trim();
        if (username.Length == 0 || password.Length == 0)
            return Results.BadRequest(new { message = "Введите имя пользователя и пароль" });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null) return Results.Unauthorized();
        var hash = Features.Auth.AuthUtils.HashWithSalt(password, user.PasswordSalt);
        if (!string.Equals(hash, user.PasswordHash, StringComparison.Ordinal))
            return Results.Unauthorized();

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

        var resp = new AuthResponse { UserId = user.Id, Username = user.Username, Developer = user.Developer, Token = token };
        return Results.Ok(resp);
    }
}
