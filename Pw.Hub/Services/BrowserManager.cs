using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using Pw.Hub.Controls;
using Pw.Hub;

namespace Pw.Hub.Services;

/// <summary>
/// BrowserManager — менеджер динамически создаваемых браузеров (Lua API v2).
/// Отвечает за создание/удаление браузеров, хранит для каждого:
///  - визуальный контрол <see cref="BrowserView"/>,
///  - реализацию браузера <see cref="IBrowser"/> (WebCoreBrowser),
///  - менеджер аккаунтов <see cref="IAccountManager"/> (изолированный на данный браузер).
/// Размещение визуальных контролов выполняется в <see cref="MainWindow"/> в контейнере UniformGrid (BrowserWorkspace).
/// </summary>
public class BrowserManager
{
    private readonly MainWindow _mainWindow;

    private int _nextId = 1;

    private class Entry
    {
        public int Id { get; init; }
        public BrowserView View { get; init; }
        public IBrowser Browser { get; init; }
        public IAccountManager AccountManager { get; init; }
    }

    private readonly Dictionary<int, Entry> _entries = new();

    public BrowserManager(MainWindow mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    /// <summary>
    /// Опции создания браузера v2 (минимальные). Пока оставляем на будущее, чтобы не ломать API.
    /// </summary>
    public class CreateOptions
    {
        /// <summary>
        /// Начальный URL, на который перейти сразу после создания (опционально).
        /// </summary>
        public string StartUrl { get; set; } = null;
    }

    /// <summary>
    /// Создаёт новый браузер: визуальный контрол добавляется в рабочее пространство главного окна,
    /// создаются IBrowser и IAccountManager, настраивается InPrivate-сессия перед сменой аккаунта.
    /// Возвращает дескриптор (целочисленный handle), по которому Lua v2 сможет управлять браузером.
    /// </summary>
    public async Task<int> CreateAsync(CreateOptions options = null)
    {
        // 1) Создаём визуальный контейнер (BrowserView) на UI-потоке
        BrowserView view = null;
        await _mainWindow.Dispatcher.InvokeAsync(() =>
        {
            view = new BrowserView();
            _mainWindow.AddBrowserView(view);
        });

        // 2) Создаём движок браузера и менеджер аккаунтов, привязанный к нему
        var browser = new WebCoreBrowser(view);
        var am = new AccountManager(browser);
        // ВНИМАНИЕ: В Lua API v2 смена аккаунта НЕ создаёт новую сессию.
        // Новая сессия на смену аккаунта разрешена только для legacy Lua API v1.

        // Первый профиль для этого браузера создаём сразу, чтобы не шарить состояние с host.Current
        try { await browser.CreateNewSessionAsync(); } catch { }

        // 3) Регистрируем запись
        var id = _nextId++;
        _entries[id] = new Entry
        {
            Id = id,
            View = view,
            Browser = browser,
            AccountManager = am
        };

        // 4) Необязательная стартовая навигация
        if (!string.IsNullOrWhiteSpace(options?.StartUrl))
        {
            try { await browser.NavigateAsync(options.StartUrl); } catch { }
        }

        return id;
    }

    /// <summary>
    /// Закрывает/удаляет браузер по дескриптору. Возвращает true, если удаление прошло успешно.
    /// </summary>
    public bool Close(int handle)
    {
        if (!_entries.TryGetValue(handle, out var e)) return false;

        string profileDir = null;
        try
        {
            // Попробуем получить папку профиля из реализации браузера
            if (e.Browser is WebCoreBrowser wc)
                profileDir = wc.UserDataFolder;
        }
        catch { }

        try
        {
            _mainWindow.RemoveBrowserView(e.View);
        }
        catch { }

        // Явно освобождаем WebView2
        try { e.View?.Current?.Dispose(); } catch { }

        _entries.Remove(handle);

        // Удаляем папку профиля асинхронно, чтобы не блокировать UI
        try
        {
            if (!string.IsNullOrWhiteSpace(profileDir))
            {
                _ = Task.Run(async () =>
                {
                    try { await WebCoreBrowser.TryDeleteDirForExternal(profileDir); } catch { }
                });
            }
        }
        catch { }

        return true;
    }

    /// <summary>
    /// Возвращает список активных дескрипторов.
    /// </summary>
    public int[] List() => _entries.Keys.OrderBy(x => x).ToArray();

    /// <summary>
    /// Получить браузер и менеджер аккаунтов по handle. Возвращает false, если не найден.
    /// </summary>
    public bool TryGet(int handle, out IBrowser browser, out IAccountManager accountManager)
    {
        if (_entries.TryGetValue(handle, out var e))
        {
            browser = e.Browser;
            accountManager = e.AccountManager;
            return true;
        }
        browser = null;
        accountManager = null;
        return false;
    }
}
