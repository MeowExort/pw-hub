using System.Diagnostics;
using Pw.Modules.Api.Application.Auth.Dtos;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Modules;

namespace Pw.Modules.Api.Features.Auth.Me;

public static class MeEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", Handle).WithName("Me");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db)
    {
        ModuleMetrics.AuthCheckAttempts.Add(1);
        var sw = Stopwatch.StartNew();
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null)
        {
            ModuleMetrics.AuthCheckFailure.Add(1, new KeyValuePair<string, object?>(ModuleMetrics.TagReason, "unauthorized"));
            ModuleMetrics.AuthCheckDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "failure"));
            return Results.Unauthorized();
        }
        ModuleMetrics.AuthCheckSuccess.Add(1);
        ModuleMetrics.AuthCheckDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ModuleMetrics.TagResult, "success"));
        return Results.Ok(new UserDto { UserId = user.Id, Username = user.Username, Developer = user.Developer, TelegramId = user.TelegramId, TelegramUsername = user.TelegramUsername, TelegramLinkedAt = user.TelegramLinkedAt });
    }
}