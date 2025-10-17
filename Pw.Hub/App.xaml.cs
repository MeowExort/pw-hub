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

    protected override void OnStartup(StartupEventArgs e)
    {
        NotifyIcon = new NotifyIcon()
        {
            Text = "PW Hub",
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName!)!
        };
        
        _authService = new AuthService();

        NotifyIcon.AddMenu("Проверить авторизацию", OnClick);

        NotifyIcon.AddMenu("-");
        NotifyIcon.AddMenu("Закрыть", (_, _) => Current.Shutdown());

        NotifyIcon.MouseDoubleClick += NotifyIconOnMouseDoubleClick;

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