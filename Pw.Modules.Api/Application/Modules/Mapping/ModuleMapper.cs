using Markdig;
using System.Text.Json;
using Pw.Modules.Api.Application.Modules.Dtos;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Application.Modules.Mapping;

public static class ModuleMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ModuleDto ToDto(Module m, int installCount)
    {
        var inputs = string.IsNullOrWhiteSpace(m.InputsJson)
            ? Array.Empty<InputDefinitionDto>()
            : (JsonSerializer.Deserialize<InputDefinitionDto[]>(m.InputsJson, JsonOptions) ?? Array.Empty<InputDefinitionDto>());

        return new ModuleDto
        {
            Id = m.Id,
            Name = m.Name,
            Version = m.Version,
            Description = m.Description,
            DescriptionHtml = Markdown.ToHtml(m.Description ?? string.Empty),
            Script = m.Script,
            Inputs = inputs,
            RunCount = m.RunCount,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
            InstallCount = installCount,
            OwnerUserId = m.OwnerUserId
        };
    }

    public static string SerializeInputs(InputDefinitionDto[] inputs)
        => JsonSerializer.Serialize(inputs ?? Array.Empty<InputDefinitionDto>(), JsonOptions);
}