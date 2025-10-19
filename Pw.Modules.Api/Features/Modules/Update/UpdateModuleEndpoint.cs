using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Application.Modules.Dtos;
using Pw.Modules.Api.Application.Modules.Mapping;
using Pw.Modules.Api.Data;

namespace Pw.Modules.Api.Features.Modules.Update;

public static class UpdateModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/{id:guid}", Handle).WithName("UpdateModule");
    }

    public static async Task<IResult> Handle(HttpRequest request, ModulesDbContext db, Guid id, UpdateModuleRequest req)
    {
        var token = request.Headers["X-Auth-Token"].FirstOrDefault();
        var user = await Features.Auth.AuthUtils.GetUserByTokenAsync(db, token);
        if (user == null) return Results.Unauthorized();
        var module = await db.Modules.FirstOrDefaultAsync(m => m.Id == id);
        if (module == null) return Results.NotFound();
        if (!user.Developer || !string.Equals(module.OwnerUserId, user.Id, StringComparison.Ordinal))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(req.Script))
            return Results.BadRequest(new { message = "Поле 'script' обязательно" });

        module.Name = req.Name;
        module.Version = string.IsNullOrWhiteSpace(req.Version) ? module.Version : req.Version.Trim();
        module.Description = req.Description;
        module.Script = req.Script.Trim();
        module.InputsJson = ModuleMapper.SerializeInputs(req.Inputs ?? Array.Empty<InputDefinitionDto>());
        module.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var count = await db.UserModules.CountAsync(x => x.ModuleId == id);
        return Results.Ok(ModuleMapper.ToDto(module, count));
    }
}
