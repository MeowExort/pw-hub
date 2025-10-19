using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Auth.Me;

public static class MeEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", Handle).WithName("Me");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();
        return Results.Ok(new UserDto { UserId = user.Id, Username = user.Username, Developer = user.Developer });
    }
}
