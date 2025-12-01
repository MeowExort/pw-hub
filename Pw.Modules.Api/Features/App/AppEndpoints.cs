namespace Pw.Modules.Api.Features.App;

public static class AppEndpoints
{
    public static RouteGroupBuilder MapApp(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app");
        // Apply specific CORS policy allowing pw-hub.ru for this group
        group.RequireCors("PwHub");

        UploadVersionEndpoint.Map(group);
        ManifestEndpoint.Map(group);
        StatsEndpoint.Map(group);
        return group;
    }
}
