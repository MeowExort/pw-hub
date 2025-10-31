using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pw.Hub.ViewModels;

/// <summary>
/// Базовый класс для всех ViewModel с поддержкой INotifyPropertyChanged.
/// Вызывайте <see cref="OnPropertyChanged"/> в сеттерах свойств для обновления привязок в UI.
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Событие уведомления об изменении значения свойства.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Вызывает уведомление для привязок WPF о том, что свойство изменилось.
    /// </summary>
    /// <param name="propertyName">Имя свойства. Заполняется автоматически компилятором.</param>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
