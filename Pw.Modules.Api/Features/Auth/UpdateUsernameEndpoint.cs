using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth;

public static class UpdateUsernameEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/username", Handle).WithName("UpdateUsername");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, UpdateUsernameRequest req)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();

        var newUsername = (req.Username ?? string.Empty).Trim();
        if (newUsername.Length < 3)
            return Results.BadRequest(new { message = "Имя пользователя слишком короткое" });

        var isTaken = await db.Users.AnyAsync(u => u.Username.ToLower() == newUsername.ToLower() && u.Id != user.Id);
        if (isTaken)
            return Results.Conflict(new { message = "Имя пользователя уже занято" });

        user.Username = newUsername;
        await db.SaveChangesAsync();

        return Results.Ok(new UserDto { UserId = user.Id, Username = user.Username, Developer = user.Developer, TelegramId = user.TelegramId, TelegramUsername = user.TelegramUsername, TelegramLinkedAt = user.TelegramLinkedAt });
    }
}
