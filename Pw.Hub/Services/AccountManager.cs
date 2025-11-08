using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Infrastructure.Logging;
using System.Diagnostics;

namespace Pw.Hub.Services;

public readonly record struct AccountSwitchOptions(bool CreateFreshSession, BrowserSessionIsolationMode SessionMode);

public class AccountManager(IBrowser browser) : IAccountManager
{
    private static readonly ILogger _log = Log.For<AccountManager>();
    public event Action<Account> CurrentAccountChanged;
    public event Action<Account> CurrentAccountDataChanged;
    public event Action<bool> CurrentAccountChanging;
    private const string CookieFileExtension = ".json";
    private const string CookieFolder = "Cookies";

    public Account CurrentAccount { get; private set; }
    public bool IsChanging => _isChanging;
    private volatile bool _isChanging;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private PropertyChangedEventHandler _accountPropertyChangedHandler;

    // Optional hook to ensure a brand new browser session before applying target account's cookies
    public Func<Task>? EnsureNewSessionBeforeSwitchAsync { get; set; }

    // Перегрузка для v1: централизованное создание новой сессии внутри AccountManager
    public async Task ChangeAccountAsync(string accountId, AccountSwitchOptions opts)
    {
        await _semaphoreSlim.WaitAsync();
        SetChanging(true);
        try
        {
            var sw = Stopwatch.StartNew();
            _log.Info("ChangeAccount(v1): start", new System.Collections.Generic.Dictionary<string, object?> { { "accountId", accountId }, { "createFreshSession", opts.CreateFreshSession }, { "sessionMode", opts.SessionMode.ToString() } });
            // Save cookies from the previous account before session reset
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

            var previousUri = browser.Source?.ToString() ?? "https://pwonline.ru";

            // Централизованно создаём новую сессию, если это требуется опциями
            if (opts.CreateFreshSession)
            {
                try { await browser.CreateNewSessionAsync(opts.SessionMode); } catch { }
                // Lua API v1: применяем анти‑детект автоматически и закрепляем отпечаток за аккаунтом
                try
                {
                    var fp = await LoadOrCreateFingerprintAsync(CurrentAccount.Id);
                    await browser.ApplyAntiDetectAsync(fp);
                }
                catch { }
            }
            else if (EnsureNewSessionBeforeSwitchAsync != null)
            {
                // Совместимость: если хук задан, выполняем его (для старых сценариев)
                try { await EnsureNewSessionBeforeSwitchAsync(); } catch { }
            }

            // Гарантируем, что новая сессия создана и ядро WebView2 готово к операциям (CoreWebView2 инициализирован)
            await EnsureBrowserReadyAsync();
            try { _log.Debug("ChangeAccount(v1): browser ready"); } catch { }

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

            await Task.Delay(3000);
            await browser.SetCookieAsync(cookies);
            // After creating a brand-new session, force a deterministic navigation instead of Reload to avoid staying on about:blank
            await browser.NavigateAsync(previousUri);
            await browser.ReloadAsync();
            var pageLoaded = await browser.WaitForElementExistsAsync(".main_menu", 30000);
            if (!pageLoaded)
            {
                // Мягкий повтор: пробуем перейти на базовый URL и подождать ещё раз
                try { await browser.NavigateAsync("https://pwonline.ru/"); } catch { }
                pageLoaded = await browser.WaitForElementExistsAsync(".main_menu", 15000);
                if (!pageLoaded)
                {
                    throw new InvalidOperationException("Page did not load in time (after retry)");
                }
            }

            var isAuthorized = await IsAuthorizedAsync();
            if (!isAuthorized)
            {
                // Мягкий повтор авторизационной проверки: один раз перезайдём на базовую
                try { await browser.NavigateAsync("https://pwonline.ru/"); } catch { }
                await Task.Delay(300);
                var pageReady = await browser.WaitForElementExistsAsync(".main_menu", 10000);
                if (pageReady)
                {
                    isAuthorized = await IsAuthorizedAsync();
                }
                if (!isAuthorized)
                {
                    return;
                }
            }

            await SaveCookies();
            account.LastVisit = DateTime.UtcNow;
            db.Update(account);
            await db.SaveChangesAsync();
            try { CurrentAccountChanged?.Invoke(account); } catch { }
        }
        finally
        {
            SetChanging(false);
            _semaphoreSlim.Release();
        }
    }

    private void SetChanging(bool value)
    {
        _isChanging = value;
        try { CurrentAccountChanging?.Invoke(value); } catch { }
    }

