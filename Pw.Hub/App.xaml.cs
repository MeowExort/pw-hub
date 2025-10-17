using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Pw.Hub.Services;
using NotifyIcon = NotifyIconEx.NotifyIcon;

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

        // Fire-and-forget update check on startup (no UI if up-to-date or failed)
        _ = _updateService.CheckForUpdates(false);

        base.OnStartup(e);
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
}