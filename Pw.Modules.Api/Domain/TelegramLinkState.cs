namespace Pw.Modules.Api.Domain;

public sealed class TelegramLinkState
{
    public Guid Id { get; set; }
    public string State { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}