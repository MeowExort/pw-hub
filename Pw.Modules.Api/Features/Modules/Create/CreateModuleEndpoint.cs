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

    public static async Task<IResult> Handle(ModulesDbContext db, CreateModuleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Script))
        {
            return Results.BadRequest(new { message = "Поле 'script' обязательно" });
        }

        var now = DateTimeOffset.UtcNow;
        var m = new Module
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            Script = req.Script.Trim(),
            InputsJson = ModuleMapper.SerializeInputs(req.Inputs ?? Array.Empty<InputDefinitionDto>()),
            RunCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Modules.Add(m);
        await db.SaveChangesAsync();
        return Results.Created($"/api/modules/{m.Id}", ModuleMapper.ToDto(m, 0));
    }
}