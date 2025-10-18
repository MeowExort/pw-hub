using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Run;

public static class IncrementRunEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}/run", Handle)
            .WithName("IncrementRun");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, Guid id)
    {
        var module = await db.Modules.FindAsync(id);
        if (module == null) return Results.NotFound();
        module.RunCount += 1;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var count = await db.UserModules.CountAsync(x => x.ModuleId == id);
        return Results.Ok(ModuleMapper.ToDto(module, count));
    }
}