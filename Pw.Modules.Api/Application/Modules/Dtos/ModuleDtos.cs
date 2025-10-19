namespace Pw.Modules.Api.Application.Modules.Dtos
{
    public sealed class InputDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; }
    }

    public sealed class CreateModuleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }
        public string Script { get; set; } = string.Empty;
        public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
    }

    public sealed class UpdateModuleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }
        public string Script { get; set; } = string.Empty;
        public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
    }

    public sealed class ModuleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }
        public string DescriptionHtml { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
        public long RunCount { get; set; }
        public int InstallCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? OwnerUserId { get; set; }
    }
}