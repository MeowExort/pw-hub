using System.Windows;

namespace Pw.Hub.Services;

/// <summary>
/// Базовая реализация <see cref="IWindowService"/> для работы с окнами WPF.
/// Централизует логику назначения владельца окна (Owner) и открытия модальных/немодальных окон.
/// </summary>
public class WindowService : IWindowService
{
    /// <summary>
    /// Открыть немодальное окно. Если владелец не задан, будет назначен текущий MainWindow.
    /// </summary>
    public void ShowWindow(Window window)
    {
        if (Application.Current?.MainWindow != null && window.Owner == null)
        {
            window.Owner = Application.Current.MainWindow;
        }
        window.Show();
    }

    /// <summary>
    /// Открыть модальное окно (диалог). Если владелец не указан, используется MainWindow.
    /// </summary>
    public bool? ShowDialog(Window window, Window? owner = null)
    {
        if (owner != null)
            window.Owner = owner;
        else if (Application.Current?.MainWindow != null && window.Owner == null)
            window.Owner = Application.Current.MainWindow;

        return window.ShowDialog();
    }
}
