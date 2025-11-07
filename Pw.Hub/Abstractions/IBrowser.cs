using Pw.Hub.Models;

namespace Pw.Hub.Abstractions;

public enum BrowserSessionIsolationMode
{
    // Использовать InPrivate (инкогнито) профиль WebView2. Данные не сохраняются и шарятся между всеми InPrivate-инстансами.
    InPrivate = 0,
    // Использовать отдельную папку профиля (UserDataFolder) для полной изоляции.
    SeparateProfile = 1,
}

public interface IBrowser
{
    Task<string> ExecuteScriptAsync(string script);
    Task NavigateAsync(string url);
    Task ReloadAsync();
    Task<bool> ElementExistsAsync(string selector);
    Task<bool> WaitForElementExistsAsync(string selector, int timeoutMs = 5000);
    Task<Cookie[]> GetCookiesAsync();
    Task SetCookieAsync(Cookie[] cookie);
    /// <summary>
    /// Пересоздаёт сессию браузера. Если указан overrideMode, используется он; иначе — режим,
    /// заданный в конструкторе реализации (например, InPrivate или SeparateProfile).
    /// </summary>
    Task CreateNewSessionAsync(BrowserSessionIsolationMode? overrideMode = null);
    Uri Source { get; }
}