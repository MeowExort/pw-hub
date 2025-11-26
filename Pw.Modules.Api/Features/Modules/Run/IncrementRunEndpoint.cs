using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;

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
        ModuleMetrics.ModulesStartAttempts.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagModuleId, id));
        var sw = Stopwatch.StartNew();
        var module = await db.Modules.FindAsync(id);
        if (module == null)
        {
            ModuleMetrics.ModulesStartFailure.Add(1,
                new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "not_found"),
                new KeyValuePair<string, object?>(ModuleMetrics.TagModuleId, id));
            ModuleMetrics.ModulesStartDurationMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"),
                new KeyValuePair<string, object?>(ModuleMetrics.TagModuleId, id));
            return Results.NotFound();
        }
        module.RunCount += 1;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var count = await db.UserModules.CountAsync(x => x.ModuleId == id);
        ModuleMetrics.ModulesStartSuccess.Add(1,
            new KeyValuePair<string, object?>(ModuleMetrics.TagModuleId, id),
            new KeyValuePair<string, object?>(ModuleMetrics.TagModuleName, module.Name));
        ModuleMetrics.ModulesStartDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "success"),
            new KeyValuePair<string, object?>(ModuleMetrics.TagModuleId, id),
            new KeyValuePair<string, object?>(ModuleMetrics.TagModuleName, module.Name));
        return Results.Ok(ModuleMapper.ToDto(module, count));
    }
}