using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pw.Hub.Models;

public class Account : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string Email
    {
        get;
        set => SetField(ref field, value);
    }

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