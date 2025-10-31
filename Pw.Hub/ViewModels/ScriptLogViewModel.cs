using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel окна логов выполнения скрипта/модуля.
/// Инкапсулирует логику подсчёта времени, прогресса и накопления лога.
/// </summary>
public class ScriptLogViewModel : BaseViewModel
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;

    private string _title = "📝 Логи выполнения";
    /// <summary>
    /// Заголовок окна (включая название модуля при наличии).
    /// </summary>
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    private bool _isRunning = true;
    /// <summary>
    /// Флаг выполнения. Влияет на доступность кнопок и индикацию.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanClose)); }
    }

    /// <summary>
    /// Признак возможности закрыть окно (кнопка "Закрыть").
    /// </summary>
    public bool CanClose => !IsRunning;

    private string _statusText = string.Empty;
    /// <summary>
    /// Текст статуса под прогресс-баром.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    private int _percent;
    /// <summary>
    /// Процент выполнения 0..100.
    /// </summary>
    public int Percent
    {
        get => _percent;
        private set { _percent = value; OnPropertyChanged(); OnPropertyChanged(nameof(PercentText)); OnPropertyChanged(nameof(ProgressValue)); }
    }

    /// <summary>
    /// Представление процента для UI.
    /// </summary>
    public string PercentText => Percent + "%";

    /// <summary>
    /// Значение прогресса для ProgressBar (0..100).
    /// </summary>
    public int ProgressValue => Percent;

    private string _elapsedText = "⏱ Текущее время выполнения: 00:00";
    /// <summary>
    /// Текст с текущим/итоговым временем выполнения.
    /// </summary>
    public string ElapsedText
    {
        get => _elapsedText;
        private set { _elapsedText = value; OnPropertyChanged(); }
    }

    private readonly StringBuilder _logBuilder = new();
    private string _logText = string.Empty;
    /// <summary>
    /// Полный текст лога для биндинга к TextBox.
    /// </summary>
    public string LogText
    {
        get => _logText;
        private set { _logText = value; OnPropertyChanged(); RequestScrollToEnd?.Invoke(); }
    }

    /// <summary>
    /// Команда остановки выполнения (зовёт колбэк, который передаёт окно).
    /// </summary>
    public ICommand StopCommand { get; }

    /// <summary>
    /// Команда закрытия окна (используется представлением для проверки CanClose).
    /// </summary>
    public ICommand CloseCommand { get; }

    private Action _onStop;

    /// <summary>
    /// Событие, по которому View может прокрутить лог в конец.
    /// </summary>
    public event Action RequestScrollToEnd;

    /// <summary>
    /// Событие запроса закрытия окна (View назначит DialogResult и закроет окно).
    /// </summary>
    public event Action RequestClose;

    public ScriptLogViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();
        _stopwatch.Start();
        _timer.Start();
        UpdateElapsed();

        StopCommand = new RelayCommand(_ => _onStop?.Invoke(), _ => IsRunning);
        CloseCommand = new RelayCommand(_ => { if (CanClose) RequestClose?.Invoke(); }, _ => CanClose);
    }

    /// <summary>
    /// Назначает внешнее действие остановки (например, остановка раннера Lua).
    /// </summary>
    public void SetStopAction(Action onStop) => _onStop = onStop;

    /// <summary>
    /// Переключает состояние выполнения. Обновляет таймер и доступность команд.
    /// </summary>
    public void SetRunning(bool running)
    {
        IsRunning = running;
        if (!running)
        {
            if (_stopwatch.IsRunning) _stopwatch.Stop();
            _timer.Stop();
            UpdateElapsed(final: true);
        }
        else
        {
            if (!_stopwatch.IsRunning) _stopwatch.Start();
            _timer.Start();
        }
        // Обновить доступность команд
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Добавляет строку в лог. Потокобезопасность обеспечивается вызовом из UI-потока View.
    /// </summary>
    public void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (_logBuilder.Length > 0) _logBuilder.AppendLine();
        _logBuilder.Append(line);
        LogText = _logBuilder.ToString();
    }

    /// <summary>
    /// Обновляет прогресс.
    /// </summary>
    public void ReportProgress(int percent, string message = null)
    {
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;
        Percent = percent;
        StatusText = message ?? string.Empty;
    }

    /// <summary>
    /// Помечает выполнение завершённым. Обновляет прогресс до 100% и добавляет итоговое время в лог.
    /// </summary>
    public void MarkCompleted(string finalMessage = null)
    {
        SetRunning(false);
        ReportProgress(100, "Готово");
        if (_logBuilder.Length > 0) _logBuilder.AppendLine();
        _logBuilder.AppendLine($"Время выполнения: {FormatElapsed(_stopwatch.Elapsed)}");
        if (!string.IsNullOrWhiteSpace(finalMessage))
        {
            _logBuilder.AppendLine();
            _logBuilder.AppendLine("=== Результат ===");
            _logBuilder.AppendLine(finalMessage);
        }
        LogText = _logBuilder.ToString();
    }

    private void UpdateElapsed(bool final = false)
    {
        var prefix = final ? "⏱ Итоговое время выполнения: " : "⏱ Текущее время выполнения: ";
        ElapsedText = prefix + FormatElapsed(_stopwatch.Elapsed);
    }

    private static string FormatElapsed(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        return $"{ts.Minutes:00}:{ts.Seconds:00}";
    }
}
