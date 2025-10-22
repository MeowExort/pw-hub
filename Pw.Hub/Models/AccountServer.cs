using System.Collections.Generic;

namespace Pw.Hub.Models;

public class AccountServer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OptionId { get; set; } = string.Empty; // shard option value
    public string Name { get; set; } = string.Empty; // shard option text
    public List<AccountCharacter> Characters { get; set; } = new();
    public Account Account { get; set; }
    public string AccountId { get; set; }
}
