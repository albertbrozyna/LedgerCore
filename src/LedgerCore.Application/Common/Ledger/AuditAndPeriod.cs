using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Common.Ledger;

/// <summary>Dziennik audytu: kto, kiedy i co zmienił. Zapis następuje razem z operacją (przy jej SaveChanges).</summary>
public class AuditLog(IAppDbContext db, IUserDetails me)
{
    public async Task Add(string action, string details, CancellationToken ct)
    {
        var id = me.User?.Id;
        var name = "system";
        if (id != null)
        {
            var u = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.FirstName, x.LastName, x.Email }).FirstOrDefaultAsync(ct);
            name = u is null ? id.ToString()! : $"{u.FirstName} {u.LastName} ({u.Email})";
        }
        db.AuditEntries.Add(new AuditEntry(id, name, action, details));
    }
}

public static class PeriodGuard
{
    public static async Task<DateTime?> LockedThrough(IAppDbContext db, CancellationToken ct) =>
        await db.PeriodLocks.AsNoTracking().Select(p => p.LockedThrough).FirstOrDefaultAsync(ct);

    /// <summary>Rzuca wyjątek, gdy data wypada w zamkniętym okresie.</summary>
    public static async Task EnsureOpen(IAppDbContext db, DateTime date, CancellationToken ct)
    {
        var locked = await LockedThrough(db, ct);
        if (locked.HasValue && date.Date <= locked.Value.Date)
            throw new DomainValidationException($"Okres jest zamknięty do {locked.Value:yyyy-MM-dd} – nie można księgować z datą {date:yyyy-MM-dd}");
    }
}
