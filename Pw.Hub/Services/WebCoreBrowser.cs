using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;
using System.Windows.Threading;
using System.Windows;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Pw.Hub.Services;

public class WebCoreBrowser(IWebViewHost host, BrowserSessionIsolationMode mode = BrowserSessionIsolationMode.SeparateProfile) : IBrowser
{
    private WebView2 _webView = host.Current;
    private readonly IWebViewHost _host = host;
    private readonly BrowserSessionIsolationMode _mode = mode;
    // Текущая папка пользовательских данных (профиль) для данного инстанса браузера
    private string _userDataDir;

    // Прокси-настройки для текущего инстанса (применяются при создании новой сессии)
    private string _proxyHost;
    private int _proxyPort;
    private string _proxyUser;
    private string _proxyPassword;

    /// <summary>
    /// Папка профиля WebView2, используемая текущим инстансом. Нужна для диагностики/очистки.
    /// </summary>
    public string UserDataFolder => _userDataDir;

    private async Task<T> OnUiAsync<T>(Func<Task<T>> func)
    {
        if (_webView.Dispatcher.CheckAccess())
            return await func();
        return await _webView.Dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task.Unwrap();
    }

    private async Task OnUiAsync(Func<Task> func)
    {
        if (_webView.Dispatcher.CheckAccess())
        {
            await func();
            return;
        }
        await _webView.Dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task;
    }

