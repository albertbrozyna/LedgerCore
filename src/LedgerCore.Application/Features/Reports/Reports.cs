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

/// <summary>Zestawienie obrotów i ssald (bilans próbny) za okres.</summary>
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
            var entries = await LedgerQueries.LoadEntries(db, null, r.To, null, ct);
            var from = r.From?.Date;

            var rows = accounts.OrderBy(a => a.Code, StringComparer.Ordinal).Select(a =>
            {
                var mine = entries.Where(e => e.AccountId == a.Id).ToList();
                var before = from.HasValue ? mine.Where(e => e.Date < from.Value).ToList() : new List<EntryRow>();
                var period = from.HasValue ? mine.Where(e => e.Date >= from.Value).ToList() : mine;
                var dr = period.Where(e => e.Side == DebitCredit.Debit).Sum(e => e.Amount);
                var cr = period.Where(e => e.Side == DebitCredit.Credit).Sum(e => e.Amount);
                var opening = LedgerQueries.Net(before);
                return new Row(a.Id, a.Code, a.Name, a.Type, opening, dr, cr, opening + dr - cr);
            }).Where(x => x.Opening != 0 || x.DebitTurnover != 0 || x.CreditTurnover != 0 || x.Closing != 0).ToList();

            return new Response(rows, rows.Sum(x => x.DebitTurnover), rows.Sum(x => x.CreditTurnover));
        }
    }
}

/// <summary>Karta konta (zapisy z saldem narastającym).</summary>
public static class AccountLedger
{
    public record Query(Guid AccountId, DateTime? From, DateTime? To) : IRequest<Response>;

    public record Line(Guid TransactionId, string Number, DateTime Date, string Description,
        decimal Debit, decimal Credit, decimal Balance);

    /// <summary>Salda w znaku naturalnym konta.</summary>
    public record Response(Guid AccountId, string Code, string Name, AccountType Type,
        decimal OpeningBalance, List<Line> Lines, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var a = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.AccountId, ct)
                    ?? throw new AccountNotFoundException(r.AccountId);
            var all = await LedgerQueries.LoadEntries(db, null, r.To, a.Id, ct);
            var from = r.From?.Date;

            var opening = from.HasValue ? LedgerQueries.Natural(a, LedgerQueries.Net(all.Where(e => e.Date < from.Value))) : 0m;
            var running = opening;
            var lines = new List<Line>();
            foreach (var e in all.Where(e => !from.HasValue || e.Date >= from.Value))
            {
                var signed = e.Side == DebitCredit.Debit ? e.Amount : -e.Amount;
                running += a.IsDebitNormal ? signed : -signed;
                lines.Add(new Line(e.TransactionId, e.Number, e.Date, e.Description,
                    e.Side == DebitCredit.Debit ? e.Amount : 0, e.Side == DebitCredit.Credit ? e.Amount : 0, running));
            }
            return new Response(a.Id, a.Code, a.Name, a.Type, opening, lines,
                lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), running);
        }
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
            var entries = (await LedgerQueries.LoadEntries(db, r.From, r.To, null, ct)).Where(e => !e.Number.StartsWith("ZR/")).ToLookup(e => e.AccountId);

            List<Row> Build(AccountType t) => accounts.Where(a => a.Type == t).OrderBy(a => a.Code, StringComparer.Ordinal)
                .Select(a => new Row(a.Id, a.Code, a.Name, LedgerQueries.Natural(a, LedgerQueries.Net(entries[a.Id]))))
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
            var entries = await LedgerQueries.LoadEntries(db, null, null, null, ct);
            var byAccount = entries.ToLookup(e => e.AccountId);
            decimal Sum(AccountType t, DateTime? from = null) => accounts.Where(a => a.Type == t).Sum(a =>
                LedgerQueries.Natural(a, LedgerQueries.Net(byAccount[a.Id].Where(e => !from.HasValue || (e.Date >= from.Value && !e.Number.StartsWith("ZR/"))))));

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var income = Sum(AccountType.Income, monthStart);
            var expenses = Sum(AccountType.Expense, monthStart);

            var recent = await db.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
                .Take(5).Select(t => new { t.Id, t.Number, t.Date, t.Description, Debits = t.Entries.Where(e => e.Side == DebitCredit.Debit).Select(e => e.Amount).ToList() })
                .ToListAsync(ct);

            var open = (await db.Invoices.AsNoTracking().Where(i => !i.IsCanceled).ToListAsync(ct)).Where(i => i.Remaining > 0).ToList();
            decimal Rem(InvoiceType t, bool overdueOnly) => open.Where(i => i.Type == t && (!overdueOnly || i.DueDate < today)).Sum(i => i.Remaining);

            // Ostatnie 12 miesięcy (bez księgowań zamykających rok)
            var incomeIds = accounts.Where(a => a.Type == AccountType.Income).Select(a => a.Id).ToHashSet();
            var expenseIds = accounts.Where(a => a.Type == AccountType.Expense).Select(a => a.Id).ToHashSet();
            var live = entries.Where(e => !e.Number.StartsWith("ZR/")).ToList();
            var months = Enumerable.Range(0, 12).Select(i => monthStart.AddMonths(i - 11)).Select(m =>
            {
                var inMonth = live.Where(e => e.Date.Year == m.Year && e.Date.Month == m.Month).ToList();
                var inc = LedgerQueries.Net(inMonth.Where(e => incomeIds.Contains(e.AccountId)));
                var exp = LedgerQueries.Net(inMonth.Where(e => expenseIds.Contains(e.AccountId)));
                return new MonthPoint(m.ToString("yyyy-MM"), -inc, exp);
            }).ToList();

            return new Response(Sum(AccountType.Asset), Sum(AccountType.Liability), Sum(AccountType.Equity),
                income, expenses, income - expenses, accounts.Count, await db.Transactions.CountAsync(ct),
                recent.Select(t => new RecentTx(t.Id, t.Number, t.Date, t.Description, t.Debits.Sum())).ToList(),
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
            var entries = (await LedgerQueries.LoadEntries(db, null, r.AsOf, null, ct)).ToLookup(e => e.AccountId);
            decimal Bal(Account a) => LedgerQueries.Natural(a, LedgerQueries.Net(entries[a.Id]));
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
