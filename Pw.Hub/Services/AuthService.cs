using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Application = System.Windows.Application;

namespace Pw.Hub.Services;

public class AuthService
{
    private readonly DispatcherTimer _dispatcherTimer;
    private bool _running;

    public AuthService()
    {
        _dispatcherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1),
        };
        _dispatcherTimer.Tick += DispatcherTimerOnTick;

        _dispatcherTimer.Start();
    }

    public async Task CheckAccounts(bool ignoreVisibility = false)
    {
        if (_running)
            return;
        _running = true;
        try
        {
            if (Application.Current.MainWindow is not MainWindow mainWindow)
                return;

            if (Application.Current is not App app)
                return;

            if (!ignoreVisibility && mainWindow.IsVisible)
                return;

            var threshold = DateTime.UtcNow.AddDays(-1);
            await using var db = new AppDbContext();
            var accounts = await db.Accounts
                .Where(x => x.LastVisit <= threshold)
                .AsNoTracking()
                .ToListAsync();
            
            if (accounts.Count == 0)
                return;

            var notAuthorized = new List<Account>();
            var reAuthorized = new List<Account>();

            foreach (var account in accounts)
            {
                var check = await mainWindow.ChangeAccount(account);
                if (check)
                    reAuthorized.Add(account);
                else
                    notAuthorized.Add(account);
            }

            app.NotifyIcon.ShowBalloonTip(5, "Проверка авторизации",
                $"Не авторизованы: {notAuthorized.Count}, Авторизовались: {reAuthorized.Count}",
                notAuthorized.Count > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
        finally
        {
            _running = false;
        }
    }

    private async void DispatcherTimerOnTick(object sender, EventArgs e)
    {
        await CheckAccounts();
    }
}