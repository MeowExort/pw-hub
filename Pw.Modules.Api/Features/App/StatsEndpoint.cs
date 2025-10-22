using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.App;

public static class StatsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/stats", GetStats)
            .WithName("GetAppStats")
            .WithSummary("Возвращает статистику по пользователям и модулям")
            .WithDescription("Количество активных пользователей (по незавершенным сессиям), количество модулей и общее количество запусков модулей")
            .Produces<StatsResponse>(StatusCodes.Status200OK);
    }

    private static async Task<StatsResponse> GetStats(ModulesDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Активные пользователи: есть хотя бы одна неистекшая сессия
        var activeUsers = await db.Sessions
            .Where(s => s.ExpiresAt > now)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct);

        var modules = await db.Modules.CountAsync(ct);

        // Количество запусков модулей: суммарное значение RunCount по всем модулям
        var moduleRuns = await db.Modules.SumAsync(m => (long)m.RunCount, ct);

        return new StatsResponse(activeUsers, modules, moduleRuns);
    }

    public sealed record StatsResponse(int ActiveUsers, int Modules, long ModuleRuns);
}