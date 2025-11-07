using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;

namespace Pw.Hub.Controls;

/// <summary>
/// BrowserView — независимый визуальный контейнер для одного экземпляра WebView2.
/// Реализует IWebViewHost, чтобы браузерный сервис (WebCoreBrowser) мог безопасно
/// пересоздавать контрол для новой InPrivate-сессии без деталей UI.
/// </summary>
public partial class BrowserView : UserControl, IWebViewHost
{
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
        Grid.SetRow(newControl, 0);
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
            if (!e.Uri.StartsWith("https://pwonline.ru") && !e.Uri.StartsWith("http://pwonline.ru") &&
                !e.Uri.StartsWith("https://pw.mail.ru") && !e.Uri.StartsWith("http://pw.mail.ru"))
                e.Cancel = true;
        }
        catch { }
    }

    // Обновление адресной строки/кнопок по завершению навигации
    private void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try { AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty; } catch { }
        UpdateNavUi();
        // Здесь можно добавлять общий JS/инъекции для v2-браузеров при необходимости.
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        // Установка тёмного фона about:blank
        try { Wv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }
        try { Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged; } catch { }
        UpdateNavUi();
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
