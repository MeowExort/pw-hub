using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Install;

public static class InstallModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}/install", Handle)
            .WithName("InstallModule");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, Guid id, string userId)
    {
        var module = await db.Modules.FindAsync(id);
        if (module == null) return Results.NotFound();

        userId = userId.Trim();
        var existing = await db.UserModules.FirstOrDefaultAsync(x => x.ModuleId == id && x.UserId == userId);
        if (existing != null) return Results.Conflict(new { message = "Already installed" });

        db.UserModules.Add(new Domain.UserModule { ModuleId = id, UserId = userId, InstalledAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        // Increment install metric only on successful install
        ModuleMetrics.Install.Add(1);

        var count = await db.UserModules.CountAsync(x => x.ModuleId == id);
        return Results.Ok(ModuleMapper.ToDto(module, count));
    }
}