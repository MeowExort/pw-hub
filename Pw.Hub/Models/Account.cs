using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Pw.Hub.Models;

public class Account : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    private string _siteId = string.Empty;
    public string SiteId
    {
        get => _siteId;
        set => SetField(ref _siteId, value);
    }

    private string _imageSource;
    public string ImageSource
    {
        get => _imageSource;
        set
        {
            if (SetField(ref _imageSource, value))
            {
                OnPropertyChanged(nameof(ImageUri));
            }
        }
    }

    private DateTime _lastVisit = DateTime.MinValue;
    public DateTime LastVisit
    {
        get => _lastVisit;
        set => SetField(ref _lastVisit, value);
    }

    // New: order within squad (0-based)
    private int _orderIndex = 0;
    public int OrderIndex
    {
        get => _orderIndex;
        set => SetField(ref _orderIndex, value);
    }

    // Convenience property for accessing servers as objects
    private List<AccountServer> _servers;
    public List<AccountServer> Servers
    {
        get => _servers;
        set => SetField(ref _servers, value);
    }

    public Uri ImageUri => string.IsNullOrEmpty(ImageSource) ? null : new Uri(ImageSource);

    public string SquadId { get; set; }
    public Squad Squad { get; set; }

    public override string ToString() => Name;
    public event PropertyChangedEventHandler PropertyChanged;

    // --- Runtime-only promo form snapshot (not persisted to DB) ---
    // Значения последней отправленной формы на странице promo_items.php
    private string _promoDo;
    private List<string> _promoCartItems;
    private string _promoAccInfo;
    private DateTime? _promoLastSubmittedAt;

    [NotMapped]
    public string PromoDo
    {
        get => _promoDo;
        set => SetField(ref _promoDo, value);
    }

    [NotMapped]
    public List<string> PromoCartItems
    {
        get => _promoCartItems;
        set => SetField(ref _promoCartItems, value);
    }

    [NotMapped]
    public string PromoAccInfo
    {
        get => _promoAccInfo;
        set => SetField(ref _promoAccInfo, value);
    }

    [NotMapped]
    public DateTime? PromoLastSubmittedAt
    {
        get => _promoLastSubmittedAt;
        set => SetField(ref _promoLastSubmittedAt, value);
    }

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