using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Dtos;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Search;

public static class SearchModulesEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("SearchModules");
    }

    public static async Task<IResult> Handle(ModulesDbContext db, string? q, string? tags, string? sort, string? order, int page = 1, int pageSize = 20, int? sinceDays = null)
    {
        // Increment searches metric at the beginning of a search request
        ModuleMetrics.Searches.Add(1);

        var query = db.Modules.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m => m.Name.ToLower().Contains(term.ToLower())
                                     || (m.Description != null && m.Description.ToLower().Contains(term.ToLower())));
        }

        // 'tags' is ignored to maintain backward-compatible signature, as modules model no longer contains tags.

        var windowDays = sinceDays.HasValue && sinceDays.Value > 0 ? sinceDays.Value : 30;
        var since = DateTimeOffset.UtcNow.AddDays(-windowDays);

        var modulesWithCounts = query
            .Select(m => new
            {
                Module = m,
                InstallCount = db.UserModules.Count(um => um.ModuleId == m.Id),
                RecentInstallCount = db.UserModules.Count(um => um.ModuleId == m.Id && um.InstalledAt >= since),
                AuthorUsername = db.Users.Where(u => u.Id == m.OwnerUserId!).Select(u => u.Username).FirstOrDefault()
            });

        var ord = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        switch ((sort ?? string.Empty).ToLower())
        {
            case "popular":
                modulesWithCounts = ord == "asc"
                    ? modulesWithCounts
                        .OrderBy(x => x.RecentInstallCount)
                        .ThenBy(x => x.InstallCount)
                        .ThenBy(x => x.Module.Name)
                    : modulesWithCounts
                        .OrderByDescending(x => x.RecentInstallCount)
                        .ThenByDescending(x => x.InstallCount)
                        .ThenBy(x => x.Module.Name);
                break;
            case "installs":
                modulesWithCounts = ord == "asc"
                    ? modulesWithCounts.OrderBy(x => x.InstallCount).ThenBy(x => x.Module.Name)
                    : modulesWithCounts.OrderByDescending(x => x.InstallCount).ThenBy(x => x.Module.Name);
                break;
            case "runs":
                modulesWithCounts = ord == "asc"
                    ? modulesWithCounts.OrderBy(x => x.Module.RunCount).ThenBy(x => x.Module.Name)
                    : modulesWithCounts.OrderByDescending(x => x.Module.RunCount).ThenBy(x => x.Module.Name);
                break;
            case "name":
            default:
                modulesWithCounts = ord == "asc"
                    ? modulesWithCounts.OrderBy(x => x.Module.Name)
                    : modulesWithCounts.OrderByDescending(x => x.Module.Name);
                break;
        }

        var total = await modulesWithCounts.CountAsync();
        var items = await modulesWithCounts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResponse<ModuleDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items.Select(x => ModuleMapper.ToDto(x.Module, x.InstallCount, x.AuthorUsername)).ToList()
        };

        return Results.Ok(result);
    }
}

public class PagedResponse<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<T> Items { get; set; } = new();
}