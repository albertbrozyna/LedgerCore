using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Common.Ledger;

/// <summary>Płaski wiersz zapisu księgowego – baza do wszystkich raportów.</summary>
public record EntryRow(
    Guid EntryId, Guid TransactionId, Guid AccountId, string Number, DateTime Date, DateTime CreatedAt,
    string Description, decimal Amount, DebitCredit Side);

public static class LedgerQueries
{
    /// <summary>
    /// Wczytuje zapisy księgowe. Agregacja odbywa się w pamięci, bo SQLite nie wspiera SUM na decimal –
    /// przy skali małego biura (dziesiątki tysięcy zapisów) jest to w zupełności wystarczające.
    /// </summary>
    public static async Task<List<EntryRow>> LoadEntries(
        IAppDbContext db, DateTime? from, DateTime? to, Guid? accountId, CancellationToken ct)
    {
        var q = db.JournalEntries.AsNoTracking().AsQueryable();
        if (accountId.HasValue) q = q.Where(e => e.AccountId == accountId.Value);
        if (from.HasValue) q = q.Where(e => e.Transaction.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(e => e.Transaction.Date <= to.Value.Date);

        var rows = await q.Select(e => new EntryRow(
                e.Id, e.TransactionId, e.AccountId, e.Transaction.Number, e.Transaction.Date, e.Transaction.CreatedAt,
                e.Transaction.Description, e.Amount, e.Side))
            .ToListAsync(ct);

        return rows.OrderBy(r => r.Date).ThenBy(r => r.CreatedAt).ThenBy(r => r.Number).ToList();
    }

    /// <summary>Saldo netto (Winien minus Ma).</summary>
    public static decimal Net(IEnumerable<EntryRow> rows) =>
        rows.Sum(r => r.Side == DebitCredit.Debit ? r.Amount : -r.Amount);

    /// <summary>Saldo w "naturalnym" znaku konta (dodatnie = normalna strona konta).</summary>
    public static decimal Natural(Account a, decimal net) => a.IsDebitNormal ? net : -net;
}
