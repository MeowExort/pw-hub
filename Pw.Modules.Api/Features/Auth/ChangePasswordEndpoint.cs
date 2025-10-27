using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth;

public static class ChangePasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/password", Handle).WithName("ChangePassword");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, ChangePasswordRequest req)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();

        var current = (req.CurrentPassword ?? string.Empty).Trim();
        var next = (req.NewPassword ?? string.Empty).Trim();
        if (current.Length == 0 || next.Length < 3)
            return Results.BadRequest(new { message = "Неверные данные пароля" });

        var currentHash = AuthUtils.HashWithSalt(current, user.PasswordSalt);
        if (!string.Equals(currentHash, user.PasswordHash, StringComparison.Ordinal))
            return Results.BadRequest(new { message = "Текущий пароль неверен" });

        var (hash, salt) = AuthUtils.HashPassword(next);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Пароль изменён" });
    }
}
