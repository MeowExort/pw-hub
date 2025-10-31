using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel окна просмотра переменных при остановке отладки Lua.
/// Содержит коллекции локальных/глобальных переменных и команды управления окном.
/// </summary>
public class DebugVariablesViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Заголовок окна (например: "Остановлено на строке 42").
    /// </summary>
    private string _title = "Переменные";
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Коллекция локальных переменных текущего фрейма.
    /// </summary>
    public ObservableCollection<DebugVarNode> Locals { get; } = new();

    /// <summary>
    /// Коллекция (срез) глобальных переменных.
    /// </summary>
    public ObservableCollection<DebugVarNode> Globals { get; } = new();

    /// <summary>
    /// Команда закрытия окна.
    /// </summary>
    public ICommand CloseCommand { get; }

    /// <summary>
    /// Событие запроса закрытия окна. View подпишется и закроет себя.
    /// </summary>
    public event Action RequestClose;

    public DebugVariablesViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
    }

    /// <summary>
    /// Обновляет данные VM из словарей Lua runner'а.
    /// </summary>
    /// <param name="line">Номер строки, на которой произошла остановка.</param>
    /// <param name="locals">Словарь локальных переменных.</param>
    /// <param name="globals">Словарь глобальных переменных.</param>
    public void SetData(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
    {
        Title = $"Остановлено на строке {line}";
        Locals.Clear();
        Globals.Clear();
        foreach (var node in BuildNodes(locals)) Locals.Add(node);
        foreach (var node in BuildNodes(globals)) Globals.Add(node);
    }

    /// <summary>
    /// Преобразует словарь значений в иерархию узлов для TreeView.
    /// </summary>
    private static IEnumerable<DebugVarNode> BuildNodes(IDictionary<string, object> dict, int depth = 0)
    {
        if (dict == null) yield break;
        foreach (var kv in dict.OrderBy(k => k.Key))
            yield return CreateNode(kv.Key, kv.Value, depth);
    }

    /// <summary>
    /// Создаёт узел отображения для одного значения, рекурсивно разворачивая таблицы.
    /// </summary>
    private static DebugVarNode CreateNode(string name, object value, int depth)
    {
        var node = new DebugVarNode { Name = name };
        if (value is IDictionary<string, object> table)
        {
            node.TypeName = "table";
            node.DisplayValue = $"table[{table.Count}]";
            foreach (var child in BuildNodes(table, depth + 1))
                node.Children.Add(child);
        }
        else if (value is string s)
        {
            node.TypeName = "string";
            node.DisplayValue = $"\"{s}\"";
        }
        else if (value is bool b)
        {
            node.TypeName = "boolean";
            node.DisplayValue = b ? "true" : "false";
        }
        else if (value == null)
        {
            node.TypeName = "nil";
            node.DisplayValue = "nil";
        }
        else
        {
            node.TypeName = value.GetType().Name;
            try { node.DisplayValue = value.ToString(); }
            catch { node.DisplayValue = node.TypeName; }
        }
        return node;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Узел дерева переменных для отображения в отладчике.
/// </summary>
public class DebugVarNode
{
    /// <summary>
    /// Имя переменной (ключ).
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Отображаемое значение (строка).
    /// </summary>
    public string DisplayValue { get; set; }
    /// <summary>
    /// Человекочитаемый тип значения.
    /// </summary>
    public string TypeName { get; set; }
    /// <summary>
    /// Дочерние элементы (для таблиц/сложных типов).
    /// </summary>
    public ObservableCollection<DebugVarNode> Children { get; } = new();
}
