using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public partial class RunArgsWindow : Window
{
    public Dictionary<string, object> Result { get; private set; }

    private readonly ObservableCollection<InputItemViewModel> _items = new();

    public RunArgsWindow(IList<InputDefinitionDto> inputs)
    {
        InitializeComponent();

        if (inputs != null)
        {
            foreach (var input in inputs)
            {
                _items.Add(new InputItemViewModel(input));
            }
        }

        Items.ItemsSource = _items;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        try
        {
            Result = new Dictionary<string, object>();
            foreach (var item in _items)
            {
                Result[item.Name] = item.Value ?? string.Empty;
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

    private class InputItemViewModel : INotifyPropertyChanged
    {
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

        public InputItemViewModel(InputDefinitionDto dto)
        {
            Name = dto.Name ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(dto.Label) ? Name : dto.Label;
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "string" : dto.Type;
            Required = dto.Required;
            Value = dto.Default ?? string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
