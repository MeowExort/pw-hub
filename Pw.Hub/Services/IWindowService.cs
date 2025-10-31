using System.Windows;

namespace Pw.Hub.Services;

/// <summary>
/// Сервис абстракции для работы с окнами WPF из ViewModel.
/// Позволяет открывать окна и диалоги, не нарушая MVVM (без прямых зависимостей от Window в VM).
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Открыть немодальное окно.
    /// </summary>
    /// <param name="window">Экземпляр окна для показа.</param>
    void ShowWindow(Window window);

    /// <summary>
    /// Открыть модальное окно (диалог).
    /// </summary>
    /// <param name="window">Экземпляр окна для показа.</param>
    /// <param name="owner">Необязательный владелец окна; по умолчанию берётся MainWindow.</param>
    /// <returns>Результат ShowDialog.</returns>
    bool? ShowDialog(Window window, Window? owner = null);
}
