using System.Windows;
using System.Windows.Input;

namespace Pw.Hub;

public static class WindowBehaviors
{
    // Commands for window operations
    public static readonly ICommand MinimizeCommand = new RelayCommand(obj =>
    {
        if (obj is Window window)
            window.WindowState = WindowState.Minimized;
    });

    public static readonly ICommand MaximizeRestoreCommand = new RelayCommand(obj =>
    {
        if (obj is Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    });

    public static readonly ICommand CloseCommand = new RelayCommand(obj =>
    {
        if (obj is Window window)
            window.Close();
    });

    // Simple RelayCommand implementation
    private class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public RelayCommand(Action<object?> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged;
    }
}
