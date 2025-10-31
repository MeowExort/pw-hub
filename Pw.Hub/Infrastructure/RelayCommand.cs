using System.Windows.Input;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Универсальная реализация команды для MVVM.
/// Позволяет пробрасывать действия из ViewModel в XAML без написания отдельных классов команд.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    /// <summary>
    /// Создаёт команду.
    /// </summary>
    /// <param name="execute">Действие, выполняемое при вызове команды.</param>
    /// <param name="canExecute">Опциональная функция проверки доступности команды.</param>
    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Возвращает признак возможности выполнения команды в текущем состоянии.
    /// </summary>
    public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Выполняет действие команды.
    /// </summary>
    public void Execute(object parameter) => _execute(parameter);

    /// <summary>
    /// Событие обновления состояния доступности. Подписано на CommandManager.RequerySuggested.
    /// </summary>
    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
