using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/register", Handle).WithName("Register");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, RegisterRequest req)
    {
        var username = (req.Username ?? string.Empty).Trim();
        var password = (req.Password ?? string.Empty).Trim();
        if (username.Length < 3 || password.Length < 3)
            return Results.BadRequest(new { message = "Неверные имя пользователя или пароль" });

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            return Results.Conflict(new { message = "Пользователь уже существует" });

        var (hash, salt) = Features.Auth.AuthUtils.HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Developer = req.Developer,
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
        return Results.Ok(resp);
    }
}