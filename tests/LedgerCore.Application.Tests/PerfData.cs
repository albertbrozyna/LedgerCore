using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Infrastructure.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Tests;

/// <summary>
/// Generator dużej bazy do testów wydajności: plik SQLite w katalogu tymczasowym, 1 firma, ~50 kont,
/// <paramref name="transactions"/> księgowań po 2 zapisy (200 000 zapisów dla 100 000), 5 lat.
/// Wstawia dane surowym SQL (szybko), w tym kolumny pomocnicze, jeśli schemat je ma.
/// </summary>
public sealed class PerfData : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lc-perf-{Guid.NewGuid():N}.db");
    public CompanyContext Company { get; } = new();
    public Guid CompanyId { get; } = Guid.NewGuid();
    public List<Guid> AccountIds { get; } = [];
    public Guid BusyAccountId { get; private set; }
    public int EntryCount { get; }
    public int FirstYear { get; } = 2021;
    public int Years { get; } = 5;

    public PerfData(int transactions = 100_000)
    {
        EntryCount = transactions * 2;
        using (var db = NewContext())
        {
            db.Database.EnsureCreated();
            db.Companies.Add(new Company("Perf"));
            db.SaveChanges();
        }
        // EF nadaje firmie losowy Id – pobierz go i użyj jako bieżącej
        using (var db = NewContext())
            CompanyId = db.Companies.AsNoTracking().Select(c => c.Id).Single();
        Company.CompanyId = CompanyId;
        using (var db = NewContext())
        {
            var types = new[] { AccountType.Asset, AccountType.Liability, AccountType.Equity, AccountType.Income, AccountType.Expense };
            for (var i = 0; i < 50; i++) db.Accounts.Add(new Account($"Konto {i}", (100 + i * 7).ToString(), types[i % 5]));
            db.SaveChanges();
            AccountIds.AddRange(db.Accounts.AsNoTracking().OrderBy(a => a.Code).Select(a => a.Id).ToList());
        }
        BusyAccountId = AccountIds[0];
        Insert(transactions);
    }

    public LedgerDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite($"Data Source={Path}").Options, Company);

    static string G(Guid g) => g.ToString().ToUpperInvariant();

    void Insert(int transactions)
    {
        using var conn = new SqliteConnection($"Data Source={Path}");
        conn.Open();
        var cols = new HashSet<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(journalentries)";
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(1));
        }
        var hasCents = cols.Contains("AmountCents");

        using var tx = conn.BeginTransaction();
        using var insT = conn.CreateCommand();
        insT.CommandText = "INSERT INTO transactions (Id, CompanyId, CreatedAt, CreatedByUserId, Date, Description, Number, ReversesTransactionId) VALUES ($id,$c,$d,NULL,$d,$desc,$n,NULL)";
        foreach (var p in new[] { "$id", "$c", "$d", "$desc", "$n" }) insT.Parameters.Add(new SqliteParameter(p, null));
        using var insE = conn.CreateCommand();
        insE.CommandText = hasCents
            ? "INSERT INTO journalentries (Id, AccountId, Amount, AmountCents, Date, IsClosing, CompanyId, CreatedAt, Side, TransactionId) VALUES ($id,$a,$amt,$cents,$d,0,$c,$d,$s,$t)"
            : "INSERT INTO journalentries (Id, AccountId, Amount, CompanyId, CreatedAt, Side, TransactionId) VALUES ($id,$a,$amt,$c,$d,$s,$t)";
        foreach (var p in new[] { "$id", "$a", "$amt", "$c", "$d", "$s", "$t" }) insE.Parameters.Add(new SqliteParameter(p, null));
        if (hasCents) insE.Parameters.Add(new SqliteParameter("$cents", null));

        var rnd = new Random(12345);
        var start = new DateTime(FirstYear, 1, 1);
        var days = 365 * Years;
        var seq = new Dictionary<int, int>();
        for (var i = 0; i < transactions; i++)
        {
            var date = start.AddDays(rnd.Next(days));
            var n = seq[date.Year] = seq.GetValueOrDefault(date.Year) + 1;
            var id = Guid.NewGuid();
            var ds = date.ToString("yyyy-MM-dd HH:mm:ss");
            insT.Parameters["$id"].Value = G(id); insT.Parameters["$c"].Value = G(CompanyId); insT.Parameters["$d"].Value = ds;
            insT.Parameters["$desc"].Value = $"Księgowanie {i}"; insT.Parameters["$n"].Value = $"DK/{date.Year}/{n:00000}";
            insT.ExecuteNonQuery();

            // 20% zapisów na jedno „ruchliwe” konto (karta konta ma wiele wierszy)
            var debit = rnd.Next(10) < 2 ? BusyAccountId : AccountIds[rnd.Next(AccountIds.Count)];
            var credit = AccountIds[rnd.Next(AccountIds.Count)];
            var cents = rnd.Next(1, 1_000_000);
            var amount = (cents / 100m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var (acc, side) in new[] { (debit, 0), (credit, 1) })
            {
                insE.Parameters["$id"].Value = G(Guid.NewGuid()); insE.Parameters["$a"].Value = G(acc); insE.Parameters["$amt"].Value = amount;
                insE.Parameters["$c"].Value = G(CompanyId); insE.Parameters["$d"].Value = ds; insE.Parameters["$s"].Value = side;
                insE.Parameters["$t"].Value = G(id);
                if (hasCents) insE.Parameters["$cents"].Value = (long)cents;
                insE.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { Path, Path + "-wal", Path + "-shm", Path + "-journal" })
            try { File.Delete(f); } catch { /* plik może być jeszcze zajęty – to katalog tymczasowy */ }
    }
}
