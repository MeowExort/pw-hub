using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Delete;

public static class DeleteModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle).WithName("DeleteModule");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, Guid id)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();
        var module = await db.Modules.FirstOrDefaultAsync(m => m.Id == id);
        if (module == null) return Results.NotFound();
        if (!user.Developer || !string.Equals(module.OwnerUserId, user.Id, StringComparison.Ordinal))
            return Results.Forbid();

        db.Modules.Remove(module);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
