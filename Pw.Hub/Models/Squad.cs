using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pw.Hub.Models;

public class Squad : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    // New: order among squads (0-based)
    private int _orderIndex = 0;
    public int OrderIndex
    {
        get => _orderIndex;
        set => SetField(ref _orderIndex, value);
    }

    public ObservableCollection<Account> Accounts { get; set; } = new ObservableCollection<Account>();

    public override string ToString() => Name;
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}