    private async Task EnsureCoreAndSetBackgroundAsync()
    {
        await _webView.EnsureCoreWebView2Async();
        try
        {
            // Ensure about:blank (and any transparent area) is dark
            _webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30);
        }
        catch
        {
        }
    }

    public Task<string> ExecuteScriptAsync(string script)
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
            var result = await _webView.ExecuteScriptAsync(script);
            return result.Trim('"');
        });
    }

    public Task NavigateAsync(string url)
    {
        return OnUiAsync(async () =>
        {
            var uri = new Uri(url);
            // Разрешаем навигацию только на доверенные домены
            bool IsAllowedHost(string host)
            {
                if (string.IsNullOrWhiteSpace(host)) return false;
                // Базовые домены
                if (host.Equals("pwonline.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.Equals("2ip.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.Equals("vkplay.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.Equals("vk.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.Equals("vk.com", StringComparison.OrdinalIgnoreCase)) return true;
                // Поддомены
                if (host.EndsWith(".pwonline.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.EndsWith(".2ip.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.EndsWith(".vkplay.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.EndsWith(".vk.ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (host.EndsWith(".vk.com", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            if (!IsAllowedHost(uri.Host))
                return;
            await EnsureCoreAndSetBackgroundAsync();
            _webView.CoreWebView2.Navigate(url);
        });
    }

    public Task ReloadAsync()
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
            _webView.CoreWebView2.Reload();
        });
    }

    public async Task<bool> ElementExistsAsync(string selector)
    {
        var script = $@"
            (function() {{
                return document.querySelector('{selector}') !== null;
            }})()";
        var result = await ExecuteScriptAsync(script);
        return result.Trim().ToLower() == "true";
    }

    public async Task<bool> WaitForElementExistsAsync(string selector, int timeoutMs = 5000)
    {
        var elapsed = 0;
        var interval = 50;
        while (elapsed < timeoutMs)
        {
            var exists = await ElementExistsAsync(selector);
            if (exists)
                return true;
            await Task.Delay(interval);
            elapsed += interval;
        }

        return false;
    }

    public Task<Cookie[]> GetCookiesAsync()
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
            var cookieManager = _webView.CoreWebView2.CookieManager;
            var coreCookies = await cookieManager.GetCookiesAsync(null);
            return coreCookies.Select(Cookie.FromCoreWebView2Cookie).ToArray();
        });
    }

    public Task SetCookieAsync(Cookie[] cookie)
    {
        return OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
            var cookieManager = _webView.CoreWebView2.CookieManager;
            cookieManager.DeleteAllCookies();
            foreach (var c in cookie)
            {
                var coreCookie = cookieManager.CreateCookie(
                    c.Name,
                    c.Value,
                    c.Domain,
                    c.Path);
                coreCookie.Expires = c.Expires;
                coreCookie.IsHttpOnly = c.IsHttpOnly;
                coreCookie.IsSecure = c.IsSecure;
                coreCookie.SameSite = c.SameSite;
                cookieManager.AddOrUpdateCookie(coreCookie);
            }
        });
    }

    public Task CreateNewSessionAsync(BrowserSessionIsolationMode? overrideMode = null)
    {
            return OnUiAsync(async () =>
            {
                var previousUri = _webView?.Source ?? new Uri("https://pwonline.ru");

                string oldDir = _userDataDir;
                string newDir = null;
                var creationProps = new CoreWebView2CreationProperties();

                var effMode = overrideMode ?? _mode;
                if (effMode == BrowserSessionIsolationMode.SeparateProfile)
                {
                    // Полная изоляция через отдельную папку профиля
                    newDir = CreateProfileDir();
                    creationProps.UserDataFolder = newDir;
                }
                else
                {
                    // Основной браузер: используем InPrivate (инкогнито). Папка профиля не задаётся.
                    creationProps.IsInPrivateModeEnabled = true;
                }

                var newWv = new WebView2
                {
                    CreationProperties = creationProps
                };

                // Если задан прокси — передадим его через аргументы запуска браузера
                try
                {
                    if (!string.IsNullOrWhiteSpace(_proxyHost) && _proxyPort > 0)
                    {
                        var arg = $"--proxy-server={_proxyHost}:{_proxyPort}";
                        var existing = creationProps.AdditionalBrowserArguments;
                        creationProps.AdditionalBrowserArguments = string.IsNullOrWhiteSpace(existing) ? arg : (existing + " " + arg);
                    }
                }
                catch { }

                // Готовим новый контрол скрытым и с тёмным фоном до показа
                try { newWv.Visibility = Visibility.Hidden; } catch { }
                try { newWv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }

                // Предзагружаем в визуальное дерево (старый остаётся видимым)
                await _host.PreloadAsync(newWv);

                // Инициализируем ядро и ещё раз страхуем фон
                try { await newWv.EnsureCoreWebView2Async(); } catch { }
                try { newWv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }

                // Перехватываем открытие новых окон (window.open / target=_blank) и
                // перенаправляем в текущем экземпляре вместо создания отдельного окна.
                try
                {
                    newWv.CoreWebView2.NewWindowRequested += (s, e) =>
                    {
                        try
                        {
                            var targetUrl = e.Uri; // может быть пустым
                            e.Handled = true; // не создавать новое окно
                            if (!string.IsNullOrWhiteSpace(targetUrl))
                            {
                                // Используем существующую навигацию с allow‑list фильтром
                                _ = NavigateAsync(targetUrl);
                            }
                        }
                        catch { }
                    };
                }
                catch { }

                // Обработчик базовой аутентификации — используем для прокси-логина/пароля
                try
                {
                    newWv.CoreWebView2.BasicAuthenticationRequested += (s, e) =>
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(_proxyUser))
                            {
                                // Для прокси-аутентификации Uri указывает на прокси-сервер
                                try
                                {
                                    var uri = new Uri(e.Uri);
                                    if (!string.IsNullOrWhiteSpace(_proxyHost) && !uri.Host.Equals(_proxyHost, StringComparison.OrdinalIgnoreCase))
                                        return;
                                }
                                catch { }

                                e.Response.UserName = _proxyUser;
                                e.Response.Password = _proxyPassword ?? string.Empty;
                            }
                        }
                        catch { }
                    };
                }
                catch { }

                newWv.Source = previousUri;

                // Переключаем внутреннюю ссылку и сохраняем сведения о профиле (только для SeparateProfile)
                _webView = newWv;
                _userDataDir = (effMode == BrowserSessionIsolationMode.SeparateProfile) ? newDir : null;

                // Финализация показа при первом старте навигации
                var finalized = false;
                void EnsureFinalize()
                {
                    if (finalized) return;
                    finalized = true;
                    _ = OnUiAsync(async () =>
                    {
                        try { await _host.FinalizeSwapAsync(newWv); } catch { }
                    });
                }

                try
                {
                    newWv.CoreWebView2.NavigationStarting += (s, e) =>
                    {
                        EnsureFinalize();
                    };
                }
                catch
                {
                    EnsureFinalize();
                }

                // Фолбэк: если навигации нет — показать через 500 мс
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    EnsureFinalize();
                });

                // Удаляем старую папку профиля только если мы работаем в режиме SeparateProfile и есть что удалять
                if (effMode == BrowserSessionIsolationMode.SeparateProfile && !string.IsNullOrWhiteSpace(oldDir))
                {
                    _ = TryDeleteDirWithRetries(oldDir);
                }
            });
    }

    /// <summary>
    /// Установить прокси-подключение из строки формата "login:password@ip:port" или "ip:port".
    /// Вызывать до CreateNewSessionAsync, чтобы прокси применился к профилю/процессу.
    /// </summary>
    public void SetProxyFromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { _proxyHost = null; _proxyPort = 0; _proxyUser = null; _proxyPassword = null; return; }
        try
        {
            var s = value.Trim();
            string credsPart = null;
            string hostPart = s;
            var atIdx = s.LastIndexOf('@');
            if (atIdx > 0)
            {
                credsPart = s.Substring(0, atIdx);
                hostPart = s.Substring(atIdx + 1);
            }

            var hp = hostPart.Split(':');
            if (hp.Length >= 2 && int.TryParse(hp[1], out var port))
            {
                _proxyHost = hp[0];
                _proxyPort = port;
            }
            else
            {
                // Невалидно — сбрасываем
                _proxyHost = null;
                _proxyPort = 0;
            }

            _proxyUser = null;
            _proxyPassword = null;
            if (!string.IsNullOrWhiteSpace(credsPart))
            {
                var cp = credsPart.Split(':');
                _proxyUser = cp.ElementAtOrDefault(0);
                _proxyPassword = cp.Length > 1 ? string.Join(":", cp.Skip(1)) : null; // допускаем двоеточия в пароле
            }
        }
        catch
        {
            _proxyHost = null;
            _proxyPort = 0;
            _proxyUser = null;
            _proxyPassword = null;
        }
    }

    public async Task ApplyAntiDetectAsync(FingerprintProfile profile)
    {
        if (profile == null) return;
        await OnUiAsync(async () =>
        {
            await EnsureCoreAndSetBackgroundAsync();
            try { _webView.CoreWebView2.Settings.UserAgent = profile.UserAgent; } catch { }

            // Build spoofing script
            var langArray = string.Join(",", profile.Languages.Select(l => $"\"{l}\""));
            var tzOffset = profile.TimezoneOffsetMinutes;
            var hw = profile.HardwareConcurrency;
            var dm = profile.DeviceMemory;
            var sw = profile.ScreenWidth;
            var sh = profile.ScreenHeight;
            var cd = profile.ColorDepth;
            var platform = profile.Platform ?? "Win32";
            var vendor = profile.Vendor ?? "Google Inc.";
            var vendorSub = profile.VendorSub ?? string.Empty;
            var product = profile.Product ?? "Gecko";
            var productSub = profile.ProductSub ?? "20030107";
            var webglVendor = profile.WebglVendor ?? "ANGLE (Intel)";
            var webglRenderer = profile.WebglRenderer ?? "ANGLE (Intel, Intel(R) UHD Graphics)";
            var canvasNoise = profile.CanvasNoise;

            var script = $@"
(function() {{
  try {{
    const define = (obj, prop, val) => {{
      try {{ Object.defineProperty(obj, prop, {{ get: () => val, configurable: true }}); }} catch {{}}
    }};

    // navigator spoof
    const nav = navigator;
    define(nav, 'platform', '{platform}');
    define(nav, 'vendor', '{vendor}');
    define(nav, 'vendorSub', '{vendorSub}');
    define(nav, 'product', '{product}');
    define(nav, 'productSub', '{productSub}');
    define(nav, 'hardwareConcurrency', {hw});
    define(nav, 'deviceMemory', {dm});
    define(nav, 'language', {(profile.Languages?.Count > 0 ? $"'{profile.Languages[0]}'" : "'en-US'")});
    Object.defineProperty(nav, 'languages', {{ get: () => [{langArray}], configurable: true }});
    define(nav, 'userAgent', '{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(profile.UserAgent ?? "Mozilla/5.0")}');

    // screen spoof
    const scr = screen;
    define(scr, 'width', {sw});
    define(scr, 'height', {sh});
    define(scr, 'availWidth', {sw});
    define(scr, 'availHeight', {sh - 40});
    define(scr, 'colorDepth', {cd});

    // timezone spoof
    const _Date = Date;
    const RealDate = _Date;
    function ZonedDate(...args) {{ return new RealDate(...args); }}
    ZonedDate.prototype = RealDate.prototype;
    ZonedDate.UTC = RealDate.UTC;
    ZonedDate.parse = RealDate.parse;
    ZonedDate.now = RealDate.now;
    RealDate.prototype.getTimezoneOffset = function() {{ return {tzOffset}; }};
    window.Date = ZonedDate;

    // Intl spoof
    const _rdto = Intl.DateTimeFormat.prototype.resolvedOptions;
    Intl.DateTimeFormat.prototype.resolvedOptions = function(...args) {{
      const o = _rdto.apply(this, args);
      try {{ o.timeZone = undefined; }} catch {{}}
      return o;
    }};

    // Canvas noise
    const addNoise = (canvas, ctx) => {{
      try {{
        const w = canvas.width, h = canvas.height;
        const imgData = ctx.getImageData(0,0,w,h);
        const d = imgData.data;
        const n = {canvasNoise.ToString(System.Globalization.CultureInfo.InvariantCulture)};
        for (let i=0;i<d.length;i+=4) {{
          d[i]   = Math.min(255, Math.max(0, d[i]   + (Math.random()-0.5)*n*10));
          d[i+1] = Math.min(255, Math.max(0, d[i+1] + (Math.random()-0.5)*n*10));
          d[i+2] = Math.min(255, Math.max(0, d[i+2] + (Math.random()-0.5)*n*10));
        }}
        ctx.putImageData(imgData,0,0);
      }} catch {{}}
    }};
    ['toDataURL','toBlob'].forEach(fn => {{
      try {{
        const orig = HTMLCanvasElement.prototype[fn];
        HTMLCanvasElement.prototype[fn] = function(...args) {{
          const ctx = this.getContext('2d');
          if (ctx) addNoise(this, ctx);
          return orig.apply(this, args);
        }};
      }} catch {{}}
    }});

    // WebGL spoof
    const spoofGL = (proto) => {{
      if (!proto) return;
      try {{
        const origParam = proto.getParameter;
        proto.getParameter = function(param) {{
          if (param === 37445) return '{webglVendor}'; // UNMASKED_VENDOR_WEBGL
          if (param === 37446) return '{webglRenderer}'; // UNMASKED_RENDERER_WEBGL
          return origParam.apply(this, arguments);
        }};
      }} catch {{}}
    }};
    spoofGL(WebGLRenderingContext?.prototype);
    spoofGL(WebGL2RenderingContext?.prototype);

  }} catch {{}}
}})();
";
            try { await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script); } catch { }
        });
    }

    public Uri Source
    {
        get
        {
            try
            {
                if (_webView?.Dispatcher?.CheckAccess() == true)
                    return _webView.Source;
                // Доступ к свойству WebView2.Source должен выполняться на UI-потоке
                return _webView.Dispatcher.Invoke(() => _webView.Source);
            }
            catch
            {
                // В случае недоступности UI-потока/исключения — вернуть безопасный дефолтный URI
                try { return new Uri("https://pwonline.ru"); } catch { }
                // Последний фолбэк — пустой about:blank
                return new Uri("about:blank", UriKind.RelativeOrAbsolute);
            }
        }
    }

    // === Helpers: профили WebView2 ===
    private static string ProfilesRoot()
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "PwHub", "WebViewProfiles");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            var fallback = Path.Combine(Path.GetTempPath(), "PwHub", "WebViewProfiles");
            try { Directory.CreateDirectory(fallback); } catch { }
            return fallback;
        }
    }

    private static string CreateProfileDir()
    {
        var root = ProfilesRoot();
        string path;
        int attempt = 0;
        do
        {
            var id = Guid.NewGuid().ToString("N");
            path = Path.Combine(root, id);
            attempt++;
        } while (Directory.Exists(path) && attempt < 5);

        try { Directory.CreateDirectory(path); } catch { }
        return path;
    }

    private static async Task<bool> TryDeleteDirWithRetries(string path, int attempts = 10, int delayMs = 250)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // Снимаем атрибуты только чтение, удаляем рекурсивно
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                        }
                    }
                    catch { }
                    Directory.Delete(path, true);
                }
                return true;
            }
            catch
            {
                try { await Task.Delay(delayMs); } catch { }
            }
        }
        return false;
    }

    /// <summary>
    /// Публичный вспомогательный метод для удаления папки профиля из внешнего кода (BrowserManager).
    /// </summary>
    public static Task<bool> TryDeleteDirForExternal(string path)
    {
        return TryDeleteDirWithRetries(path);
    }
}