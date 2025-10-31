using System;
using System.Collections.Generic;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel диалога параметров модуля.
/// Отвечает за хранение собранных значений, команды управления окном и минимальную бизнес-логику.
/// Визуальное построение формы (динамические поля) пока остаётся во View (переходный этап рефакторинга).
/// </summary>
public class ModuleArgsViewModel : BaseViewModel
{
    private string _title = "Параметры модуля";
    private ModuleDefinition _module;

    /// <summary>
    /// Заголовок окна. По умолчанию — "Параметры модуля". При наличии имени модуля подставляется имя.
    /// </summary>
    public string Title
    {
        get => _title;
        set { _title = value ?? string.Empty; OnPropertyChanged(); }
    }

    /// <summary>
    /// Определение модуля, для которого собираются параметры. Используется на будущее для полной MVVM‑формы.
    /// </summary>
    public ModuleDefinition Module
    {
        get => _module;
        set { _module = value; Title = string.IsNullOrWhiteSpace(value?.Name) ? "Параметры модуля" : value.Name; }
    }

    /// <summary>
    /// Словарь типизированных значений, собранных с формы (для непосредственного исполнения).
    /// </summary>
    public Dictionary<string, object> Values { get; } = new();

    /// <summary>
    /// Словарь строковых представлений значений (для сохранения в LastArgs).
    /// </summary>
    public Dictionary<string, string> StringValues { get; } = new();

    /// <summary>
    /// Команда отмены (закрыть окно без сохранения).
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Событие запроса закрытия окна. View подписывается и закрывает себя (устанавливает DialogResult).
    /// </summary>
    public event Action<bool?> RequestClose;

    public ModuleArgsViewModel()
    {
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
    }

    /// <summary>
    /// Вызывается представлением после сбора значений с UI-контролов.
    /// Значения копируются в VM и инициируется закрытие окна с положительным результатом.
    /// </summary>
    /// <param name="values">Типизированные значения.</param>
    /// <param name="stringValues">Строковые значения для последующего сохранения.</param>
    public void ConfirmWithValues(Dictionary<string, object> values, Dictionary<string, string> stringValues)
    {
        try
        {
            Values.Clear();
            StringValues.Clear();
            if (values != null)
                foreach (var kv in values)
                    Values[kv.Key] = kv.Value;
            if (stringValues != null)
                foreach (var kv in stringValues)
                    StringValues[kv.Key] = kv.Value;
        }
        catch { /* сохраняем максимально возможное */ }
        finally
        {
            RequestClose?.Invoke(true);
        }
    }
}
