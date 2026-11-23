using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Accounts.Queries.List;
using LedgerCore.Application.Features.Administration;
using LedgerCore.Application.Features.Reports;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

/// <summary>
/// Raporty liczone w SQL (T-017) muszą dawać wyniki identyczne co do grosza z referencyjnym liczeniem w pamięci
/// na decimal (`LedgerQueries.LoadEntries`), także z księgowaniem zamykającym rok i danymi drugiej firmy.
/// </summary>
public class AggregationEquivalenceTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    async Task Seed()
    {
        var rnd = new Random(7);
        async Task Random(int n, DateTime start, int days, string prefix)
        {
            var accounts = await t.Db.Accounts.AsNoTracking().ToListAsync(); // konta bieżącej firmy
            for (var i = 0; i < n; i++)
            {
                var d = start.AddDays(rnd.Next(days));
                var number = await LedgerCore.Application.Features.Transactions.Commands.Create.TransactionNumbers.Next(t.Db, d, Ct);
                var tx = new Transaction($"{prefix} {i}", d, number);
                var amount = rnd.Next(1, 500_000) / 100m + rnd.Next(0, 3) * 0.01m;
                var a = accounts[rnd.Next(accounts.Count)].Id; var b = accounts[rnd.Next(accounts.Count)].Id;
                tx.AddEntry(a, amount, DebitCredit.Debit);
                tx.AddEntry(b, amount - 0.01m > 0 ? amount - 0.01m : amount, DebitCredit.Credit);
                if (amount - 0.01m > 0) tx.AddEntry(accounts[rnd.Next(accounts.Count)].Id, 0.01m, DebitCredit.Credit); // zapis 3-wierszowy
                tx.Validate();
                t.Db.Transactions.Add(tx);
                await t.Db.SaveChangesAsync(); // numer kolejnego dokumentu liczony z bazy – zapis po każdym
            }
            await t.Db.SaveChangesAsync();
        }
        await Random(250, new DateTime(2024, 1, 1), 366, "R24");
        await new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2024), Ct);
        await Random(150, new DateTime(2025, 1, 1), 300, "R25");
        t.Use(t.CompanyB);                    // dane drugiej firmy nie mogą wpływać na wynik
        await Random(40, new DateTime(2025, 1, 1), 300, "B");
        t.Use(t.CompanyA);
    }

    // --- referencja: stare liczenie w pamięci na decimal ---
    async Task<Dictionary<Guid, decimal>> RefNet(DateTime? from, DateTime? to, bool excludeClosing)
    {
        var rows = await LedgerQueries.LoadEntries(t.Db, from, to, null, Ct);
        return rows.Where(r => !excludeClosing || !r.Number.StartsWith("ZR/")).GroupBy(r => r.AccountId)
            .ToDictionary(g => g.Key, g => LedgerQueries.Net(g));
    }

    [Fact]
    public async Task Amount_and_AmountCents_stay_consistent()
    {
        await Seed();
        var all = await t.Db.JournalEntries.AsNoTracking().ToListAsync();
        Assert.NotEmpty(all);
        Assert.All(all, e => Assert.Equal(e.Amount, e.AmountCents / 100m));
        Assert.Contains(all, e => e.IsClosing);
        Assert.All(all, e => Assert.NotEqual(default, e.Date));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2024-03-01", "2024-09-30")]
    [InlineData("2025-01-01", null)]
    [InlineData(null, "2024-12-31")]
    public async Task TrialBalance_matches_reference(string? from, string? to)
    {
        await Seed();
        var f = from is null ? (DateTime?)null : DateTime.Parse(from); var e = to is null ? (DateTime?)null : DateTime.Parse(to);
        var result = await new TrialBalance.Handler(t.Db).Handle(new(f, e), Ct);

        var before = f.HasValue ? await RefNet(null, f.Value.AddDays(-1), false) : [];
        var period = await RefRows(f, e);
        foreach (var row in result.Rows)
        {
            var opening = before.GetValueOrDefault(row.AccountId);
            var p = period.Where(x => x.AccountId == row.AccountId).ToList();
            var dr = p.Where(x => x.Side == DebitCredit.Debit).Sum(x => x.Amount);
            var cr = p.Where(x => x.Side == DebitCredit.Credit).Sum(x => x.Amount);
            Assert.Equal(opening, row.Opening); Assert.Equal(dr, row.DebitTurnover); Assert.Equal(cr, row.CreditTurnover);
            Assert.Equal(opening + dr - cr, row.Closing);
        }
        var expectedRows = (await t.Db.Accounts.AsNoTracking().ToListAsync()).Count(a =>
            before.GetValueOrDefault(a.Id) != 0 || period.Any(x => x.AccountId == a.Id));
        Assert.Equal(expectedRows, result.Rows.Count);
        Assert.Equal(result.TotalDebitTurnover, result.TotalCreditTurnover);
    }

    async Task<List<EntryRow>> RefRows(DateTime? from, DateTime? to) =>
        (await LedgerQueries.LoadEntries(t.Db, from, to, null, Ct)).Where(x => from.HasValue ? x.Date >= from.Value.Date : true).ToList();

    [Fact]
    public async Task ProfitAndLoss_and_BalanceSheet_match_reference()
    {
        await Seed();
        var accounts = await t.Db.Accounts.AsNoTracking().ToListAsync();
        var from = new DateTime(2024, 2, 1); var to = new DateTime(2025, 6, 30);

        var pl = await new ProfitAndLoss.Handler(t.Db).Handle(new(from, to), Ct);
        var net = await RefNet(from, to, true);
        decimal Nat(Account a, Dictionary<Guid, decimal> n) => LedgerQueries.Natural(a, n.GetValueOrDefault(a.Id));
        Assert.Equal(accounts.Where(a => a.Type == AccountType.Income).Sum(a => Nat(a, net)), pl.TotalIncome);
        Assert.Equal(accounts.Where(a => a.Type == AccountType.Expense).Sum(a => Nat(a, net)), pl.TotalExpenses);

        var bs = await new BalanceSheet.Handler(t.Db).Handle(new(to), Ct);
        var all = await RefNet(null, to, false);
        Assert.Equal(accounts.Where(a => a.Type == AccountType.Asset).Sum(a => Nat(a, all)), bs.TotalAssets);
        Assert.Equal(bs.TotalAssets, bs.TotalLiabilitiesAndEquity);
    }

    [Fact]
    public async Task ListAccounts_matches_reference()
    {
        await Seed();
        var list = await new ListAccounts.Handler(t.Db).Handle(new(true), Ct);
        var rows = await LedgerQueries.LoadEntries(t.Db, null, null, null, Ct);
        foreach (var a in list)
        {
            var mine = rows.Where(r => r.AccountId == a.Id).ToList();
            Assert.Equal(mine.Where(x => x.Side == DebitCredit.Debit).Sum(x => x.Amount), a.DebitTotal);
            Assert.Equal(mine.Where(x => x.Side == DebitCredit.Credit).Sum(x => x.Amount), a.CreditTotal);
        }
    }

    [Fact]
    public async Task Dashboard_matches_reference()
    {
        await Seed();
        var d = await new Dashboard.Handler(t.Db).Handle(new(), Ct);
        var accounts = await t.Db.Accounts.AsNoTracking().ToListAsync();
        var all = await RefNet(null, null, false);
        decimal Nat(Account a) => LedgerQueries.Natural(a, all.GetValueOrDefault(a.Id));
        Assert.Equal(accounts.Where(a => a.Type == AccountType.Asset).Sum(Nat), d.Assets);
        Assert.Equal(accounts.Where(a => a.Type == AccountType.Liability).Sum(Nat), d.Liabilities);
        Assert.Equal(accounts.Where(a => a.Type == AccountType.Equity).Sum(Nat), d.Equity);
        Assert.Equal(12, d.Months.Count);
        Assert.Equal(await t.Db.Transactions.CountAsync(), d.TransactionCount);
    }

    [Fact]
    public async Task AccountLedger_pages_concatenate_to_full_ledger_with_identical_balances()
    {
        await Seed();
        var busiest = (await t.Db.JournalEntries.AsNoTracking().GroupBy(e => e.AccountId).Select(g => new { g.Key, N = g.Count() })
            .ToListAsync()).OrderByDescending(x => x.N).First().Key;
        var from = new DateTime(2024, 3, 1);
        var h = new AccountLedger.Handler(t.Db);

        var full = await h.Handle(new(busiest, from, null), Ct);
        Assert.True(full.Lines.Count > 20);
        var paged = new List<AccountLedger.Line>();
        for (var p = 1; p <= (int)Math.Ceiling(full.Lines.Count / 7.0); p++)
        {
            var page = await h.Handle(new(busiest, from, null, p, 7), Ct);
            Assert.Equal(full.Lines.Count, page.TotalCount);
            Assert.Equal(full.ClosingBalance, page.ClosingBalance);   // salda i sumy dotyczą całego okresu
            Assert.Equal(full.TotalDebit, page.TotalDebit);
            paged.AddRange(page.Lines);
        }
        Assert.Equal(full.Lines.Select(l => (l.TransactionId, l.Balance)), paged.Select(l => (l.TransactionId, l.Balance)));
        Assert.Equal(full.Lines.Last().Balance, full.ClosingBalance);

        // referencja: saldo narastające liczone z LoadEntries
        var account = await t.Db.Accounts.AsNoTracking().FirstAsync(a => a.Id == busiest);
        var rows = await LedgerQueries.LoadEntries(t.Db, from, null, busiest, Ct);
        var opening = LedgerQueries.Natural(account, LedgerQueries.Net(await LedgerQueries.LoadEntries(t.Db, null, from.AddDays(-1), busiest, Ct)));
        Assert.Equal(opening, full.OpeningBalance);
        Assert.Equal(opening + LedgerQueries.Natural(account, LedgerQueries.Net(rows)), full.ClosingBalance);
        Assert.Equal(rows.Select(r => r.TransactionId), full.Lines.Select(l => l.TransactionId));
    }

    [Fact]
    public async Task Sums_are_isolated_per_company()
    {
        await Seed();
        var a = await LedgerQueries.Sums(t.Db, null, null, false, null, Ct);
        t.Use(t.CompanyB);
        var b = await LedgerQueries.Sums(t.Db, null, null, false, null, Ct);
        Assert.Empty(a.Keys.Intersect(b.Keys));
        Assert.True(b.Count > 0);
    }

    [Fact]
    public async Task Aggregate_queries_use_covering_indexes()
    {
        await Seed();
        var conn = t.Db.Database.GetDbConnection();
        string Plan(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
            using var r = cmd.ExecuteReader();
            var lines = new List<string>();
            while (r.Read()) lines.Add(r.GetString(3));
            return string.Join(" | ", lines);
        }
        var range = Plan("SELECT AccountId, Side, SUM(AmountCents) FROM journalentries WHERE CompanyId = 'x' AND Date >= '2024-01-01' AND Date <= '2024-12-31' GROUP BY AccountId, Side");
        Assert.Contains("COVERING INDEX IX_journalentries_Company_Date_Account", range);
        var card = Plan("SELECT Side, SUM(AmountCents) FROM journalentries WHERE CompanyId = 'x' AND AccountId = 'y' AND Date >= '2024-01-01' GROUP BY Side");
        Assert.Contains("COVERING INDEX IX_journalentries_Company_Account_Date", card);
    }
}
