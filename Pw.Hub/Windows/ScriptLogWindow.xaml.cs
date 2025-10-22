using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Pw.Hub.Windows;

public partial class ScriptLogWindow : Window
{
    private bool _running = true;
    private Action _onStop;

    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;

    public ScriptLogWindow(string moduleTitle = null)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(moduleTitle) ? "Логи выполнения" : $"Модуль — {moduleTitle}";
        CloseButton.IsEnabled = false;
        StopButton.IsEnabled = true;

        // Инициализация таймера и запуск отсчета времени выполнения
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();
        _stopwatch.Start();
        _timer.Start();
        UpdateElapsed();
    }

    public void SetStopAction(Action onStop)
    {
        _onStop = onStop;
    }

    public void SetRunning(bool running)
    {
        _running = running;
        CloseButton.IsEnabled = !running;
        StopButton.IsEnabled = running;

        if (!running)
        {
            // Останавливаем измерение времени
            if (_stopwatch.IsRunning) _stopwatch.Stop();
            _timer?.Stop();
            UpdateElapsed(final: true);
        }
        else
        {
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }
            _timer?.Start();
        }
    }

    public void AppendLog(string line)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(AppendLog), line);
                return;
            }

            if (string.IsNullOrEmpty(line)) return;
            if (LogTextBox.Text.Length == 0)
                LogTextBox.AppendText(line);
            else
                LogTextBox.AppendText(Environment.NewLine + line);
            LogTextBox.CaretIndex = LogTextBox.Text.Length;
            LogTextBox.ScrollToEnd();
        }
        catch
        {
        }
    }

    public void ReportProgress(int percent, string message = null)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<int, string>(ReportProgress), percent, message);
                return;
            }
            
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            Progress.Value = percent;
            PercentText.Text = percent + "%";
            StatusText.Text = message ?? string.Empty;
            TaskbarItemInfo.ProgressValue = percent / 100.0;
        }
        catch
        {
        }
    }

    public void MarkCompleted(string finalMessage = null)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(MarkCompleted), finalMessage);
                return;
            }

            SetRunning(false);
            ReportProgress(100, "Готово");

            // Зафиксировать итоговое время выполнения в логах
            AppendLog("");
            AppendLog($"Время выполнения: {FormatElapsed(_stopwatch.Elapsed)}");

            if (!string.IsNullOrWhiteSpace(finalMessage))
            {
                AppendLog("");
                AppendLog("=== Результат ===");
                AppendLog(finalMessage!);
            }
        }
        catch
        {
        }
    }

    private void UpdateElapsed(bool final = false)
    {
        try
        {
            var prefix = final ? "⏱ Итоговое время выполнения: " : "⏱ Текущее время выполнения: ";
            var text = prefix + FormatElapsed(_stopwatch.Elapsed);
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<bool>(UpdateElapsed), final);
                return;
            }
            ElapsedText.Text = text;
        }
        catch
        {
        }
    }

    private static string FormatElapsed(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        return $"{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        DialogResult = true;
        Close();
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _onStop?.Invoke();
        }
        catch { }
    }
}