using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pw.Hub.Services;

namespace Pw.Hub.Controls;

/// <summary>
/// BrowserView — независимый визуальный контейнер для одного экземпляра WebView2.
/// Реализует IWebViewHost, чтобы браузерный сервис (WebCoreBrowser) мог безопасно
/// пересоздавать контрол для новой InPrivate-сессии без деталей UI.
/// </summary>
public partial class BrowserView : UserControl, IWebViewHost, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
    {
        try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); } catch { }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (value != _isLoading) { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }
    }

    public BrowserView()
    {
        InitializeComponent();
        // Тёмный фон по умолчанию и скрытие белых вспышек в момент инициализации
        try { Wv.Visibility = Visibility.Visible; } catch { }
        // Инициализация UI состояния
        UpdateNavUi();
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }
    }

    // IWebViewHost
    public WebView2 Current => Wv;

    /// <summary>
    /// Полная замена контрола (используется в legacy-сценариях). В новой архитектуре
    /// предпочтительно использовать пару PreloadAsync + FinalizeSwapAsync для устранения "белых вспышек".
    /// </summary>
    public async Task ReplaceAsync(WebView2 newControl)
    {
        if (newControl == null) return;

        // Отвязываем обработчики у старого контрола
        try { Wv.NavigationStarting -= OnNavigationStartingFilter; } catch { }
        try { Wv.NavigationCompleted -= Wv_OnNavigationCompleted; } catch { }
        try { Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted; } catch { }
        try { Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged; } catch { }

        var parent = Content as Grid;
        if (parent == null)
        {
            // Фолбэк: заменить содержимое UserControl
            Content = newControl;
            Wv = newControl;
            AttachHandlers();
            try { await Wv.EnsureCoreWebView2Async(); } catch { }
            return;
        }

        // Удаляем старый контрол
        parent.Children.Clear();
        parent.Children.Add(newControl);
        try { Grid.SetRow(newControl, 1); } catch { }
        try { Grid.SetColumn(newControl, 0); } catch { }

        Wv = newControl;
        AttachHandlers();
        try { await Wv.EnsureCoreWebView2Async(); } catch { }
    }

    /// <summary>
    /// Предзагрузка нового WebView2 в дерево в скрытом состоянии (старый остаётся видимым).
    /// </summary>
    public async Task PreloadAsync(WebView2 newControl)
    {
        if (newControl == null) return;
        var parent = Content as Grid;
        if (parent == null) return;

        try { newControl.Visibility = Visibility.Hidden; } catch { }
        try { newControl.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }

        // Добавляем до старого, чтобы порядок наложения сохранился
        parent.Children.Add(newControl);
        // WebView2 должен находиться во второй строке (Grid.Row=1), первая строка — это панель навигации высотой 32px
        Grid.SetRow(newControl, 1);
        Grid.SetColumn(newControl, 0);

        // Раннее подключение базовых обработчиков (фильтр домена и т.д.)
        try { newControl.NavigationStarting += OnNavigationStartingFilter; } catch { }
        try { newControl.NavigationCompleted += Wv_OnNavigationCompleted; } catch { }
        try { newControl.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted; } catch { }

        try { await newControl.EnsureCoreWebView2Async(); } catch { }
        try { newControl.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }
    }

    /// <summary>
    /// Финализация замены: удаляем старый контрол, показываем новый и переносим ссылки обработчиков.
    /// </summary>
    public async Task FinalizeSwapAsync(WebView2 newControl)
    {
        if (newControl == null) return;
        var parent = Content as Grid;
        if (parent == null) return;

        // Удаляем старый (первый) элемент, если он есть и это не newControl
        if (parent.Children.Count > 0)
        {
            var toRemove = Wv;
            if (!ReferenceEquals(toRemove, newControl))
            {
                try { toRemove.NavigationStarting -= OnNavigationStartingFilter; } catch { }
                try { toRemove.NavigationCompleted -= Wv_OnNavigationCompleted; } catch { }
                try { toRemove.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted; } catch { }
                try { toRemove.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged; } catch { }
                try { toRemove.Dispose(); } catch { }
                try { parent.Children.Remove(toRemove); } catch { }
            }
        }

        Wv = newControl;
        AttachHandlers();
        try { Wv.Visibility = Visibility.Visible; } catch { }
        try { await Wv.EnsureCoreWebView2Async(); } catch { }
    }

    private void AttachHandlers()
    {
        try { Wv.NavigationStarting += OnNavigationStartingFilter; } catch { }
        try { Wv.NavigationCompleted += Wv_OnNavigationCompleted; } catch { }
        try { Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted; } catch { }
        try { Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged; } catch { }
    }

    private void CoreWebView2OnHistoryChanged(object? sender, object e)
    {
        UpdateNavUi();
    }

    private void UpdateNavUi()
    {
        try
        {
            var core = Wv?.CoreWebView2;
            if (core == null)
            {
                BtnBack.IsEnabled = false;
                BtnForward.IsEnabled = false;
                BtnRefresh.IsEnabled = false;
            }
            else
            {
                BtnBack.IsEnabled = core.CanGoBack;
                BtnForward.IsEnabled = core.CanGoForward;
                BtnRefresh.IsEnabled = true;
            }
        }
        catch { }

        try { AddressBox.Text = Wv?.Source?.ToString() ?? AddressBox.Text; } catch { }
    }

    // Фильтр доменов — как в AccountPage: запрещаем уходить на посторонние домены
    private void OnNavigationStartingFilter(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            var allowed = e.Uri.StartsWith("https://pwonline.ru") || e.Uri.StartsWith("http://pwonline.ru") ||
                          e.Uri.StartsWith("https://pw.mail.ru") || e.Uri.StartsWith("http://pw.mail.ru");
            if (!allowed)
            {
                e.Cancel = true;
                IsLoading = false;
                return;
            }
            // Allowed navigation — show loading indicator
            IsLoading = true;
        }
        catch { }
    }

    // Обновление адресной строки/кнопок по завершению навигации
    private void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try { IsLoading = false; } catch { }
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }
        UpdateNavUi();
        // Здесь можно добавлять общий JS/инъекции для v2-браузеров при необходимости.
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        // Установка тёмного фона about:blank
        try { Wv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }
        try { Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged; } catch { }
        try
        {
            var core = Wv.CoreWebView2;
            // JS-хелпер appConfig.get/set с фолбэком на localStorage и таймаутом
            var helper = @"(function(){
  try{
    if (!window.appConfig){
      var pending = new Map();
      var nextId = 1;
      function tryLocalGet(key, def){ try{ var raw = localStorage.getItem(key); return raw!=null ? JSON.parse(raw) : def; }catch(_){ return def; } }
      function tryLocalSet(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch(_){ } }
      window.appConfig = {
        get: function(key, def){ return new Promise(function(resolve){ var id = nextId++; pending.set(id, resolve); try{ window.chrome && window.chrome.webview && window.chrome.webview.postMessage({ type: 'appConfig:get', id: id, key: key }); } catch(_){ resolve(tryLocalGet(key, def)); return; } setTimeout(function(){ if (pending.has(id)){ pending.delete(id); resolve(tryLocalGet(key, def)); } }, 800); }); },
        set: function(key, val){ return new Promise(function(resolve){ var id = nextId++; pending.set(id, resolve); try{ window.chrome && window.chrome.webview && window.chrome.webview.postMessage({ type: 'appConfig:set', id: id, key: key, value: val }); } catch(_){ tryLocalSet(key, val); resolve(true); return; } setTimeout(function(){ if (pending.has(id)){ pending.delete(id); tryLocalSet(key, val); resolve(true); } }, 800); }); }
      };
      if (window.chrome && window.chrome.webview){
        window.chrome.webview.addEventListener('message', function(ev){ try{ var msg = ev && ev.data; if (!msg || msg.type !== 'appConfig:resp') return; var res = pending.get(msg.id); if (!res) return; pending.delete(msg.id); if ('value' in msg) res(msg.value); else res(true); } catch(__){} });
      }
    }
  }catch(__){}
})();";
            try { core.AddScriptToExecuteOnDocumentCreatedAsync(helper); } catch { }

            // Обработчик сообщений из JS: appConfig:get/set
            try { core.WebMessageReceived += CoreOnWebMessageReceived; } catch { }
        }
        catch { }
        UpdateNavUi();
    }

    private void CoreOnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var appConfig = App.Services.GetService<IAppConfigService>();
            if (appConfig == null) return;

            var json = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json)) return;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();
            if (type == "appConfig:get")
            {
                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var key = root.TryGetProperty("key", out var kEl) ? kEl.GetString() : null;
                string? value = null;
                if (!string.IsNullOrEmpty(key))
                {
                    appConfig.TryGetString(key!, out value);
                }
                // Сформируем JSON вручную, чтобы вернуть value как raw JSON (а не строку)
                var valuePart = value ?? "null";
                var resp = $"{{\"type\":\"appConfig:resp\",\"id\":{id},\"value\":{valuePart}}}";
                Wv.CoreWebView2.PostWebMessageAsJson(resp);
            }
            else if (type == "appConfig:set")
            {
                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var key = root.TryGetProperty("key", out var kEl) ? kEl.GetString() : null;
                if (!string.IsNullOrEmpty(key))
                {
                    if (root.TryGetProperty("value", out var vEl))
                    {
                        // Сохраняем как строку JSON
                        var raw = vEl.GetRawText();
                        appConfig.SetString(key!, raw);
                    }
                    else
                    {
                        appConfig.SetString(key!, "null");
                    }
                }
                var resp = JsonSerializer.Serialize(new { type = "appConfig:resp", id = id, ok = true });
                Wv.CoreWebView2.PostWebMessageAsJson(resp);
                _ = appConfig.SaveAsync();
            }
        }
        catch { }
    }

    // === Обработчики UI навигации ===
    private async void BtnBack_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Wv?.CoreWebView2?.CanGoBack == true) Wv.CoreWebView2.GoBack();
        }
        catch { }
        UpdateNavUi();
    }

    private async void BtnForward_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Wv?.CoreWebView2?.CanGoForward == true) Wv.CoreWebView2.GoForward();
        }
        catch { }
        UpdateNavUi();
    }

    private async void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        try { if (Wv?.CoreWebView2 != null) Wv.CoreWebView2.Reload(); } catch { }
        UpdateNavUi();
    }

    private void AddressBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var raw = AddressBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        string url = BuildAllowedUrl(raw);
        if (url == null) return; // не позволяем перейти на чужой домен

        try
        {
            if (Wv?.CoreWebView2 != null)
                Wv.CoreWebView2.Navigate(url);
            else
                Wv.Source = new System.Uri(url);
        }
        catch { }
    }

    private static string BuildAllowedUrl(string input)
    {
        // Нормализация: если нет схемы — добавим https://
        try
        {
            string s = input;
            if (s.StartsWith("/"))
            {
                s = "https://pwonline.ru" + s;
            }
            else if (!s.StartsWith("http://") && !s.StartsWith("https://"))
            {
                // Возможно, пользователь ввёл домен без схемы
                s = "https://" + s;
            }

            // Если указали голый домен pwonline.ru — добавим слэш
            if (s.Equals("https://pwonline.ru", System.StringComparison.OrdinalIgnoreCase))
                s += "/";

            // Разрешаем только pwonline.ru и pw.mail.ru
            var ok = s.StartsWith("https://pwonline.ru") || s.StartsWith("http://pwonline.ru") ||
                     s.StartsWith("https://pw.mail.ru") || s.StartsWith("http://pw.mail.ru");
            return ok ? s : null;
        }
        catch { return null; }
    }
}
