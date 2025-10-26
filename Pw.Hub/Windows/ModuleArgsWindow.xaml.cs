using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Models;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Windows;

public partial class ModuleArgsWindow : Window
{
    private readonly ModuleDefinition _module;
    private readonly Dictionary<string, FrameworkElement> _inputs = new();

    public Dictionary<string, object> Values { get; } = new();
    // Stringified values for persistence (LastArgs)
    public Dictionary<string, string> StringValues { get; } = new();

    public ModuleArgsWindow(ModuleDefinition module)
    {
        InitializeComponent();
        _module = module;
        Title = string.IsNullOrWhiteSpace(module.Name) ? "Параметры модуля" : module.Name;
        BuildUi();
        PrefillFromLastArgs();
    }

    private void BuildUi()
    {
        InputsPanel.Children.Clear();
        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 8) };

        if (_module.Inputs.Count == 0)
        {
            var lbl = new TextBlock { Text = "Модуль не требует аргументов запуска." };
            // Apply application style to label
            if (TryFindResource("ModernTextBlock") is Style lblStyle)
                lbl.Style = lblStyle;
            lbl.TextAlignment = TextAlignment.Center;
            sp.Children.Add(lbl);
        }
        
        foreach (var input in _module.Inputs)
        {
            var lbl = new TextBlock { Text = string.IsNullOrWhiteSpace(input.Label) ? input.Name : input.Label };
            // Apply application style to label
            if (TryFindResource("ModernTextBlock") is Style lblStyle)
                lbl.Style = lblStyle;
            sp.Children.Add(lbl);

            FrameworkElement editor;
            switch ((input.Type ?? "string").ToLowerInvariant())
            {
                case "bool":
                case "boolean":
                    var cb = new CheckBox { IsChecked = bool.TryParse(input.Default, out var b) && b };
                    if (TryFindResource("ModernCheckBox") is Style cbStyle)
                        cb.Style = cbStyle;
                    editor = cb;
                    break;
                case "number":
                case "int":
                case "double":
                    var tbNum = new TextBox { Text = input.Default ?? string.Empty };
                    if (TryFindResource("ModernTextBox") is Style tbStyle1)
                        tbNum.Style = tbStyle1;
                    editor = tbNum;
                    break;
                case "password":
                case "пароль":
                    var pb = new PasswordBox();
                    if (!string.IsNullOrEmpty(input.Default)) pb.Password = input.Default;
                    if (TryFindResource("ModernPasswordBox") is Style pbStyle)
                        pb.Style = pbStyle;
                    editor = pb;
                    break;
                case "отряд":
                case "squad":
                    var cbSquad = new ComboBox();
                    if (TryFindResource("ModernComboBox") is Style comboStyle)
                        cbSquad.Style = comboStyle;
                    try
                    {
                        using var db = new AppDbContext();
                        var squads = db.Squads
                            .Include(s => s.Accounts)
                                .ThenInclude(a => a.Servers)
                                    .ThenInclude(sv => sv.Characters)
                            .OrderBy(s => s.OrderIndex)
                            .ThenBy(s => s.Name)
                            .ToList();
                        cbSquad.ItemsSource = squads;
                        cbSquad.DisplayMemberPath = nameof(Squad.Name);
                    }
                    catch { }
                    editor = cbSquad;
                    break;
                case "отряды":
                case "squads":
                    var lbSquads = new ListBox { SelectionMode = SelectionMode.Extended, Height = 160 };
                    if (TryFindResource("ModernListBox") is Style lbStyle)
                        lbSquads.Style = lbStyle;
                    try
                    {
                        using var db2 = new AppDbContext();
                        var squads2 = db2.Squads
                            .Include(s => s.Accounts)
                                .ThenInclude(a => a.Servers)
                                    .ThenInclude(sv => sv.Characters)
                            .OrderBy(s => s.OrderIndex)
                            .ThenBy(s => s.Name)
                            .ToList();
                        lbSquads.ItemsSource = squads2;
                        lbSquads.DisplayMemberPath = nameof(Squad.Name);
                    }
                    catch { }
                    editor = lbSquads;
                    break;
                default:
                    var tb = new TextBox { Text = input.Default ?? string.Empty };
                    if (TryFindResource("ModernTextBox") is Style tbStyle2)
                        tb.Style = tbStyle2;
                    editor = tb;
                    break;
            }
            editor.Tag = input;
            _inputs[input.Name] = editor;
            sp.Children.Add(editor);
        }
        
        InputsPanel.Children.Add(sp);
    }

    private void PrefillFromLastArgs()
    {
        try
        {
            var last = _module.LastArgs ?? new Dictionary<string, string>();
            foreach (var kv in _inputs)
            {
                var name = kv.Key;
                var editor = kv.Value;
                var def = (ModuleInput)editor.Tag;
                if (!last.TryGetValue(name, out var saved)) continue;
                var type = (def.Type ?? "string").ToLowerInvariant();
                if (editor is CheckBox cb)
                {
                    if (bool.TryParse(saved, out var b)) cb.IsChecked = b;
                }
                else if (editor is TextBox tb)
                {
                    // Keep saved string as-is; for numbers, ensure it is formatted invariant
                    tb.Text = saved ?? string.Empty;
                }
                else if (editor is PasswordBox pb)
                {
                    pb.Password = saved ?? string.Empty;
                }
                else if (editor is ComboBox combo && (type == "отряд" || type == "squad"))
                {
                    try
                    {
                        var id = saved;
                        if (!string.IsNullOrWhiteSpace(id) && combo.ItemsSource is System.Collections.IEnumerable items)
                        {
                            foreach (var item in items)
                            {
                                if (item is Squad s && string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                                {
                                    combo.SelectedItem = item;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
                else if (editor is ListBox list && (type == "отряды" || type == "squads"))
                {
                    try
                    {
                        var ids = (saved ?? string.Empty).Split(new[]{',',';',' '}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                        foreach (var item in list.Items)
                        {
                            if (item is Squad s && set.Contains(s.Id))
                            {
                                list.SelectedItems.Add(item);
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Values.Clear();
        StringValues.Clear();
        foreach (var kv in _inputs)
        {
            var name = kv.Key;
            var editor = kv.Value;
            var def = (ModuleInput)editor.Tag;
            object value = null;
            string stringValue = string.Empty;
            var type = (def.Type ?? "string").ToLowerInvariant();
            if (editor is CheckBox cb)
            {
                var b = cb.IsChecked == true;
                value = b;
                stringValue = b ? "true" : "false";
            }
            else if (editor is TextBox tb)
            {
                var text = tb.Text ?? string.Empty;
                if (type is "number" or "int" or "double")
                {
                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        value = d;
                        stringValue = d.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        value = 0d;
                        stringValue = "0";
                    }
                }
                else
                {
                    value = text;
                    stringValue = text;
                }
            }
            else if (editor is PasswordBox pb)
            {
                var text = pb.Password ?? string.Empty;
                value = text;
                stringValue = text;
            }
            else if (editor is ComboBox combo && (type == "отряд" || type == "squad"))
            {
                var selectedSquad = combo.SelectedItem as Squad;
                value = selectedSquad; // keep as object; LuaScriptRunner will convert to Lua table
                stringValue = selectedSquad?.Id ?? string.Empty; // persist Squad Id
            }
            else if (editor is ListBox list && (type == "отряды" || type == "squads"))
            {
                var squads = list.SelectedItems.Cast<object>().OfType<Squad>().ToList();
                value = squads; // list of squads; integration will convert to Lua table
                stringValue = string.Join(",", squads.Select(s => s.Id ?? string.Empty));
            }

            if (def.Required && (value == null 
                                 || (value is string str && string.IsNullOrWhiteSpace(str))
                                 || (value is System.Collections.ICollection col && col.Count == 0)))
            {
                MessageBox.Show($"Заполните поле: {def.Label ?? def.Name}", "Параметры модуля", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Values[name] = value;
            StringValues[name] = stringValue ?? string.Empty;
        }
        DialogResult = true;
        Close();
    }
}