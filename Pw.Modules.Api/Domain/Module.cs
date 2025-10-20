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

    public sealed class UserModule
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public Guid ModuleId { get; set; }
        public Module Module { get; set; } = null!;
        public DateTimeOffset InstalledAt { get; set; }
    }

    public sealed class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public bool Developer { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<Session> Sessions { get; set; } = new();
    }

    public sealed class Session
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}