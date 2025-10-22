using System.IO;
using System.Text.Json;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class AccountManager(IBrowser browser) : IAccountManager
{
    private const string CookieFileExtension = ".json";
    private const string CookieFolder = "Cookies";

    private Account CurrentAccount;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async Task<Account[]> GetAccountsAsync()
    {
        await using var db = new AppDbContext();
        return db.Accounts.ToArray();
    }

    public async Task ChangeAccountAsync(string accountId)
    {
        await _semaphoreSlim.WaitAsync();
        await SaveCookies();
        await using var db = new AppDbContext();
        var account = await db.Accounts.FindAsync(accountId);
        CurrentAccount = account ?? throw new InvalidOperationException("Account not found");

        Cookie[] cookies;
        if (!File.Exists(GetCookieFilePath()))
        {
            cookies = [];
        }
        else
        {
            var json = await File.ReadAllTextAsync(GetCookieFilePath());
            cookies = JsonSerializer.Deserialize<Cookie[]>(json, JsonSerializerOptions.Web) ?? [];
        }

        await browser.SetCookieAsync(cookies);
        await browser.ReloadAsync();
        var pageLoaded = await browser.WaitForElementExistsAsync(".main_menu", 30000);
        if (!pageLoaded)
        {
            _semaphoreSlim.Release();
            throw new InvalidOperationException("Page did not load in time");
        }
        var isAuthorized = await IsAuthorizedAsync();
        if (!isAuthorized)
        {
            _semaphoreSlim.Release();
            return;
        }

        await SaveCookies();
        account.LastVisit = DateTime.UtcNow;
        db.Update(account);
        await db.SaveChangesAsync();
        _semaphoreSlim.Release();
    }

    public async Task<bool> IsAuthorizedAsync()
    {
        var exists = await browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 500);
        if (!exists)
            return false;
        var result = await GetAccountAsync();
        return !string.IsNullOrEmpty(result);
    }

    public async Task<string> GetAccountAsync()
    {
        var result = await browser.ExecuteScriptAsync("$('.auth_h > h2 > a > strong')[0].innerText");
        return result.Trim('"').Replace("null", "");
    }

    public string GetAccount()
    {
        return GetAccountAsync().GetAwaiter().GetResult();
    }

    private string GetCookieFilePath()
    {
        var path = Path.Combine(CookieFolder, $"{CurrentAccount.Id:N}{CookieFileExtension}");
        if (!Directory.Exists(CookieFolder))
            Directory.CreateDirectory(CookieFolder);
        return path;
    }

    private async Task SaveCookies()
    {
        if (CurrentAccount == null)
            return;
        var cookies = await browser.GetCookiesAsync();
        var json = JsonSerializer.Serialize(cookies, JsonSerializerOptions.Web);
        await File.WriteAllTextAsync(GetCookieFilePath(), json);
    }
}