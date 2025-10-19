using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using Pw.Hub.Models;

namespace Pw.Hub.Windows;

public partial class ModuleEditorWindow : Window
{
    public ModuleDefinition Module { get; private set; }
    public ObservableCollection<ModuleInput> Inputs { get; } = new();

    public ModuleEditorWindow(ModuleDefinition? module = null)
    {
        InitializeComponent();
        if (module == null)
        {
            Module = new ModuleDefinition();
        }
        else
        {
            // clone to avoid editing original if canceled
            Module = new ModuleDefinition
            {
                Id = module.Id,
                Name = module.Name,
                Description = module.Description,
                Script = module.Script,
                Inputs = module.Inputs?.Select(i => new ModuleInput
                {
                    Name = i.Name,
                    Label = i.Label,
                    Type = i.Type,
                    Default = i.Default,
                    Required = i.Required
                }).ToList() ?? new()
            };
        }

        NameTextBox.Text = Module.Name;
        DescriptionTextBox.Text = Module.Description;
        ScriptTextBox.Text = Module.Script;

        foreach (var input in Module.Inputs)
            Inputs.Add(input);
        InputsGrid.ItemsSource = Inputs;

        Title = string.IsNullOrWhiteSpace(Module.Name) ? "Модуль" : $"Модуль — {Module.Name}";
    }

    private void BrowseScript_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выбор Lua-скрипта",
            Filter = "Lua files (*.lua)|*.lua|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == true)
        {
            ScriptTextBox.Text = dlg.FileName;
        }
    }

    private void AddInput_Click(object sender, RoutedEventArgs e)
    {
        Inputs.Add(new ModuleInput { Name = "param", Label = "Параметр", Type = "string", Default = "", Required = false });
    }

    private void RemoveInput_Click(object sender, RoutedEventArgs e)
    {
        if (InputsGrid.SelectedItem is ModuleInput mi)
        {
            Inputs.Remove(mi);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        var script = ScriptTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Введите название модуля", "Модуль", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(script))
        {
            MessageBox.Show(this, "Укажите путь к Lua-скрипту", "Модуль", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Module.Name = name;
        Module.Description = DescriptionTextBox.Text ?? string.Empty;
        Module.Script = script;
        Module.Inputs = Inputs.ToList();
        if (string.IsNullOrWhiteSpace(Module.Id))
        {
            Module.Id = name.Replace(' ', '_');
        }

        DialogResult = true;
        Close();
    }
}
