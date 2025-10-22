using System.Collections.Generic;

namespace Pw.Hub.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class AccountServer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OptionId { get; set; } = string.Empty; // shard option value
    public string Name { get; set; } = string.Empty; // shard option text

    // OptionId of the character selected as default for this server; null or empty means "не выбран"
    public string DefaultCharacterOptionId { get; set; }

    public List<AccountCharacter> Characters { get; set; } = new();
    public Account Account { get; set; }
    public string AccountId { get; set; }

    [NotMapped]
    public List<AccountCharacter> CharactersWithPlaceholder
    {
        get
        {
            var list = new List<AccountCharacter> { new AccountCharacter { OptionId = null, Name = "— не выбран —" } };
            if (Characters != null && Characters.Count > 0)
                list.AddRange(Characters);
            return list;
        }
    }
}
