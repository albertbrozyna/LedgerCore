using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Common.Ledger;

/// <summary>Płaski wiersz zapisu księgowego – baza do wszystkich raportów.</summary>
public record EntryRow(
    Guid EntryId, Guid TransactionId, Guid AccountId, string Number, DateTime Date, DateTime CreatedAt,
    string Description, decimal Amount, DebitCredit Side);

/// <summary>Obroty konta w groszach (sumy liczone w SQL na kolumnie całkowitej – bez błędów zaokrągleń).</summary>
public record AccountSum(Guid AccountId, long DebitCents, long CreditCents)
{
    public decimal Debit => DebitCents / 100m;
    public decimal Credit => CreditCents / 100m;
    /// <summary>Saldo netto (Winien minus Ma).</summary>
    public decimal Net => (DebitCents - CreditCents) / 100m;
}

public static class LedgerQueries
{
    /// <summary>
    /// Obroty Winien/Ma per konto w zakresie dat (granice włącznie), liczone po stronie bazy.
    /// <paramref name="excludeClosing"/> pomija księgowania zamykające rok (ZR/…), jak raporty wyników.
    /// </summary>
    public static async Task<Dictionary<Guid, AccountSum>> Sums(
        IAppDbContext db, DateTime? from, DateTime? to, bool excludeClosing, Guid? accountId, CancellationToken ct)
        => await SumsFor(db, from, to, excludeClosing, accountId.HasValue ? [accountId.Value] : null, ct);

    /// <summary>Jak wyżej, ale dla wskazanych kont (null = wszystkie).</summary>
    public static async Task<Dictionary<Guid, AccountSum>> SumsFor(
        IAppDbContext db, DateTime? from, DateTime? to, bool excludeClosing, List<Guid>? accountIds, CancellationToken ct)
    {
        var q = db.JournalEntries.AsNoTracking().AsQueryable();
        if (accountIds != null) q = q.Where(e => accountIds.Contains(e.AccountId));
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(e => e.Date <= to.Value.Date);
        if (excludeClosing) q = q.Where(e => !e.IsClosing);
        var rows = await q.GroupBy(e => new { e.AccountId, e.Side })
            .Select(g => new { g.Key.AccountId, g.Key.Side, Cents = g.Sum(e => e.AmountCents) })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.AccountId).ToDictionary(g => g.Key, g => new AccountSum(g.Key,
            g.Where(r => r.Side == DebitCredit.Debit).Sum(r => r.Cents), g.Where(r => r.Side == DebitCredit.Credit).Sum(r => r.Cents)));
    }

    public static AccountSum Of(Dictionary<Guid, AccountSum> sums, Guid accountId) =>
        sums.TryGetValue(accountId, out var s) ? s : new AccountSum(accountId, 0, 0);

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
