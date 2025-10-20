namespace Pw.Modules.Api.Features.App;

public static class ManifestEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/manifest", Handle)
            .WithName("GetAppManifest");
    }

    public static async Task<IResult> Handle()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "app-updates", "manifest.json");
        if (!File.Exists(path))
            return Results.NotFound(new { message = "Манифест не найден" });
        var json = await File.ReadAllTextAsync(path);
        return Results.Text(json, "application/json; charset=utf-8");
    }
}
