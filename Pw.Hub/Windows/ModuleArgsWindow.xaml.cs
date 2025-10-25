using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Pw.Hub.Models;

namespace Pw.Hub.Windows;

public partial class ModuleArgsWindow : Window
{
    private readonly ModuleDefinition _module;
    private readonly Dictionary<string, FrameworkElement> _inputs = new();

    public Dictionary<string, object> Values { get; } = new();

    public ModuleArgsWindow(ModuleDefinition module)
    {
        InitializeComponent();
        _module = module;
        Title = string.IsNullOrWhiteSpace(module.Name) ? "Параметры модуля" : module.Name;
        BuildUi();
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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Values.Clear();
        foreach (var kv in _inputs)
        {
            var name = kv.Key;
            var editor = kv.Value;
            var def = (ModuleInput)editor.Tag;
            object value = null;
            var type = (def.Type ?? "string").ToLowerInvariant();
            if (editor is CheckBox cb)
            {
                value = cb.IsChecked == true;
            }
            else if (editor is TextBox tb)
            {
                var text = tb.Text ?? string.Empty;
                if (type is "number" or "int" or "double")
                {
                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        value = d;
                    else
                        value = 0d;
                }
                else
                {
                    value = text;
                }
            }

            if (def.Required && (value == null || (value is string s && string.IsNullOrWhiteSpace(s))))
            {
                MessageBox.Show($"Заполните поле: {def.Label ?? def.Name}", "Параметры модуля", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Values[name] = value;
        }
        DialogResult = true;
        Close();
    }
}