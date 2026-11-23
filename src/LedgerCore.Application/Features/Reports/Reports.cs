using System.Globalization;
using System.Text;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Reports;

/// <summary>Zestawienie obrotów i sald (bilans próbny) za okres. Sumy liczone w SQL (T-017).</summary>
public static class TrialBalance
{
    public record Query(DateTime? From, DateTime? To) : IRequest<Response>;

    /// <summary>Opening/Closing: saldo netto (Winien - Ma). Dodatnie = saldo Winien.</summary>
    public record Row(Guid AccountId, string Code, string Name, AccountType Type,
        decimal Opening, decimal DebitTurnover, decimal CreditTurnover, decimal Closing);

    public record Response(List<Row> Rows, decimal TotalDebitTurnover, decimal TotalCreditTurnover);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var accounts = await db.Accounts.AsNoTracking().ToListAsync(ct);
            var from = r.From?.Date;
            var before = from.HasValue ? await LedgerQueries.Sums(db, null, from.Value.AddDays(-1), false, null, ct) : new();
            var period = await LedgerQueries.Sums(db, from, r.To, false, null, ct);

            var rows = accounts.OrderBy(a => a.Code, StringComparer.Ordinal).Select(a =>
            {
                var opening = LedgerQueries.Of(before, a.Id).Net;
                var p = LedgerQueries.Of(period, a.Id);
                return new Row(a.Id, a.Code, a.Name, a.Type, opening, p.Debit, p.Credit, opening + p.Debit - p.Credit);
            }).Where(x => x.Opening != 0 || x.DebitTurnover != 0 || x.CreditTurnover != 0 || x.Closing != 0).ToList();

            return new Response(rows, rows.Sum(x => x.DebitTurnover), rows.Sum(x => x.CreditTurnover));
        }
    }
}

/// <summary>
/// Karta konta (zapisy z saldem narastającym). Bez <c>Page</c> zwraca wszystkie wiersze (wydruk/eksport),
/// z <c>Page</c> – jedną stronę; salda i sumy dotyczą zawsze całego okresu.
/// </summary>
public static class AccountLedger
{
    public record Query(Guid AccountId, DateTime? From, DateTime? To, int? Page = null, int PageSize = 50) : IRequest<Response>;

    public record Line(Guid TransactionId, string Number, DateTime Date, string Description,
        decimal Debit, decimal Credit, decimal Balance);

    /// <summary>Salda w znaku naturalnym konta. TotalDebit/TotalCredit/ClosingBalance dotyczą całego okresu, nie tylko strony.</summary>
    public record Response(Guid AccountId, string Code, string Name, AccountType Type,
        decimal OpeningBalance, List<Line> Lines, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance,
        int TotalCount, int Page, int PageSize);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var a = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.AccountId, ct)
                    ?? throw new AccountNotFoundException(r.AccountId);
            var from = r.From?.Date;
            var to = r.To?.Date;

            var opening = from.HasValue
                ? LedgerQueries.Natural(a, LedgerQueries.Of(await LedgerQueries.Sums(db, null, from.Value.AddDays(-1), false, a.Id, ct), a.Id).Net)
                : 0m;
            var total = LedgerQueries.Of(await LedgerQueries.Sums(db, from, to, false, a.Id, ct), a.Id);

            var q = db.JournalEntries.AsNoTracking().Where(e => e.AccountId == a.Id);
            if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
            if (to.HasValue) q = q.Where(e => e.Date <= to.Value);
            var ordered = q.OrderBy(e => e.Date).ThenBy(e => e.Transaction.CreatedAt).ThenBy(e => e.Transaction.Number);
            var count = await q.CountAsync(ct);

            var paged = r.Page.HasValue;
            var size = Math.Clamp(r.PageSize, 1, 500);
            var page = paged ? Math.Max(1, r.Page!.Value) : 1;
            var skip = paged ? (page - 1) * size : 0;

            // saldo na początku strony = saldo otwarcia + suma wierszy poprzedzających stronę (same kwoty, bez opisów)
            var running = opening;
            if (skip > 0)
            {
                var before = await ordered.Take(skip).Select(e => new { e.Side, e.AmountCents }).ToListAsync(ct);
                running += Signed(a, before.Sum(x => x.Side == DebitCredit.Debit ? x.AmountCents : -x.AmountCents));
            }

            var rowsQuery = paged ? ordered.Skip(skip).Take(size) : ordered;
            var rows = await rowsQuery.Select(e => new
            {
                e.TransactionId, e.Transaction.Number, e.Date, e.Transaction.Description, e.AmountCents, e.Side
            }).ToListAsync(ct);

            var lines = new List<Line>(rows.Count);
            foreach (var e in rows)
            {
                var amount = e.AmountCents / 100m;
                var signed = e.Side == DebitCredit.Debit ? amount : -amount;
                running += a.IsDebitNormal ? signed : -signed;
                lines.Add(new Line(e.TransactionId, e.Number, e.Date, e.Description,
                    e.Side == DebitCredit.Debit ? amount : 0, e.Side == DebitCredit.Credit ? amount : 0, running));
            }

            var closing = opening + LedgerQueries.Natural(a, total.Net);
            return new Response(a.Id, a.Code, a.Name, a.Type, opening, lines, total.Debit, total.Credit, closing,
                count, paged ? page : 1, paged ? size : Math.Max(count, 1));
        }

        static decimal Signed(Account a, long netCents) => LedgerQueries.Natural(a, netCents / 100m);
    }
}

