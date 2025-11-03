using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Models;
using Pw.Hub.Infrastructure;
using Pw.Hub.ViewModels;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

/// <summary>
/// Диалог параметров модуля. Бизнес-логика и состояние вынесены в ModuleArgsViewModel.
/// Данное окно отвечает только за построение динамической формы и сбор значений из UI.
/// </summary>
public partial class ModuleArgsWindow : Window
{
    private readonly ModuleDefinition _module;
    private readonly Dictionary<string, FrameworkElement> _inputs = new();

    // Helper selection models for grouped accounts UI
    private class AccountSelectionRef
    {
        public Account Account { get; set; }
        public CheckBox Toggle { get; set; }
    }
    private class SquadGroupRefs
    {
        public Squad Squad { get; set; }
        public CheckBox HeaderCheck { get; set; }
        public TextBlock HeaderText { get; set; }
        public List<AccountSelectionRef> Items { get; set; } = new();
    }
    private class AccountsGroupedModel
    {
        public List<SquadGroupRefs> Groups { get; set; } = new();
        public IEnumerable<AccountSelectionRef> AllItems => Groups.SelectMany(g => g.Items);
    }

    /// <summary>
    /// VM диалога; хранит значения и команды (Отмена/ОК через ConfirmWithValues).
    /// </summary>
    public ModuleArgsViewModel Vm { get; }

    /// <summary>
    /// Типизированные значения, собранные из формы (оставлено для обратной совместимости с существующим кодом вызова).
    /// После закрытия окна содержит итоговые значения из Vm.
    /// </summary>
    public Dictionary<string, object> Values { get; } = new();
    /// <summary>
    /// Строковые значения (для сохранения LastArgs) — совместимость с существующим кодом.
    /// </summary>
    public Dictionary<string, string> StringValues { get; } = new();

    public ModuleArgsWindow(ModuleDefinition module)
    {
        InitializeComponent();
        _module = module;
        Vm = new ModuleArgsViewModel { Module = module };
        Vm.RequestClose += OnRequestClose;
        DataContext = Vm; // Заголовок и команды берутся из VM
        BuildUi();
        PrefillFromLastArgs();
    }

