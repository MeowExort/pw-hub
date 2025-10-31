using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Get;

public class GetModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}", Handle)
            .WithName("GetModule")
            .WithSummary("Получает информацию о модуле")
            .WithDescription("Требуется заголовок X-Api-Token.");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, Guid id)
    {
        var item = await db.Modules
            .Where(m => m.Id == id)
            .Select(m => new
            {
                Module = m,
                InstallCount = db.UserModules.Count(um => um.ModuleId == m.Id)
            })
            .SingleOrDefaultAsync();

        if (item == null)
            return Results.NotFound();

        var result = ModuleMapper.ToDto(item.Module, item.InstallCount);
        return Results.Ok(result);
    }
}