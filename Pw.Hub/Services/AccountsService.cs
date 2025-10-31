using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса управления отрядами и аккаунтами (CRUD) и загрузки данных.
/// Инкапсулирует EF Core доступ и обеспечивает корректную сортировку/OrderIndex.
/// </summary>
public class AccountsService : IAccountsService
{
    /// <inheritdoc />
    public List<Squad> LoadSquads()
    {
        using var db = new AppDbContext();
        db.Database.Migrate();
        var squads = db.Squads
            .Include(s => s.Accounts)
            .ThenInclude(a => a.Servers)
            .ThenInclude(sv => sv.Characters)
            .OrderBy(s => s.OrderIndex)
            .ThenBy(s => s.Name)
            .ToList();

        var result = new List<Squad>(squads.Count);
        foreach (var s in squads)
        {
            var orderedAccounts = s.Accounts
                .OrderBy(a => a.OrderIndex)
                .ThenBy(a => a.Name)
                .ToList();
            result.Add(new Squad
            {
                Id = s.Id,
                Name = s.Name,
                OrderIndex = s.OrderIndex,
                Accounts = new System.Collections.ObjectModel.ObservableCollection<Account>(orderedAccounts)
            });
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<Squad> CreateSquadAsync(string name)
    {
        await using var db = new AppDbContext();
        var maxOrder = await db.Squads.Select(x => (int?)x.OrderIndex).MaxAsync() ?? -1;
        var entity = new Squad { Name = name, OrderIndex = maxOrder + 1 };
        db.Squads.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    /// <inheritdoc />
    public async Task UpdateSquadAsync(Squad squad, string newName)
    {
        if (squad == null) return;
        await using var db = new AppDbContext();
        var entity = await db.Squads.FirstOrDefaultAsync(s => s.Id == squad.Id);
        if (entity == null) return;
        entity.Name = newName;
        db.Update(entity);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteSquadAsync(Squad squad)
    {
        if (squad == null) return;
        await using var db = new AppDbContext();
        var entity = await db.Squads.Include(s => s.Accounts).FirstOrDefaultAsync(s => s.Id == squad.Id);
        if (entity == null) return;
        db.Squads.Remove(entity);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Account> CreateAccountAsync(Squad squad, string accountName)
    {
        await using var db = new AppDbContext();
        var maxOrder = await db.Accounts.Where(a => a.SquadId == squad.Id).Select(a => (int?)a.OrderIndex).MaxAsync() ?? -1;
        var account = new Account
        {
            Name = accountName,
            SquadId = squad.Id,
            OrderIndex = maxOrder + 1,
            ImageSource = string.Empty
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    /// <inheritdoc />
    public async Task UpdateAccountAsync(Account account, string newName)
    {
        if (account == null) return;
        await using var db = new AppDbContext();
        var entity = await db.Accounts.FirstOrDefaultAsync(a => a.Id == account.Id);
        if (entity == null) return;
        entity.Name = newName;
        db.Update(entity);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAccountAsync(Account account)
    {
        if (account == null) return;
        await using var db = new AppDbContext();
        var entity = await db.Accounts.FirstOrDefaultAsync(a => a.Id == account.Id);
        if (entity == null) return;
        db.Accounts.Remove(entity);
        await db.SaveChangesAsync();
    }
}
