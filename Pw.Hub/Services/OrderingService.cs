using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Models;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса управления порядком (OrderIndex) отрядов и аккаунтов.
/// Пересчитывает индексы в БД и обеспечивает корректные вставки до/после целевого элемента.
/// </summary>
public class OrderingService : IOrderingService
{
    /// <inheritdoc />
    public async Task ReorderSquadAsync(Squad moved, Squad target, bool insertAfter)
    {
        if (moved == null || target == null || moved.Id == target.Id) return;
        await using var db = new AppDbContext();
        var squads = await db.Squads.OrderBy(s => s.OrderIndex).ThenBy(s => s.Name).ToListAsync();

        // Удаляем moved из текущей позиции
        var movedIdx = squads.FindIndex(s => s.Id == moved.Id);
        if (movedIdx < 0) return;
        squads.RemoveAt(movedIdx);

        // Ищем позицию вставки относительно target
        var targetIdx = squads.FindIndex(s => s.Id == target.Id);
        if (targetIdx < 0) return;
        var insertIdx = insertAfter ? targetIdx + 1 : targetIdx;
        if (insertIdx < 0) insertIdx = 0;
        if (insertIdx > squads.Count) insertIdx = squads.Count;
        squads.Insert(insertIdx, moved);

        // Пересчитываем OrderIndex по порядку
        for (int i = 0; i < squads.Count; i++)
        {
            var s = squads[i];
            if (s.OrderIndex != i)
            {
                s.OrderIndex = i;
                db.Update(s);
            }
        }
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ReorderAccountAsync(Account moved, Squad targetSquad, Account? targetAccount, bool insertAfter)
    {
        if (moved == null || targetSquad == null) return;
        await using var db = new AppDbContext();

        // Загружаем целевой отряд с аккаунтами
        var squad = await db.Squads
            .Include(s => s.Accounts)
            .FirstOrDefaultAsync(s => s.Id == targetSquad.Id);
        if (squad == null) return;

        // Если аккаунт переносится между отрядами — обновим SquadId
        if (!string.Equals(moved.SquadId, squad.Id, StringComparison.Ordinal))
        {
            var movedEntity = await db.Accounts.FirstOrDefaultAsync(a => a.Id == moved.Id);
            if (movedEntity == null) return;
            movedEntity.SquadId = squad.Id;
            await db.SaveChangesAsync();
        }

        // Обновим набор аккаунтов (на всякий случай перезагружаем)
        await db.Entry(squad).Collection(s => s.Accounts).LoadAsync();
        var accounts = squad.Accounts.OrderBy(a => a.OrderIndex).ThenBy(a => a.Name).ToList();

        // Удаляем перемещаемый из текущего списка (возможно, из другого отряда)
        accounts.RemoveAll(a => a.Id == moved.Id);

        // Определяем индекс вставки
        int insertIdx;
        if (targetAccount == null)
        {
            insertIdx = accounts.Count; // в конец
        }
        else
        {
            var targetIdx = accounts.FindIndex(a => a.Id == targetAccount.Id);
            insertIdx = targetIdx < 0 ? accounts.Count : (insertAfter ? targetIdx + 1 : targetIdx);
        }
        var movedCopy = await db.Accounts.FirstOrDefaultAsync(a => a.Id == moved.Id);
        if (movedCopy == null) return;
        // Синхронизируем имя (может отличаться в VM) для детерминированной сортировки, не обязательно
        movedCopy.Name = movedCopy.Name;
        movedCopy.SquadId = squad.Id;
        accounts.Insert(insertIdx, movedCopy);

        // Пересчитываем OrderIndex и сохраняем
        for (int i = 0; i < accounts.Count; i++)
        {
            var a = accounts[i];
            if (a.OrderIndex != i)
            {
                a.OrderIndex = i;
                db.Update(a);
            }
        }
        await db.SaveChangesAsync();
    }
}
