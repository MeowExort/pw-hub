namespace Pw.Modules.Api.Domain;

public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool Developer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Telegram link fields
    public long? TelegramId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTimeOffset? TelegramLinkedAt { get; set; }

    public List<Session> Sessions { get; set; } = new();
}