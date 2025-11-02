using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса диалогов поверх стандартного MessageBox.
/// Вынесение в сервис позволяет ViewModel оставаться независимой от UI API.
/// </summary>
public class UiDialogService : IUiDialogService
{
    public void Alert(string message, string caption = "PW Hub")
    {
        try { MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
    }

    public bool Confirm(string message, string caption = "Подтверждение")
    {
        try
        {
            return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
        catch
        {
            return false;
        }
    }

    public string? Prompt(string prompt, string caption = "Ввод", string? defaultValue = null)
    {
        // Простой вариант: используем стандартный InputBox недоступен в WPF.
        // На будущее можно реализовать собственное окно ввода. Пока возвращаем null.
        return null;
    }

    public Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs)
    {
        try
        {
            if (inputs == null || inputs.Count == 0) return new Dictionary<string, object>();
            var dlg = new ModuleArgsWindow(inputs) { Owner = Application.Current?.MainWindow };
            var ok = dlg.ShowDialog();
            if (ok == true)
            {
                return dlg.Values ?? new Dictionary<string, object>();
            }
            return null; // отмена
        }
        catch
        {
            return null;
        }
    }

    public Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs, ref ModuleArgsWindow window, Window owner)
    {
        try
        {
            if (inputs == null || inputs.Count == 0) return new Dictionary<string, object>();
            
            // ModuleArgsWindow не поддерживает переиспользование с обновлением данных,
            // поэтому всегда создаём новое окно
            window = new ModuleArgsWindow(inputs) { Owner = owner ?? Application.Current?.MainWindow };

            var ok = window.ShowDialog();
            if (ok == true)
            {
                return window.Values ?? new Dictionary<string, object>();
            }
            return null; // отмена
        }
        catch
        {
            return null;
        }
    }
}