/// <summary>Rachunek wyników: przychody minus koszty za okres.</summary>
public static class ProfitAndLoss
{
    public record Query(DateTime From, DateTime To) : IRequest<Response>;
    public record Row(Guid AccountId, string Code, string Name, decimal Amount);
    public record Response(List<Row> Income, List<Row> Expenses, decimal TotalIncome, decimal TotalExpenses, decimal Result);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var accounts = await db.Accounts.AsNoTracking()
                .Where(a => a.Type == AccountType.Income || a.Type == AccountType.Expense).ToListAsync(ct);
            var sums = await LedgerQueries.Sums(db, r.From, r.To, true, null, ct);

            List<Row> Build(AccountType t) => accounts.Where(a => a.Type == t).OrderBy(a => a.Code, StringComparer.Ordinal)
                .Select(a => new Row(a.Id, a.Code, a.Name, LedgerQueries.Natural(a, LedgerQueries.Of(sums, a.Id).Net)))
                .Where(x => x.Amount != 0).ToList();

            var income = Build(AccountType.Income);
            var expenses = Build(AccountType.Expense);
            var ti = income.Sum(x => x.Amount);
            var te = expenses.Sum(x => x.Amount);
            return new Response(income, expenses, ti, te, ti - te);
        }
    }
}

public static class Dashboard
{
    public record Query : IRequest<Response>;
    public record RecentTx(Guid Id, string Number, DateTime Date, string Description, decimal Amount);
    public record Response(decimal Assets, decimal Liabilities, decimal Equity, decimal MonthIncome, decimal MonthExpenses,
        decimal MonthResult, int AccountCount, int TransactionCount, List<RecentTx> Recent,
        decimal Receivables, decimal ReceivablesOverdue, decimal Payables, decimal PayablesOverdue, List<MonthPoint> Months);
    public record MonthPoint(string Month, decimal Income, decimal Expenses);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query request, CancellationToken ct)
        {
            var accounts = await db.Accounts.AsNoTracking().ToListAsync(ct);
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var firstMonth = monthStart.AddMonths(-11);

            var all = await LedgerQueries.Sums(db, null, null, false, null, ct);                 // salda bilansowe (łącznie z zamknięciem roku)
            var live = await LedgerQueries.Sums(db, monthStart, null, true, null, ct);           // wynik bieżącego miesiąca (bez ZR/)
            decimal Bal(AccountType t, Dictionary<Guid, AccountSum> s) =>
                accounts.Where(a => a.Type == t).Sum(a => LedgerQueries.Natural(a, LedgerQueries.Of(s, a.Id).Net));

            var income = Bal(AccountType.Income, live);
            var expenses = Bal(AccountType.Expense, live);

            var recent = await db.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
                .Take(5).Select(t => new { t.Id, t.Number, t.Date, t.Description, Debits = t.Entries.Where(e => e.Side == DebitCredit.Debit).Select(e => e.AmountCents).ToList() })
                .ToListAsync(ct);

            var open = (await db.Invoices.AsNoTracking().Where(i => !i.IsCanceled).ToListAsync(ct)).Where(i => i.Remaining > 0).ToList();
            decimal Rem(InvoiceType t, bool overdueOnly) => open.Where(i => i.Type == t && (!overdueOnly || i.DueDate < today)).Sum(i => i.Remaining);

            // Ostatnie 12 miesięcy (bez księgowań zamykających rok): obroty dzienne per konto i strona, miesiące składane w pamięci
            var incomeIds = accounts.Where(a => a.Type == AccountType.Income).Select(a => a.Id).ToHashSet();
            var expenseIds = accounts.Where(a => a.Type == AccountType.Expense).Select(a => a.Id).ToHashSet();
            var daily = await db.JournalEntries.AsNoTracking()
                .Where(e => !e.IsClosing && e.Date >= firstMonth)
                .GroupBy(e => new { e.Date, e.AccountId, e.Side })
                .Select(g => new { g.Key.Date, g.Key.AccountId, g.Key.Side, Cents = g.Sum(e => e.AmountCents) })
                .ToListAsync(ct);
            var months = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i)).Select(m =>
            {
                long Net(HashSet<Guid> ids) => daily.Where(e => e.Date.Year == m.Year && e.Date.Month == m.Month && ids.Contains(e.AccountId))
                    .Sum(e => e.Side == DebitCredit.Debit ? e.Cents : -e.Cents);
                return new MonthPoint(m.ToString("yyyy-MM"), -Net(incomeIds) / 100m, Net(expenseIds) / 100m);
            }).ToList();

            return new Response(Bal(AccountType.Asset, all), Bal(AccountType.Liability, all), Bal(AccountType.Equity, all),
                income, expenses, income - expenses, accounts.Count, await db.Transactions.CountAsync(ct),
                recent.Select(t => new RecentTx(t.Id, t.Number, t.Date, t.Description, t.Debits.Sum() / 100m)).ToList(),
                Rem(InvoiceType.Sales, false), Rem(InvoiceType.Sales, true), Rem(InvoiceType.Purchase, false), Rem(InvoiceType.Purchase, true), months);
        }
    }
}

