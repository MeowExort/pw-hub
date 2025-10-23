using System.Collections.ObjectModel;
using System.Windows;

namespace Pw.Hub.Windows;

public partial class DebugVariablesWindow : Window
{
    public ObservableCollection<DebugVarNode> Locals { get; } = new();
    public ObservableCollection<DebugVarNode> Globals { get; } = new();

    public DebugVariablesWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LocalsTree.ItemsSource = Locals; 
            GlobalsTree.ItemsSource = Globals;
        };
    }

    public void SetData(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
    {
        TitleText.Text = $"Остановлено на строке {line}";
        Locals.Clear();
        Globals.Clear();
        foreach (var node in BuildNodes(locals)) Locals.Add(node);
        foreach (var node in BuildNodes(globals)) Globals.Add(node);
    }

    private static IEnumerable<DebugVarNode> BuildNodes(IDictionary<string, object> dict, int depth = 0)
    {
        if (dict == null) yield break;
        foreach (var kv in dict.OrderBy(k => k.Key))
        {
            yield return CreateNode(kv.Key, kv.Value, depth);
        }
    }

    private static DebugVarNode CreateNode(string name, object value, int depth)
    {
        var node = new DebugVarNode { Name = name };
        if (value is IDictionary<string, object> table)
        {
            node.TypeName = "table";
            node.DisplayValue = $"table[{table.Count}]";
            // Lazy build children now (one level immediate to keep it responsive)
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
            node.DisplayValue = value.ToString();
        }

        return node;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

public class DebugVarNode
{
    public string Name { get; set; }
    public string DisplayValue { get; set; }
    public string TypeName { get; set; }
    public ObservableCollection<DebugVarNode> Children { get; } = new();
}