using System.Windows;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

/// <summary>
/// Окно логов выполнения. Вся логика перенесена во ViewModel; окно лишь делегирует и настраивает DataContext.
/// Публичные методы сохранены для совместимости и делегируют во VM.
/// </summary>
public partial class ScriptLogWindow : Window
{
    private readonly bool _closeWhenEnd;
    public ScriptLogViewModel Vm { get; }

    public ScriptLogWindow(string moduleTitle = null, bool closeWhenEnd = false)
    {
        _closeWhenEnd = closeWhenEnd;
        InitializeComponent();
        Vm = new ScriptLogViewModel
        {
            Title = string.IsNullOrWhiteSpace(moduleTitle) ? "Логи выполнения" : $"Модуль — {moduleTitle}"
        };
        Vm.RequestClose += OnRequestClose;
        DataContext = Vm;
    }

    /// <summary>
    /// Назначает внешнее действие остановки выполнения (делегирует во VM).
    /// </summary>
    public void SetStopAction(Action onStop) => Vm.SetStopAction(onStop);

    /// <summary>
    /// Устанавливает состояние выполнения (делегирует во VM).
    /// </summary>
    public void SetRunning(bool running) => Vm.SetRunning(running);

    /// <summary>
    /// Добавляет строку в лог (делегирует во VM). Выполняется из UI-потока вызывающей стороны.
    /// </summary>
    public void AppendLog(string line) => Vm.AppendLog(line);

    /// <summary>
    /// Отчёт о прогрессе (делегирует во VM).
    /// </summary>
    public void ReportProgress(int percent, string message = null)
    {
        Vm.ReportProgress(percent, message);
        TaskbarItemInfo.ProgressValue = percent / 100.0;
    }

    /// <summary>
    /// Помечает выполнение завершённым (делегирует во VM) и закрывает окно автоматически при необходимости.
    /// </summary>
    public void MarkCompleted(string finalMessage = null)
    {
        Vm.MarkCompleted(finalMessage);
        if (_closeWhenEnd)
        {
            try { DialogResult = true; } catch { }
            Close();
        }
    }

    private void OnRequestClose()
    {
        try { DialogResult = true; } catch { }
        Close();
    }
}