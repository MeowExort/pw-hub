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

namespace Pw.Hub;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public NotifyIcon NotifyIcon;
    private AuthService _authService;
    private UpdateService _updateService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { Directory.SetCurrentDirectory(AppContext.BaseDirectory); } catch { }

        NotifyIcon = new NotifyIcon()
        {
            Text = "PW Hub",
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName!)!
        };
        
        _authService = new AuthService();
        _updateService = new UpdateService();

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

        // Restore default shutdown behavior after main window is shown
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Fire-and-forget update check on startup (no UI if up-to-date or failed)
        _ = _updateService.CheckForUpdates(false);
    }

    private void NotifyIconOnMouseDoubleClick(object sender, MouseEventArgs e)
    {
        Current.MainWindow?.Show();
        Current.MainWindow?.Activate();
    }

    private async void OnClick(object sender, EventArgs e)
    {
        await _authService.CheckAccounts(true);
    }

    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var sbError = new StringBuilder();
        sbError.AppendLine("Exception: " + e.Exception.Message);
        sbError.AppendLine();
        sbError.AppendLine("Stack trace: " + (e.Exception.StackTrace ?? "empty"));
        File.WriteAllText("error.log", sbError.ToString());

        MessageBox.Show(e.Exception.ToString());
    }
}