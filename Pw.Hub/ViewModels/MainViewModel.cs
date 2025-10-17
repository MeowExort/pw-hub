using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.ViewModels;

public class MainViewModel : BaseViewModel
{
    public ObservableCollection<Squad> Squads { get; } = new();

    public MainViewModel()
    {
        LoadData();
    }

    private void LoadData()
    {
        using var db = new AppDbContext();

        // Создаем базу данных если её нет
        db.Database.Migrate();

        // Загружаем отряды с аккаунтами
        var squads = db.Squads.Include(s => s.Accounts).ToList();

        Squads.Clear();
        foreach (var squad in squads)
        {
            // Преобразуем List в ObservableCollection для аккаунтов
            var observableSquad = new Squad
            {
                Id = squad.Id,
                Name = squad.Name,
                Accounts = new ObservableCollection<Account>(squad.Accounts)
            };
            Squads.Add(observableSquad);
        }
    }

    public void Reload()
    {
        LoadData();
    }
}
