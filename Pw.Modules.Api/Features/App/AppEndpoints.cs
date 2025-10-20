namespace Pw.Modules.Api.Features.App;

public static class AppEndpoints
{
    public static RouteGroupBuilder MapApp(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app");
        UploadVersionEndpoint.Map(group);
        ManifestEndpoint.Map(group);
        return group;
    }
}
