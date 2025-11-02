using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pw.Hub.Services;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;

namespace Pw.Hub.Windows;

public class InputTypeTemplateSelector : DataTemplateSelector
{
    public DataTemplate StandardTemplate { get; set; }
    public DataTemplate AccountsTemplate { get; set; }
    public DataTemplate AccountTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is RunArgsWindow.InputItemViewModel vm)
        {
            if (vm.IsAccountsType) return AccountsTemplate;
            if (vm.IsAccountType) return AccountTemplate;
        }
        return StandardTemplate;
    }
}

public partial class RunArgsWindow : Window
{
    public Dictionary<string, object> Result { get; private set; }

    private readonly ObservableCollection<InputItemViewModel> _items = new();
    private readonly IAccountManager _accountManager;

    public RunArgsWindow(IList<InputDefinitionDto> inputs)
    {
        InitializeComponent();

        // Получаем AccountManager через DI
        _accountManager = (App.Services?.GetService(typeof(IAccountManager)) as IAccountManager);

        if (inputs != null)
        {
            foreach (var input in inputs)
            {
                _items.Add(new InputItemViewModel(input, _accountManager));
            }
        }

        Items.ItemsSource = _items;
        
        // Асинхронная загрузка данных для всех полей типа accounts
        Loaded += async (_, __) =>
        {
            try
            {
                foreach (var item in _items)
                {
                    await item.LoadDataAsync();
                }
                
                // Wire up SelectionChanged handlers for all ListBoxes in the visual tree
                Items.LayoutUpdated += (s, e) =>
                {
                    try
                    {
                        WireListBoxEvents(Items);
                        Items.LayoutUpdated -= (s, e) => { }; // Unsubscribe after first layout
                    }
                    catch { }
                };
            }
            catch { }
        };
    }

    /// <summary>
    /// Обновляет входные параметры для повторного использования окна.
    /// </summary>
    public async void UpdateInputs(IList<InputDefinitionDto> inputs)
    {
        try
        {
            Result = null;
            _items.Clear();
            
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    _items.Add(new InputItemViewModel(input, _accountManager));
                }
            }
            
            // Загружаем данные для новых полей
            foreach (var item in _items)
            {
                await item.LoadDataAsync();
            }
            
            // Re-wire ListBox events after visual tree is updated
            try
            {
                // Use LayoutUpdated to ensure ListBox controls are created in visual tree
                Items.LayoutUpdated += OnItemsLayoutUpdatedForWiring;
            }
            catch { }
        }
        catch { }
    }

    private void OnItemsLayoutUpdatedForWiring(object sender, EventArgs e)
    {
        try
        {
            // Unsubscribe immediately to avoid multiple calls
            Items.LayoutUpdated -= OnItemsLayoutUpdatedForWiring;
            // Wire up ListBox events now that visual tree is updated
            WireListBoxEvents(Items);
        }
        catch { }
    }

    private void WireListBoxEvents(DependencyObject parent)
    {
        try
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.ListBox listBox && listBox.Tag is InputItemViewModel)
                {
                    listBox.SelectionChanged -= AccountsList_SelectionChanged; // Prevent double subscription
                    listBox.SelectionChanged += AccountsList_SelectionChanged;
                }
                WireListBoxEvents(child);
            }
        }
        catch { }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        try
        {
            Result = new Dictionary<string, object>();
            foreach (var item in _items)
            {
                if (item.IsAccountsType)
                {
                    // Для типа "accounts" возвращаем массив выбранных аккаунтов
                    Result[item.Name] = item.SelectedAccounts.ToArray();
                }
                else if (item.IsAccountType)
                {
                    // Для типа "account" возвращаем один аккаунт
                    Result[item.Name] = item.SelectedAccounts.FirstOrDefault();
                }
                else
                {
                    // Для остальных типов - значение как есть
                    Result[item.Name] = item.Value ?? string.Empty;
                }
            }
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AccountsList_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.ListBox listBox)
            {
                // Wire up SelectionChanged event
                listBox.SelectionChanged += AccountsList_SelectionChanged;
            }
        }
        catch { }
    }

    private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.DataContext is InputItemViewModel vm)
            {
                // Синхронизируем SelectedAccounts с текущим выбором
                vm.SelectedAccounts.Clear();
                foreach (var item in listBox.SelectedItems)
                {
                    if (item is Account account)
                    {
                        vm.SelectedAccounts.Add(account);
                    }
                }
            }
        }
        catch { }
    }

    private void SelectAllAccounts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is InputItemViewModel vm)
            {
                // Find the ListBox in visual tree
                var listBox = FindListBoxForViewModel(button, vm);
                if (listBox != null)
                {
                    listBox.SelectAll();
                }
            }
        }
        catch { }
    }

    private void DeselectAllAccounts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is InputItemViewModel vm)
            {
                // Find the ListBox in visual tree
                var listBox = FindListBoxForViewModel(button, vm);
                if (listBox != null)
                {
                    listBox.UnselectAll();
                }
            }
        }
        catch { }
    }

    private System.Windows.Controls.ListBox FindListBoxForViewModel(DependencyObject startElement, InputItemViewModel vm)
    {
        try
        {
            // Navigate up to find the parent DockPanel
            var parent = VisualTreeHelper.GetParent(startElement);
            while (parent != null && !(parent is DockPanel))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            if (parent is DockPanel dockPanel)
            {
                // Find ListBox child
                return FindChildListBox(dockPanel);
            }
        }
        catch { }
        return null;
    }

    private System.Windows.Controls.ListBox FindChildListBox(DependencyObject parent)
    {
        try
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.ListBox listBox)
                {
                    return listBox;
                }
                var result = FindChildListBox(child);
                if (result != null)
                    return result;
            }
        }
        catch { }
        return null;
    }

    public class InputItemViewModel : INotifyPropertyChanged
    {
        private readonly IAccountManager _accountManager;

        public string Name { get; }
        public string Label { get; }
        public string Type { get; }
        public bool Required { get; }

        private object _value;
        public object Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<Account> _availableAccounts = new();
        public ObservableCollection<Account> AvailableAccounts
        {
            get => _availableAccounts;
            set
            {
                _availableAccounts = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Account> _selectedAccounts = new();
        public ObservableCollection<Account> SelectedAccounts
        {
            get => _selectedAccounts;
            set
            {
                _selectedAccounts = value;
                OnPropertyChanged();
            }
        }

        public bool IsAccountsType => string.Equals(Type, "accounts", StringComparison.OrdinalIgnoreCase);
        public bool IsAccountType => string.Equals(Type, "account", StringComparison.OrdinalIgnoreCase);
        public bool IsStandardType => !IsAccountsType && !IsAccountType;

        public InputItemViewModel(InputDefinitionDto dto, IAccountManager accountManager)
        {
            _accountManager = accountManager;
            Name = dto.Name ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(dto.Label) ? Name : dto.Label;
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "string" : dto.Type;
            Required = dto.Required;
            Value = dto.Default ?? string.Empty;
        }

        public async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (IsAccountsType || IsAccountType)
            {
                try
                {
                    if (_accountManager != null)
                    {
                        var accounts = await _accountManager.GetAccountsAsync();
                        if (accounts != null)
                        {
                            AvailableAccounts.Clear();
                            foreach (var acc in accounts)
                            {
                                AvailableAccounts.Add(acc);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
