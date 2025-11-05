using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

    // Заготовки обработчиков (могут быть расширены при необходимости)
    private void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // Здесь можно добавлять общий JS/инъекции для v2-браузеров при необходимости.
    }

    private void Wv_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        // Установка тёмного фона about:blank
        try { Wv.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); } catch { }
    }
}
