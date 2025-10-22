namespace Pw.Modules.Api.Domain
{
    public sealed class Module
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = "1.0.0";

        // Markdown description, same as modules.json "description"
        public string? Description { get; set; }

        public string Script { get; set; } = string.Empty;

        // JSON storage for inputs array to keep EF simple; exposed via DTOs as strongly-typed objects
        public string InputsJson { get; set; } = "[]";
        public long RunCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? OwnerUserId { get; set; }
        public List<UserModule> UserModules { get; set; } = new();
    }
}