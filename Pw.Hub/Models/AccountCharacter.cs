namespace Pw.Hub.Models;

public class AccountCharacter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OptionId { get; set; } // option value
    public string Name { get; set; } = string.Empty; // option text
    public AccountServer Server { get; set; }
    public string ServerId { get; set; }
}
