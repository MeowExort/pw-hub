using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Pw.Hub.Models;

public class Account : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string SiteId
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string ImageSource
    {
        get;
        set
        {
            SetField(ref field, value);
            OnPropertyChanged(nameof(ImageUri));
        }
    }

    public DateTime LastVisit
    {
        get;
        set => SetField(ref field, value);
    } = DateTime.MinValue;

    // New: order within squad (0-based)
    public int OrderIndex
    {
        get;
        set => SetField(ref field, value);
    } = 0;

    // Convenience property for accessing servers as objects
    public List<AccountServer> Servers
    {
        get;
        set => SetField(ref field, value);
    }

    public Uri ImageUri => string.IsNullOrEmpty(ImageSource) ? null : new Uri(ImageSource);

    public Guid SquadId { get; set; }
    public Squad Squad { get; set; }

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