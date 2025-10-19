using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Pw.Modules.Api.Application.Modules.Dtos;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Features.Modules.Create;

public static class CreateModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateModule");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, CreateModuleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Script))
        {
            return Results.BadRequest(new { message = "Поле 'script' обязательно" });
        }

        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();
        if (!user.Developer) return Results.Forbid();

        var now = DateTimeOffset.UtcNow;
        var m = new Module
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Version = string.IsNullOrWhiteSpace(req.Version) ? "1.0.0" : req.Version.Trim(),
            Description = req.Description,
            Script = req.Script.Trim(),
            InputsJson = ModuleMapper.SerializeInputs(req.Inputs ?? Array.Empty<InputDefinitionDto>()),
            RunCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerUserId = user.Id
        };
        db.Modules.Add(m);
        await db.SaveChangesAsync();
        return Results.Created($"/api/modules/{m.Id}", ModuleMapper.ToDto(m, 0));
    }
}