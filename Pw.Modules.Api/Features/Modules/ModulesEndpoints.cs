using Microsoft.AspNetCore.Builder;

namespace Pw.Modules.Api.Features.Modules;

public static class ModulesEndpoints
{
    public static RouteGroupBuilder MapModules(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/modules");
        // Map per-slice endpoints
        Search.SearchModulesEndpoint.Map(group);
        Create.CreateModuleEndpoint.Map(group);
        Update.UpdateModuleEndpoint.Map(group);
        Delete.DeleteModuleEndpoint.Map(group);
        Install.InstallModuleEndpoint.Map(group);
        Uninstall.UninstallModuleEndpoint.Map(group);
        Run.IncrementRunEndpoint.Map(group);
        return group;
    }
}