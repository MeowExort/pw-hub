using Pw.Hub.Models;

namespace Pw.Hub.Abstractions;

public interface IAccountManager
{
    Task<Account[]> GetAccountsAsync();
    Task ChangeAccountAsync(string accountId);
    Task<bool> IsAuthorizedAsync();
    Task<string> GetAccountAsync();
    string GetAccount();
    
}