    /// <summary>
    /// Гарантирует готовность браузера к операциям (инициализирован CoreWebView2) перед установкой кук/навигацией.
    /// Поскольку IBrowser не раскрывает прямого EnsureCore, используем безвредное выполнение JS, которое внутри
    /// реализации WebCoreBrowser вызывает EnsureCoreWebView2Async().
    /// </summary>
    private async Task EnsureBrowserReadyAsync()
    {
        try
        {
            // Ничего не делает на странице, но заставляет инициализировать ядро WebView2, если оно ещё не готово
            await browser.ExecuteScriptAsync("(function(){return 'ok';})()");
        }
        catch
        {
            // Игнорируем: готовность будет проверена повторно следующими вызовами
        }
    }

    public async Task<Account[]> GetAccountsAsync()
    {
        await using var db = new AppDbContext();
        return db.Accounts
            .Include(x=> x.Squad)
            .Include(x => x.Servers)
            .ThenInclude(x => x.Characters)
            .ToArray();
    }

    public async Task ChangeAccountAsync(string accountId)
    {
        await _semaphoreSlim.WaitAsync();
        SetChanging(true);
        try
        {
            // Save cookies from the previous account before session reset
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

            var previousUri = browser.Source?.ToString() ?? "https://pwonline.ru";
            // Ensure a fresh isolated browser session before applying target account cookies
            if (EnsureNewSessionBeforeSwitchAsync != null)
            {
                try { await EnsureNewSessionBeforeSwitchAsync(); } catch { }
            }

            // Гарантируем готовность браузера (CoreWebView2 инициализирован) перед установкой кук
            await EnsureBrowserReadyAsync();

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
            // After creating a brand-new session, force a deterministic navigation instead of Reload to avoid staying on about:blank
            await browser.NavigateAsync(previousUri);
            var pageLoaded = await browser.WaitForElementExistsAsync(".main_menu", 30000);
            if (!pageLoaded)
            {
                // Мягкий повтор: пробуем перейти на базовый URL и подождать ещё раз
                try { await browser.NavigateAsync("https://pwonline.ru/"); } catch { }
                pageLoaded = await browser.WaitForElementExistsAsync(".main_menu", 15000);
                if (!pageLoaded)
                {
                    throw new InvalidOperationException("Page did not load in time (after retry)");
                }
            }

            var isAuthorized = await IsAuthorizedAsync();
            if (!isAuthorized)
            {
                // Мягкий повтор авторизационной проверки: один раз перезайдём на базовую
                try { await browser.NavigateAsync("https://pwonline.ru/"); } catch { }
                await Task.Delay(300);
                var pageReady = await browser.WaitForElementExistsAsync(".main_menu", 10000);
                if (pageReady)
                {
                    isAuthorized = await IsAuthorizedAsync();
                }
                if (!isAuthorized)
                {
                    return;
                }
            }

            await SaveCookies();
            account.LastVisit = DateTime.UtcNow;
            db.Update(account);
            await db.SaveChangesAsync();
            try { CurrentAccountChanged?.Invoke(account); } catch { }
        }
        finally
        {
            SetChanging(false);
            _semaphoreSlim.Release();
        }
    }

    public async Task<bool> IsAuthorizedAsync()
    {
        // На "холодном" профиле (SeparateProfile) элементы и данные могут появляться с задержкой,
        // поэтому ждём дольше и допускаем повторное чтение SiteId.
        var exists = await browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 500);
        if (!exists)
            return false;
        
        var siteId = await GetSiteId();
        if (string.IsNullOrWhiteSpace(siteId))
        {
            await Task.Delay(200);
            siteId = await GetSiteId();
        }
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

    // Fingerprint persistence (per account) for Lua API v1
    private const string FingerprintFolder = "Fingerprints";
    private const string FingerprintFileExtension = ".fp.json";

    private string GetFingerprintFilePath(string accountId)
    {
        var safeId = string.IsNullOrWhiteSpace(accountId) ? "unknown" : accountId.Replace(":", "_").Replace("/", "_");
        var path = Path.Combine(FingerprintFolder, $"{safeId}{FingerprintFileExtension}");
        if (!Directory.Exists(FingerprintFolder))
            Directory.CreateDirectory(FingerprintFolder);
        return path;
    }

    private async Task<FingerprintProfile> LoadOrCreateFingerprintAsync(string accountId)
    {
        try
        {
            var file = GetFingerprintFilePath(accountId);
            if (File.Exists(file))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var fp = JsonSerializer.Deserialize<FingerprintProfile>(json, JsonSerializerOptions.Web);
                    if (fp != null) return fp;
                }
                catch { }
            }

            var created = FingerprintGenerator.Generate();
            try
            {
                var json = JsonSerializer.Serialize(created, JsonSerializerOptions.Web);
                await File.WriteAllTextAsync(GetFingerprintFilePath(accountId), json);
            }
            catch { }
            return created;
        }
        catch
        {
            // Fallback: generate but do not persist on fatal error
            return FingerprintGenerator.Generate();
        }
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
