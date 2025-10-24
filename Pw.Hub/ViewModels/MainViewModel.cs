using System.Collections.ObjectModel;
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
        var squads = db.Squads
            .Include(s => s.Accounts)
            .ThenInclude(x=> x.Servers)
            .ThenInclude(x=> x.Characters)
            .OrderBy(s => s.OrderIndex)
            .ThenBy(s => s.Name)
            .ToList();

        Squads.Clear();
        foreach (var squad in squads)
        {
            // Преобразуем List в ObservableCollection для аккаунтов
            var orderedAccounts = squad.Accounts
                .OrderBy(a => a.OrderIndex)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var observableSquad = new Squad
            {
                Id = squad.Id,
                Name = squad.Name,
                OrderIndex = squad.OrderIndex,
                Accounts = new ObservableCollection<Account>(orderedAccounts)
            };
            Squads.Add(observableSquad);
        }
    }

    public void Reload()
    {
        LoadData();
    }
}
