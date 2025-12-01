using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Pw.Hub.Models;

namespace Pw.Hub.Windows;

public partial class RegistrationWindow : Window
{
    private readonly Squad _squad;
    private CancellationTokenSource _cts;
    private bool _paused;

    public RegistrationWindow(Squad squad)
    {
        InitializeComponent();
        _squad = squad;
        SquadNameText.Text = squad?.Name ?? "-";

        // Значения по умолчанию
        RefUrlBox.Text = "https://pwonline.ru/";
        EmailTemplateBox.Text = "agit0.o+{rand6}@yandex.ru";
        LimitPerProxyBox.Text = "3";
        PauseOnHumanStepsBox.IsChecked = true;
        Precheck2ipBox.IsChecked = false;

        UpdateButtons(idle: true);
        AppendLog($"[INFO] Мастер инициализирован. Отряд: '{_squad?.Name}'.\n");
    }

    private void UpdateButtons(bool idle)
    {
        BtnStart.IsEnabled = idle;
        BtnPause.IsEnabled = !idle && !_paused;
        BtnResume.IsEnabled = !idle && _paused;
        BtnStop.IsEnabled = !idle;
    }

    private void AppendLog(string text)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => AppendLog(text)); return; }
        LogBox.AppendText(text);
        LogBox.ScrollToEnd();
    }

    private string MakeEmailFromTemplate()
    {
        var tpl = EmailTemplateBox.Text?.Trim() ?? "";
        var rnd = new Random();
        string rand6 = rnd.Next(100000, 999999).ToString();
        return tpl.Replace("{rand6}", rand6);
    }

    private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        _paused = false;
        UpdateButtons(idle: false);

        var referral = RefUrlBox.Text?.Trim();
        var perProxyLimit = 3;
        int.TryParse(LimitPerProxyBox.Text?.Trim(), out perProxyLimit);
        var proxies = (ProxiesBox.Text ?? string.Empty)
            .Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        AppendLog("[RUN] Запуск мастера в сухом режиме (без ввода).\n");
        AppendLog($"[CFG] Реф. ссылка: {referral}\n");
        AppendLog($"[CFG] Прокси: {proxies.Length} шт., лимит на прокси: {perProxyLimit}\n");
        AppendLog($"[CFG] Шаблон email: {EmailTemplateBox.Text}\n");

        try
        {
            // Сухой прогон: просто имитируем шаги и логируем.
            await DryRunAsync(referral, proxies, _cts.Token);
            AppendLog("[DONE] Сухой прогон завершён.\n");
        }
        catch (OperationCanceledException)
        {
            AppendLog("[INFO] Остановлено пользователем.\n");
        }
        catch (Exception ex)
        {
            AppendLog("[ERROR] " + ex.Message + "\n");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _paused = false;
            UpdateButtons(idle: true);
        }
    }

    private async Task DryRunAsync(string referral, string[] proxies, CancellationToken ct)
    {
        // Эмуляция шагов навигации
        await StepAsync("[NAV] Переход по реферальной ссылке…", ct);
        await StepAsync("[NAV] Переход на vkplay → выбор ‘Создать новый аккаунт’…", ct);
        await StepAsync("[NAV] Переход на id.vk.com…", ct);
        await StepAsync("[INPUT] Выбор способа ‘Почта’…", ct);
        var email = MakeEmailFromTemplate();
        AppendLog($"[INPUT] Пример e‑mail для следующего шага: {email}\n");
        await StepAsync("[INPUT] (сухо) Ввод e‑mail и сабмит формы…", ct);
    }

    private async Task StepAsync(string message, CancellationToken ct)
    {
        AppendLog(message + "\n");
        for (int i = 0; i < 5; i++)
        {
            ct.ThrowIfCancellationRequested();
            await WaitIfPausedAsync(ct);
            await Task.Delay(120, ct);
        }
    }

    private async Task WaitIfPausedAsync(CancellationToken ct)
    {
        while (_paused)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(100, ct);
        }
    }

    private void BtnPause_OnClick(object sender, RoutedEventArgs e)
    {
        _paused = true;
        UpdateButtons(idle: false);
        AppendLog("[STATE] Пауза.\n");
    }

    private void BtnResume_OnClick(object sender, RoutedEventArgs e)
    {
        _paused = false;
        UpdateButtons(idle: false);
        AppendLog("[STATE] Продолжение.\n");
    }

    private void BtnStop_OnClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void BtnExport_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"registration-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };
            if (sfd.ShowDialog(this) == true)
            {
                // Пока экспортируем только журнал как текстовый CSV (одна колонка Message)
                var lines = LogBox.Text.Replace("\r", "").Split('\n');
                var sb = new StringBuilder();
                sb.AppendLine("Message");
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var escaped = line.Replace("\"", "\"\"");
                    sb.AppendLine($"\"{escaped}\"");
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                AppendLog($"[EXPORT] CSV сохранён: {sfd.FileName}\n");
            }
        }
        catch (Exception ex)
        {
            AppendLog("[ERROR] Экспорт CSV: " + ex.Message + "\n");
        }
    }
}
