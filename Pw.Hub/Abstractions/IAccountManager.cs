using Pw.Hub.Models;

namespace Pw.Hub.Abstractions;

public interface IAccountManager
{
    // Raised when current account successfully changed and authorized
    event Action<Account> CurrentAccountChanged;
    // Raised when any property of the current account changes (e.g., ImageSource)
    event Action<Account> CurrentAccountDataChanged;

    Account CurrentAccount { get; }
    
    Task<Account[]> GetAccountsAsync();
    Task ChangeAccountAsync(string accountId);
    Task<bool> IsAuthorizedAsync();
    Task<Account> GetAccountAsync();
    Task<string> GetSiteId();
}