/// <summary>Eksport dziennika do CSV (separator ';' i przecinek dziesiętny – otwiera się poprawnie w polskim Excelu).</summary>
public static class ExportJournal
{
    public record Query(DateTime? From, DateTime? To) : IRequest<byte[]>;

    public class Handler(IAppDbContext db) : IRequestHandler<Query, byte[]>
    {
        public async Task<byte[]> Handle(Query r, CancellationToken ct)
        {
            var accounts = await db.Accounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
            var entries = await LedgerQueries.LoadEntries(db, r.From, r.To, null, ct);
            var pl = CultureInfo.GetCultureInfo("pl-PL");
            var sb = new StringBuilder();
            sb.AppendLine("Numer;Data;Opis;Konto;Nazwa konta;Winien;Ma");
            foreach (var e in entries)
            {
                var a = accounts[e.AccountId];
                sb.Append(string.Join(';', Csv(e.Number), e.Date.ToString("yyyy-MM-dd"), Csv(e.Description), Csv(a.Code), Csv(a.Name),
                    e.Side == DebitCredit.Debit ? e.Amount.ToString("0.00", pl) : "",
                    e.Side == DebitCredit.Credit ? e.Amount.ToString("0.00", pl) : ""));
                sb.AppendLine();
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        // Zabezpieczenie przed wstrzyknięciem formuł w Excelu (=, +, -, @) oraz cudzysłowy
        static string Csv(string s)
        {
            if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
            return '"' + s.Replace("\"", "\"\"") + '"';
        }
    }
}

/// <summary>Bilans na dzień: aktywa = zobowiązania + kapitał (+ wynik bieżący, jeśli rok nie został zamknięty).</summary>
public static class BalanceSheet
{
    public record Query(DateTime AsOf) : IRequest<Response>;
    public record Row(Guid AccountId, string Code, string Name, decimal Amount);
    public record Response(DateTime AsOf, List<Row> Assets, List<Row> Liabilities, List<Row> Equity, decimal CurrentResult,
        decimal TotalAssets, decimal TotalLiabilitiesAndEquity);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var accounts = await db.Accounts.AsNoTracking().ToListAsync(ct);
            var sums = await LedgerQueries.Sums(db, null, r.AsOf, false, null, ct);
            decimal Bal(Account a) => LedgerQueries.Natural(a, LedgerQueries.Of(sums, a.Id).Net);
            List<Row> Rows(AccountType t) => accounts.Where(a => a.Type == t).OrderBy(a => a.Code, StringComparer.Ordinal)
                .Select(a => new Row(a.Id, a.Code, a.Name, Bal(a))).Where(x => x.Amount != 0).ToList();

            var assets = Rows(AccountType.Asset);
            var liabilities = Rows(AccountType.Liability);
            var equity = Rows(AccountType.Equity);
            var result = accounts.Where(a => a.Type == AccountType.Income).Sum(Bal) - accounts.Where(a => a.Type == AccountType.Expense).Sum(Bal);
            return new Response(r.AsOf.Date, assets, liabilities, equity, result, assets.Sum(x => x.Amount),
                liabilities.Sum(x => x.Amount) + equity.Sum(x => x.Amount) + result);
        }
    }
}
