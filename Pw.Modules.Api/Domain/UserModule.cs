namespace Pw.Modules.Api.Domain;

public sealed class UserModule
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;
    public DateTimeOffset InstalledAt { get; set; }
}