using Microsoft.AspNetCore.StaticFiles;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Auth;

namespace Pw.Modules.Api.Features.App;

public static class UploadVersionEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/versions", Handle)
            .WithName("UploadAppVersion")
            .DisableAntiforgery();
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();
        if (user.Id != "ba791076c430460eaf6cd1c7391d1d9b") return Results.Forbid();

        if (!request.HasFormContentType)
            return Results.BadRequest(new { message = "Ожидается multipart/form-data" });

        var form = await request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        var version = form["version"].FirstOrDefault();
        var mandatoryStr = form["mandatory"].FirstOrDefault();
        var releaseNotes = form["releaseNotes"].FirstOrDefault();

        if (file == null || file.Length == 0)
            return Results.BadRequest(new { message = "Файл обязателен" });
        if (string.IsNullOrWhiteSpace(version))
            return Results.BadRequest(new { message = "Параметр 'version' обязателен" });

        var updatesDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "app-updates");
        Directory.CreateDirectory(updatesDir);

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
        var safeVer = version!.Trim().Replace(" ", "-");
        var storedName = $"pw-hub-{safeVer}{ext}";
        var storedPath = Path.Combine(updatesDir, storedName);
        await using (var stream = File.Create(storedPath))
        {
            await file.CopyToAsync(stream);
        }

        // Try determine content type (for completeness)
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(storedName, out var contentType))
            contentType = "application/octet-stream";

        // Build public URL
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var fileUrl = $"{baseUrl}/app-updates/{Uri.EscapeDataString(storedName)}";

        // Write manifest file
        var manifest = new
        {
            version = version,
            url = fileUrl,
            releaseNotes = string.IsNullOrWhiteSpace(releaseNotes) ? null : releaseNotes,
            mandatory = bool.TryParse(mandatoryStr, out var m) && m
        };
        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        await File.WriteAllTextAsync(Path.Combine(updatesDir, "manifest.json"), manifestJson, System.Text.Encoding.UTF8);

        return Results.Created($"/api/app/versions/{version}", new { version, url = fileUrl, contentType });
    }
}
