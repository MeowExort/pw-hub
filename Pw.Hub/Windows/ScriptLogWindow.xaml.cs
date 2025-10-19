using System;
using System.Text;
using System.Windows;
using System.Windows.Shell;

namespace Pw.Hub.Windows;

public partial class ScriptLogWindow : Window
{
    private bool _running = true;
    private Action? _onStop;

    public ScriptLogWindow(string? moduleTitle = null)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(moduleTitle) ? "Логи выполнения" : $"Модуль — {moduleTitle}";
        CloseButton.IsEnabled = false;
        StopButton.IsEnabled = true;
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

    public void ReportProgress(int percent, string? message = null)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<int, string?>(ReportProgress), percent, message);
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

    public void MarkCompleted(string? finalMessage = null)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string?>(MarkCompleted), finalMessage);
                return;
            }

            SetRunning(false);
            ReportProgress(100, "Готово");
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