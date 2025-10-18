using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Uninstall;

public static class UninstallModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapDelete("/{id:guid}/install", Handle)
            .WithName("UninstallModule");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, Guid id, string userId)
    {
        var module = await db.Modules.FindAsync(id);
        if (module == null) return Results.NotFound();

        var rel = await db.UserModules.FirstOrDefaultAsync(x => x.ModuleId == id && x.UserId == userId);
        if (rel == null) return Results.NotFound();

        db.UserModules.Remove(rel);
        await db.SaveChangesAsync();
        var count = await db.UserModules.CountAsync(x => x.ModuleId == id);
        return Results.Ok(ModuleMapper.ToDto(module, count));
    }
}