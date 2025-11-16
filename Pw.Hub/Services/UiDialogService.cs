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
                // Базовые значения с формы
                var result = dlg.Values != null
                    ? new Dictionary<string, object>(dlg.Values, StringComparer.Ordinal)
                    : new Dictionary<string, object>(StringComparer.Ordinal);

                // Дополнительно гарантируем прокидывание enum как строки
                try
                {
                    foreach (var def in inputs)
                    {
                        var type = (def?.Type ?? "string").ToLowerInvariant();
                        if (type == "enum" || type == "перечисление")
                        {
                            var name = def?.Name ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            var has = result.TryGetValue(name, out var v);
                            var isEmpty = !has || v == null || (v is string sv && string.IsNullOrWhiteSpace(sv));
                            if (isEmpty)
                            {
                                if (dlg.StringValues != null && dlg.StringValues.TryGetValue(name, out var strVal))
                                {
                                    result[name] = strVal ?? string.Empty;
                                }
                            }
                        }
                    }
                }
                catch { }

                return result;
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
                // Базовые значения с формы
                var result = window.Values != null
                    ? new Dictionary<string, object>(window.Values, StringComparer.Ordinal)
                    : new Dictionary<string, object>(StringComparer.Ordinal);

                // Дополнительно гарантируем прокидывание enum как строки
                try
                {
                    foreach (var def in inputs)
                    {
                        var type = (def?.Type ?? "string").ToLowerInvariant();
                        if (type == "enum" || type == "перечисление")
                        {
                            var name = def?.Name ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            var has = result.TryGetValue(name, out var v);
                            var isEmpty = !has || v == null || (v is string sv && string.IsNullOrWhiteSpace(sv));
                            if (isEmpty)
                            {
                                if (window.StringValues != null && window.StringValues.TryGetValue(name, out var strVal))
                                {
                                    result[name] = strVal ?? string.Empty;
                                }
                            }
                        }
                    }
                }
                catch { }

                return result;
            }
            return null; // отмена
        }
        catch
        {
            return null;
        }
    }
}
