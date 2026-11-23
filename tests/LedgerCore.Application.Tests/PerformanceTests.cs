using System.Diagnostics;
using LedgerCore.Application.Features.Accounts.Queries.List;
using LedgerCore.Application.Features.Reports;
using Xunit.Abstractions;

namespace LedgerCore.Application.Tests;

/// <summary>
/// Pomiar na 200 000 zapisów (5 lat, 50 kont). Limity z T-017; na CI (zmienna CI) są 3× luźniejsze,
/// bo współdzielone maszyny bywają wolne. Czasy są wypisywane w wyniku testu.
/// </summary>
[Trait("Category", "Performance")]
public class PerformanceTests : IClassFixture<PerformanceTests.Fixture>
{
    public class Fixture : IDisposable
    {
        public PerfData Data { get; } = new();
        public void Dispose() => Data.Dispose();
    }

    readonly PerfData d;
    readonly ITestOutputHelper output;
    static readonly double Factor = Environment.GetEnvironmentVariable("CI") is null ? 1 : 3;

    public PerformanceTests(Fixture fx, ITestOutputHelper output) { d = fx.Data; this.output = output; }

    async Task Measure(string name, double limitMs, Func<LedgerCore.Infrastructure.Persistence.Context.LedgerDbContext, Task> action)
    {
        using var db = d.NewContext();
        await action(db); // rozgrzewka (JIT, cache SQLite)
        var best = double.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            await action(db);
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        output.WriteLine($"{name}: {best:0} ms (limit {limitMs * Factor:0} ms)");
        Assert.True(best < limitMs * Factor, $"{name}: {best:0} ms > {limitMs * Factor:0} ms");
    }

    static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public Task Dashboard_under_1s() =>
        Measure("Pulpit", 1000, db => new Dashboard.Handler(db).Handle(new(), Ct));

    [Fact]
    public Task TrialBalance_year_under_1s() =>
        Measure("Zestawienie obrotów (rok)", 1000, db =>
            new TrialBalance.Handler(db).Handle(new(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)), Ct));

    [Fact]
    public Task AccountLedger_page_under_300ms() =>
        Measure("Karta konta (strona 50)", 300, db =>
            new AccountLedger.Handler(db).Handle(new(d.BusyAccountId, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), 1, 50), Ct));

    [Fact]
    public Task ProfitAndLoss_and_BalanceSheet_under_1s() =>
        Measure("Rachunek wyników + bilans", 1000, async db =>
        {
            await new ProfitAndLoss.Handler(db).Handle(new(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)), Ct);
            await new BalanceSheet.Handler(db).Handle(new(new DateTime(2024, 12, 31)), Ct);
        });

    [Fact]
    public Task ListAccounts_under_1s() =>
        Measure("Lista kont z saldami", 1000, db => new ListAccounts.Handler(db).Handle(new(), Ct));
}
