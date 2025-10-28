using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Dtos;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Installed;

public static class InstalledModulesEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/installed", Handle)
            .WithName("GetInstalledModules");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Results.BadRequest(new { error = "userId is required" });

        var items = await db.Modules
            .Where(m => db.UserModules.Any(um => um.UserId == userId && um.ModuleId == m.Id))
            .Select(m => new
            {
                Module = m,
                InstallCount = db.UserModules.Count(um => um.ModuleId == m.Id),
                AuthorUsername = db.Users.Where(u => u.Id == m.OwnerUserId!).Select(u => u.Username).FirstOrDefault()
            })
            .ToListAsync();

        var result = items.Select(x => ModuleMapper.ToDto(x.Module, x.InstallCount, x.AuthorUsername)).ToList();
        return Results.Ok(result);
    }
}
