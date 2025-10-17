using Microsoft.Web.WebView2.Core;

namespace Pw.Hub.Models;

public class Cookie
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public bool IsSession { get; set; }
    public bool IsHttpOnly { get; set; }
    public bool IsSecure { get; set; }
    public CoreWebView2CookieSameSiteKind SameSite { get; set; }

    public static Cookie FromCoreWebView2Cookie(CoreWebView2Cookie coreCookie)
    {
        return new Cookie
        {
            Name = coreCookie.Name,
            Value = coreCookie.Value,
            Domain = coreCookie.Domain,
            Path = coreCookie.Path,
            Expires = coreCookie.Expires,
            IsHttpOnly = coreCookie.IsHttpOnly,
            IsSecure = coreCookie.IsSecure,
            IsSession = coreCookie.IsSession,
            SameSite = coreCookie.SameSite
        };
    }
}