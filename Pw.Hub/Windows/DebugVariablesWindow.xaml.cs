using System.Windows;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

/// <summary>
/// Окно просмотра переменных на точке останова. Бизнес-логика вынесена в DebugVariablesViewModel.
/// Code-behind отвечает только за инициализацию VM, привязку DataContext и делегирование методов.
/// </summary>
public partial class DebugVariablesWindow : Window
{
    /// <summary>
    /// ViewModel окна.
    /// </summary>
    public DebugVariablesViewModel Vm { get; }

    public DebugVariablesWindow()
    {
        InitializeComponent();
        Vm = new DebugVariablesViewModel();
        Vm.RequestClose += OnRequestClose;
        DataContext = Vm;
    }

    /// <summary>
    /// Делегирование заполнения данных во ViewModel (для совместимости с существующим вызовом в LuaEditorWindow).
    /// </summary>
    public void SetData(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
        => Vm.SetData(line, locals, globals);

    private void OnRequestClose()
    {
        try { DialogResult = true; } catch { }
        Close();
    }
}