using System;
using System.Collections.Generic;

namespace Pw.Modules.Api.Domain
{
    public sealed class Module
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // Markdown description, same as modules.json "description"
        public string? Description { get; set; }
        public string Script { get; set; } = string.Empty;
        // JSON storage for inputs array to keep EF simple; exposed via DTOs as strongly-typed objects
        public string InputsJson { get; set; } = "[]";
        public long RunCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<UserModule> UserModules { get; set; } = new();
    }

    public sealed class UserModule
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public Guid ModuleId { get; set; }
        public Module Module { get; set; } = null!;
        public DateTimeOffset InstalledAt { get; set; }
    }
}