    /// <summary>
    /// Конструктор для использования с InputDefinitionDto (например, при запуске из Lua редактора).
    /// Создаёт временный ModuleDefinition из списка InputDefinitionDto.
    /// </summary>
    public ModuleArgsWindow(IList<InputDefinitionDto> inputs, string title = "Аргументы запуска")
    {
        InitializeComponent();
        // Создаём временный ModuleDefinition из InputDefinitionDto
        _module = new ModuleDefinition
        {
            Name = title,
            Inputs = inputs.Select(dto => new ModuleInput
            {
                Name = dto.Name ?? string.Empty,
                Label = string.IsNullOrWhiteSpace(dto.Label) ? (dto.Name ?? string.Empty) : dto.Label,
                Type = string.IsNullOrWhiteSpace(dto.Type) ? "string" : dto.Type,
                Default = dto.Default,
                Required = dto.Required
            }).ToList()
        };
        Vm = new ModuleArgsViewModel { Module = _module };
        Vm.RequestClose += OnRequestClose;
        DataContext = Vm;
        BuildUi();
        // Не вызываем PrefillFromLastArgs, т.к. нет сохранённых значений для временного модуля
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
                    var panelSquads = new StackPanel { Orientation = Orientation.Vertical };
                    var buttonsSquads = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,6), HorizontalAlignment = HorizontalAlignment.Right };
                    var btnSelectAllSquads = new Button { Content = "Выбрать все", Margin = new Thickness(0,0,6,0) };
                    var btnClearAllSquads = new Button { Content = "Снять все" };
                    if (TryFindResource("ModernButton") is Style btnStyleSq)
                    {
                        btnSelectAllSquads.Style = btnStyleSq;
                        btnClearAllSquads.Style = btnStyleSq;
                    }
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
                    btnSelectAllSquads.Click += (_, __) =>
                    {
                        try { lbSquads.SelectAll(); } catch { }
                    };
                    btnClearAllSquads.Click += (_, __) =>
                    {
                        try { lbSquads.UnselectAll(); } catch { }
                    };
                    buttonsSquads.Children.Add(btnSelectAllSquads);
                    buttonsSquads.Children.Add(btnClearAllSquads);
                    panelSquads.Children.Add(buttonsSquads);
                    panelSquads.Children.Add(lbSquads);
                    editor = panelSquads;
                    // Store reference to list box for value handling; ensure Tag is set on the actual selector too
                    editor.Tag = input;
                    lbSquads.Tag = input;
                    _inputs[input.Name] = lbSquads; // map the actual selector control
                    sp.Children.Add(editor);
                    continue; // already added panel; skip default add below
                case "аккаунт":
                case "account":
                    var cbAccount = new ComboBox();
                    if (TryFindResource("ModernComboBox") is Style comboStyle2)
                        cbAccount.Style = comboStyle2;
                    try
                    {
                        using var db3 = new AppDbContext();
                        var accounts = db3.Accounts
                            .Include(a => a.Servers)
                                .ThenInclude(s => s.Characters)
                            .Include(a => a.Squad)
                            .OrderBy(a => a.OrderIndex)
                            .ThenBy(a => a.Name)
                            .ToList();
                        cbAccount.ItemsSource = accounts;
                        cbAccount.DisplayMemberPath = nameof(Account.Name);
                    }
                    catch { }
                    editor = cbAccount;
                    break;
                case "аккаунты":
                case "accounts":
                    // Grouped view: accounts by squads with squad-level toggle and tri-state header
                    var rootPanel = new StackPanel { Orientation = Orientation.Vertical };

                    var buttonsAccounts = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,6), HorizontalAlignment = HorizontalAlignment.Right };
                    var btnSelectAllAcc = new Button { Content = "Выбрать все", Margin = new Thickness(0,0,6,0) };
                    var btnClearAllAcc = new Button { Content = "Снять все" };
                    if (TryFindResource("ModernButton") is Style btnStyleAcc)
                    {
                        btnSelectAllAcc.Style = btnStyleAcc;
                        btnClearAllAcc.Style = btnStyleAcc;
                    }
                    buttonsAccounts.Children.Add(btnSelectAllAcc);
                    buttonsAccounts.Children.Add(btnClearAllAcc);
                    rootPanel.Children.Add(buttonsAccounts);

                    var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 260 };
                    var groupsPanel = new StackPanel { Orientation = Orientation.Vertical };
                    scroll.Content = groupsPanel;
                    rootPanel.Children.Add(scroll);

                    // Build data model
                    var model = new AccountsGroupedModel();
                    try
                    {
                        using var db4 = new AppDbContext();
                        var squadsForAcc = db4.Squads
                            .Include(s => s.Accounts)
                            .OrderBy(s => s.OrderIndex).ThenBy(s => s.Name)
                            .ToList();
                        foreach (var sq in squadsForAcc)
                        {
                            var g = new SquadGroupRefs { Squad = sq };

                            // Header with tri-state checkbox and text
                            var headerDock = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                            var headerCheck = new CheckBox { IsThreeState = true, VerticalAlignment = VerticalAlignment.Center };
                            if (TryFindResource("ModernCheckBox") is Style cbStyleHeader)
                                headerCheck.Style = cbStyleHeader;
                            var headerText = new TextBlock { Text = sq.Name, Margin = new Thickness(8,0,0,0) };
                            if (TryFindResource("ModernTextBlock") is Style lblStyleSq)
                                headerText.Style = lblStyleSq;
                            // Click on squad name should toggle selection for the whole group (like header checkbox)
                            headerText.MouseLeftButtonUp += (_, __) =>
                            {
                                try
                                {
                                    var anySelected = g.Items.Any(i => i.Toggle.IsChecked == true);
                                    var target = !anySelected; // if any selected -> clear all; else select all
                                    foreach (var it in g.Items)
                                        it.Toggle.IsChecked = target;
                                    UpdateGroupHeader(g);
                                }
                                catch { }
                            };
                            DockPanel.SetDock(headerCheck, Dock.Left);
                            headerDock.Children.Add(headerCheck);
                            headerDock.Children.Add(headerText);

                            g.HeaderCheck = headerCheck;
                            g.HeaderText = headerText;

                            // Accounts list for the squad
                            var accsSrc = sq.Accounts ?? new System.Collections.ObjectModel.ObservableCollection<Account>();
                            var accs = accsSrc.OrderBy(a => a.OrderIndex).ThenBy(a => a.Name).ToList();
                            var accsPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(24, 2, 0, 8) };
                            foreach (var a in accs)
                            {
                                var t = new CheckBox
                                {
                                    Content = a.Name,
                                    Margin = new Thickness(0, 2, 0, 2),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    Padding = new Thickness(10, 6, 10, 6),
                                };
                                // Apply modern checkbox style
                                if (TryFindResource("ModernCheckBox") is Style cbItemStyle)
                                    t.Style = cbItemStyle;
                                var item = new AccountSelectionRef { Account = a, Toggle = t };
                                // When account toggle changes, update header state text/tri-state
                                t.Checked += (_, __) => { try { UpdateGroupHeader(g); } catch { } };
                                t.Unchecked += (_, __) => { try { UpdateGroupHeader(g); } catch { } };
                                accsPanel.Children.Add(t);
                                g.Items.Add(item);
                            }

                            // Header toggle behavior: click toggles select all/clear all
                            headerCheck.Click += (_, __) =>
                            {
                                try
                                {
                                    var anySelected = g.Items.Any(i => i.Toggle.IsChecked == true);
                                    var target = !(anySelected);
                                    foreach (var it in g.Items)
                                        it.Toggle.IsChecked = target;
                                    UpdateGroupHeader(g);
                                }
                                catch { }
                            };

                            // Container for group
                            var groupContainer = new StackPanel { Orientation = Orientation.Vertical };
                            groupContainer.Children.Add(headerDock);
                            groupContainer.Children.Add(accsPanel);

                            groupsPanel.Children.Add(groupContainer);
                            model.Groups.Add(g);

                            // Initialize header state
                            UpdateGroupHeader(g);
                        }
                    }
                    catch { }

                    // Global buttons behavior
                    btnSelectAllAcc.Click += (_, __) =>
                    {
                        try
                        {
                            foreach (var it in model.AllItems)
                                it.Toggle.IsChecked = true;
                            foreach (var g in model.Groups) UpdateGroupHeader(g);
                        }
                        catch { }
                    };
                    btnClearAllAcc.Click += (_, __) =>
                    {
                        try
                        {
                            foreach (var it in model.AllItems)
                                it.Toggle.IsChecked = false;
                            foreach (var g in model.Groups) UpdateGroupHeader(g);
                        }
                        catch { }
                    };

                    // Store control and model for later prefill/value extraction
                    rootPanel.Tag = input; // keep ModuleInput here (used by generic code)
                    rootPanel.DataContext = model;
                    editor = rootPanel;
                    _inputs[input.Name] = rootPanel;
                    sp.Children.Add(rootPanel);
                    continue;
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

    private void UpdateGroupHeader(SquadGroupRefs g)
    {
        try
        {
            var total = g.Items.Count;
            var selected = g.Items.Count(i => i.Toggle.IsChecked == true);
            bool any = selected > 0;
            bool all = selected == total && total > 0;
            g.HeaderCheck.IsChecked = all ? true : any ? (bool?)null : false;
            // Show counts in header text: "Name — X / N"
            if (g.HeaderText != null)
            {
                var baseName = g.Squad?.Name ?? string.Empty;
                g.HeaderText.Text = total > 0 ? ($"{baseName} — {selected} / {total}") : baseName;
            }
        }
        catch { }
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
                else if (editor is ComboBox comboAcc && (type == "аккаунт" || type == "account"))
                {
                    try
                    {
                        var id = saved;
                        if (!string.IsNullOrWhiteSpace(id) && comboAcc.ItemsSource is System.Collections.IEnumerable items)
                        {
                            foreach (var item in items)
                            {
                                if (item is Account a && string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase))
                                {
                                    comboAcc.SelectedItem = item;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
                else if ((type == "аккаунты" || type == "accounts"))
                {
                    try
                    {
                        var ids = (saved ?? string.Empty).Split(new[]{',',';',' '}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                        if (editor is ListBox listAcc)
                        {
                            foreach (var item in listAcc.Items)
                            {
                                if (item is Account a && set.Contains(a.Id))
                                {
                                    listAcc.SelectedItems.Add(item);
                                }
                            }
                        }
                        else if (editor is FrameworkElement fe && fe.DataContext is AccountsGroupedModel gm)
                        {
                            foreach (var g in gm.Groups)
                            {
                                foreach (var it in g.Items)
                                {
                                    if (it.Account != null && !string.IsNullOrEmpty(it.Account.Id))
                                        it.Toggle.IsChecked = set.Contains(it.Account.Id);
                                }
                                UpdateGroupHeader(g);
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
        // Собираем значения из динамических контролов формы
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
            else if (editor is ComboBox comboAcc && (type == "аккаунт" || type == "account"))
            {
                var selectedAccount = comboAcc.SelectedItem as Account;
                value = selectedAccount; // convert later to Lua table
                stringValue = selectedAccount?.Id ?? string.Empty; // persist Account Id
            }
            else if ((type == "аккаунты" || type == "accounts"))
            {
                if (editor is ListBox listAcc)
                {
                    var accounts = listAcc.SelectedItems.Cast<object>().OfType<Account>().ToList();
                    value = accounts; // list of accounts
                    stringValue = string.Join(",", accounts.Select(a => a.Id ?? string.Empty));
                }
                else if (editor is FrameworkElement fe && fe.DataContext is AccountsGroupedModel gm)
                {
                    var selected = gm.AllItems
                        .Where(i => i.Toggle.IsChecked == true)
                        .Select(i => i.Account)
                        .Where(a => a != null)
                        .ToList();
                    value = selected;
                    stringValue = string.Join(",", selected.Select(a => a.Id ?? string.Empty));
                }
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

        // Передаём значения во ViewModel и инициируем закрытие через VM (MVVM-стиль)
        Vm.ConfirmWithValues(Values, StringValues);
    }

    /// <summary>
    /// Обработчик события VM на закрытие окна: синхронизирует публичные свойства и закрывает окно.
    /// </summary>
    private void OnRequestClose(bool? dialogResult)
    {
        try
        {
            // Копируем итоговые значения из VM (на случай, если вызов шёл не через Ok_Click)
            if (Values.Count == 0 && Vm.Values.Count > 0)
            {
                foreach (var kv in Vm.Values)
                    Values[kv.Key] = kv.Value;
            }
            if (StringValues.Count == 0 && Vm.StringValues.Count > 0)
            {
                foreach (var kv in Vm.StringValues)
                    StringValues[kv.Key] = kv.Value;
            }

            try { DialogResult = dialogResult; } catch { }
        }
        finally
        {
            Close();
        }
    }
}