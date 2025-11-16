using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Pw.Hub.Services;
using NotifyIcon = NotifyIconEx.NotifyIcon;
using System.IO;
using System.Text;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;
using Pw.Hub.Infrastructure.Logging;

namespace Pw.Hub;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public static IServiceProvider Services { get; private set; } = default!;

    public NotifyIcon NotifyIcon;
    private AuthService _authService;
    private UpdateService _updateService;
    private static Mutex _singleInstanceMutex;

    /// <summary>
    /// Точка входа WPF-приложения. Настраивает DI, обеспечивает единственный экземпляр,
    /// инициализирует системный трей и последовательность запуска (логин -> главное окно).
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance guard
        try
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "Global\\Pw.Hub_SingleInstance", out createdNew);
            if (!createdNew)
            {
                try { ActivateExistingInstance(); } catch { }
                // Exit this secondary instance
                Current.Shutdown();
                return;
            }
        }
        catch { }

        base.OnStartup(e);
        try { Directory.SetCurrentDirectory(AppContext.BaseDirectory); } catch { }

        // Parse command line debug flag and initialize logging
        try
        {
            var args = e?.Args ?? Array.Empty<string>(); 
            var debug = args.Any(a => string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-d", StringComparison.OrdinalIgnoreCase));
            RuntimeOptions.DebugMode = debug;
            Log.Initialize(new LoggerConfig
            {
                LogsDirectory = Path.Combine(AppContext.BaseDirectory, "logs"),
                MinimumLevel = debug ? LogLevel.Debug : LogLevel.Information,
                MaxQueueSize = 10000
            });
            Log.For<App>().Info($"App starting. DebugMode={debug}");

            // Subscribe to global exception sources
            DispatcherUnhandledException += App_OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => { try { Log.For<App>().Critical("Unhandled domain exception", ev.ExceptionObject as Exception); } catch { } };
            TaskScheduler.UnobservedTaskException += (s, ev) => { try { Log.For<App>().Error("Unobserved task exception", ev.Exception); } catch { } };
        }
        catch { }

        // Configure DI container
        try
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IWindowService, WindowService>();
            sc.AddSingleton<ViewModels.MainViewModel>();
            // domain services (optional for now)
            sc.AddSingleton<UpdateService>();
            sc.AddSingleton<AuthService>();
            sc.AddSingleton<IUpdatesCheckService, UpdatesCheckService>();
            sc.AddSingleton<IModulesSyncService, ModulesSyncService>();
            sc.AddSingleton<ILuaExecutionService, LuaExecutionService>();
            sc.AddSingleton<ILuaDebugService, LuaDebugService>();
            sc.AddSingleton<ICharactersLoadService, CharactersLoadService>();
            sc.AddSingleton<IRunModuleCoordinator, RunModuleCoordinator>();
            sc.AddSingleton<IUiDialogService, UiDialogService>();
            sc.AddSingleton<IOrderingService, OrderingService>();
            sc.AddSingleton<IAccountsService, AccountsService>();
            // App config (persistent JSON storage in %AppData%)
            sc.AddSingleton<IAppConfigService, JsonAppConfigService>();
            // AI config and diff preview services
            sc.AddSingleton<IAiConfigService, AiConfigService>();
            sc.AddSingleton<IDiffPreviewService, DiffPreviewService>();
            sc.AddSingleton<IAiAssistantService, AiAssistantService>();
            sc.AddSingleton<IAiDocService, AiDocService>();
            Services = sc.BuildServiceProvider();
        }
        catch { }

        NotifyIcon = new NotifyIcon()
        {
            Text = "PW Hub",
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName!)!
        };
        
        _authService = Services.GetRequiredService<AuthService>();
        _updateService = Services.GetRequiredService<UpdateService>();

        NotifyIcon.AddMenu("Проверить авторизацию", OnClick);
        NotifyIcon.AddMenu("Проверить обновления…", async (_, _) => await _updateService.CheckForUpdates(true));

        NotifyIcon.AddMenu("-");
        NotifyIcon.AddMenu("Закрыть", (_, _) => Current.Shutdown());

        NotifyIcon.MouseDoubleClick += NotifyIconOnMouseDoubleClick;

        // Prevent auto-shutdown when the login dialog (the only window) closes
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Show login/register window before main window
        var loginWindow = new Windows.LoginRegisterWindow();
        var result = loginWindow.ShowDialog();
        if (result != true)
        {
            // User cancelled or failed to login
            Current.Shutdown();
            return;
        }

        // Start main window
        var main = new MainWindow();
        Current.MainWindow = main;
        main.Show();
        try { Pw.Hub.Windows.LogsWindow.ShowOnDebug(main); } catch { }

        // Restore default shutdown behavior after main window is shown
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Fire-and-forget update check on startup (no UI if up-to-date or failed)
        _ = _updateService.CheckForUpdates(false);
    }

    /// <summary>
    /// Обработчик двойного клика по иконке в трее — разворачивает главное окно и активирует его.
    /// </summary>
    private void NotifyIconOnMouseDoubleClick(object sender, MouseEventArgs e)
    {
        Current.MainWindow?.Show();
        Current.MainWindow?.Activate();
    }

    /// <summary>
    /// Пункт контекстного меню трея: проверка авторизации аккаунтов с выводом UI.
    /// </summary>
    private async void OnClick(object sender, EventArgs e)
    {
        await _authService.CheckAccounts(true);
    }

    /// <summary>
    /// Глобальный обработчик необработанных исключений на UI-потоке.
    /// Логирует стек в файл error.log и показывает сообщение пользователю.
    /// </summary>
    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Log.For<App>().Error("Unhandled UI exception", e.Exception);
        }
        catch { }
        try
        {
            var sbError = new StringBuilder();
            sbError.AppendLine("Exception: " + e.Exception.Message);
            sbError.AppendLine();
            sbError.AppendLine("Stack trace: " + (e.Exception.StackTrace ?? "empty"));
            File.WriteAllText("error.log", sbError.ToString());
        }
        catch { }

        MessageBox.Show(e.Exception.ToString());
    }

    /// <summary>
    /// Корректное завершение приложения: освобождает ресурсы трея и мьютекс единственного экземпляра.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try { NotifyIcon?.Dispose(); } catch { }
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }

    // Bring an already running instance to foreground
    /// <summary>
    /// Активирует уже запущенный экземпляр приложения (если найден) — разворачивает и переводит в фокус.
    /// Используется для реализации single-instance поведения.
    /// </summary>
    private static void ActivateExistingInstance()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName)
                .Where(p => p.Id != current.Id)
                .OrderBy(p => p.StartTime)
                .ToList();
            if (others.Count == 0) return;
            var target = others[0];
            IntPtr hwnd = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)target.Id && IsWindowVisible(h))
                {
                    hwnd = h;
                    return false; // stop
                }
                return true; // continue
            }, IntPtr.Zero);

            if (hwnd != IntPtr.Zero)
            {
                // Restore if minimized, then bring to front
                if (IsIconic(hwnd)) ShowWindowAsync(hwnd, 9 /*SW_RESTORE*/);
                SetForegroundWindow(hwnd);
            }
        }
        catch { }
    }

    // Win32 interop
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}