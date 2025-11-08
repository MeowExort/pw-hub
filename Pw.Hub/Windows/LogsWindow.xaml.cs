using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pw.Hub.Infrastructure.Logging;

namespace Pw.Hub.Windows;

public partial class LogsWindow : Window
{
    private static LogsWindow? _instance;
    private readonly ObservableCollection<LogEntry> _items = new();

    public LogsWindow()
    {
        InitializeComponent();
        List.ItemsSource = _items;
        Closing += (_, e) =>
        {
            // Hide instead of close so Toggle works
            e.Cancel = true;
            Hide();
        };

        try
        {
            Log.EntryPublished += OnEntry;
        }
        catch { }

        LevelFilter.SelectionChanged += (_, _) => ApplyFilter();
        CategoryFilter.TextChanged += (_, _) => ApplyFilter();
    }

    private LogLevel MinLevelFromUi()
    {
        var idx = LevelFilter.SelectedIndex;
        return idx switch
        {
            0 => LogLevel.Debug,
            1 => LogLevel.Information,
            2 => LogLevel.Warning,
            3 => LogLevel.Error,
            _ => LogLevel.Information
        };
    }

    private void OnEntry(LogEntry e)
    {
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Filter on add for performance
                if (e.Level < MinLevelFromUi()) return;
                var cat = (CategoryFilter.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cat) && (e.Category?.IndexOf(cat, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    return;
                _items.Add(e);
                const int max = 2000;
                if (_items.Count > max)
                {
                    while (_items.Count > max)
                        _items.RemoveAt(0);
                }
                if (AutoScroll.IsChecked == true && _items.Count > 0)
                {
                    try
                    {
                        List.ScrollIntoView(_items[^1]);
                    }
                    catch { }
                }
            }));
        }
        catch { }
    }

    private void ApplyFilter()
    {
        try
        {
            // Simple refilter by clearing and will be filled as new entries come in; also try load tail of today file
            _items.Clear();
        }
        catch { }
    }

    public static void Toggle(Window? owner = null)
    {
        try
        {
            if (_instance == null)
            {
                _instance = new LogsWindow();
                if (owner != null) _instance.Owner = owner;
            }
            if (_instance.IsVisible)
                _instance.Hide();
            else
                _instance.Show();
        }
        catch { }
    }

    public static void ShowOnDebug(Window? owner = null)
    {
        try
        {
            if (!RuntimeOptions.DebugMode) return;
            if (_instance == null)
            {
                _instance = new LogsWindow();
                if (owner != null) _instance.Owner = owner;
            }
            _instance.Show();
        }
        catch { }
    }
}
