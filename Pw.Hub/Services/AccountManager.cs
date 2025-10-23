﻿using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class AccountManager(IBrowser browser) : IAccountManager
{
    public event Action<Account> CurrentAccountChanged;
    public event Action<Account> CurrentAccountDataChanged;
    private const string CookieFileExtension = ".json";
    private const string CookieFolder = "Cookies";

    public Account CurrentAccount { get; private set; }
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private PropertyChangedEventHandler _accountPropertyChangedHandler;

    public async Task<Account[]> GetAccountsAsync()
    {
        await using var db = new AppDbContext();
        return db.Accounts
            .Include(x => x.Servers)
            .ThenInclude(x => x.Characters)
            .ToArray();
    }

    public async Task ChangeAccountAsync(string accountId)
    {
        await _semaphoreSlim.WaitAsync();
        await SaveCookies();
        await using var db = new AppDbContext();
        var account = await db.Accounts.FindAsync(accountId);
        // detach from previous account PropertyChanged
        if (CurrentAccount is INotifyPropertyChanged oldInpc && _accountPropertyChangedHandler != null)
        {
            try { oldInpc.PropertyChanged -= _accountPropertyChangedHandler; } catch { }
        }
        CurrentAccount = account ?? throw new InvalidOperationException("Account not found");
        // attach to new account PropertyChanged
        AttachToCurrentAccountPropertyChanged();

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
        try { CurrentAccountChanged?.Invoke(account); } catch { }
        _semaphoreSlim.Release();
    }

    public async Task<bool> IsAuthorizedAsync()
    {
        var exists = await browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 500);
        if (!exists)
            return false;
        
        var siteId = await GetSiteId();
        if (siteId != CurrentAccount.SiteId)
            return false;

        return true;
    }

    public async Task<Account> GetAccountAsync()
    {
        return CurrentAccount;
    }

    public async Task<string> GetSiteId()
    {
        var result = await browser.ExecuteScriptAsync("$('.auth_h > h2 > a > strong')[0].innerText");
        return result.Trim('"').Replace("null", "");
    }

    private void AttachToCurrentAccountPropertyChanged()
    {
        if (CurrentAccount is INotifyPropertyChanged inpc)
        {
            _accountPropertyChangedHandler ??= (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.PropertyName) ||
                    args.PropertyName == nameof(Account.ImageSource) ||
                    args.PropertyName == nameof(Account.SiteId) ||
                    args.PropertyName == nameof(Account.Name))
                {
                    try { CurrentAccountDataChanged?.Invoke(CurrentAccount); } catch { }
                }
            };
            try { inpc.PropertyChanged += _accountPropertyChangedHandler; } catch { }
        }
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
        var siteId = await GetSiteId();
        if (siteId != CurrentAccount.SiteId)
            return;
        var cookies = await browser.GetCookiesAsync();
        var json = JsonSerializer.Serialize(cookies, JsonSerializerOptions.Web);
        await File.WriteAllTextAsync(GetCookieFilePath(), json);
    }
}