using LedgerCore.Application.Features.Accounts.Commands.Create;
using LedgerCore.Application.Features.Administration;
using LedgerCore.Application.Features.Reports;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class LedgerAndReportsTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    // ---- CreateTransaction ----

    [Fact]
    public async Task CreateTransaction_NumbersAreSequentialPerYear()
    {
        Assert.Equal("DK/2025/0001", (await t.Post("A", "100", "800", 10m)).Number);
        Assert.Equal("DK/2025/0002", (await t.Post("B", "100", "800", 10m)).Number);
        Assert.Equal("DK/2026/0001", (await t.Post("C", "100", "800", 10m, new DateTime(2026, 1, 2))).Number);
    }

    [Fact]
    public async Task CreateTransaction_RejectsUnbalancedAndInactiveAndUnknownAccount()
    {
        var h = new CreateTransaction.Handler(t.Db, t.Me, t.Audit);
        var cash = (await t.Acc("100")).Id;
        var cap = (await t.Acc("800")).Id;

        await Assert.ThrowsAsync<UnbalancedTransactionException>(() => h.Handle(new("X", Day,
            [new(cash, 10m, DebitCredit.Debit), new(cap, 9m, DebitCredit.Credit)]), Ct));

        await Assert.ThrowsAsync<AccountNotFoundException>(() => h.Handle(new("X", Day,
            [new(cash, 10m, DebitCredit.Debit), new(Guid.NewGuid(), 10m, DebitCredit.Credit)]), Ct));

        (await t.Db.Accounts.SingleAsync(a => a.Code == "800")).SetActive(false);
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new("X", Day,
            [new(cash, 10m, DebitCredit.Debit), new(cap, 10m, DebitCredit.Credit)]), Ct));
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public void CreateTransaction_Validator()
    {
        var v = new CreateTransaction.Validator();
        var id = Guid.NewGuid();
        Assert.False(v.Validate(new CreateTransaction.Command("", Day, [new CreateTransaction.NewEntryDto(id, 1m, DebitCredit.Debit), new CreateTransaction.NewEntryDto(id, 1m, DebitCredit.Credit)])).IsValid);
        Assert.False(v.Validate(new CreateTransaction.Command("X", Day, [new CreateTransaction.NewEntryDto(id, 1m, DebitCredit.Debit)])).IsValid);
        Assert.False(v.Validate(new CreateTransaction.Command("X", Day, [new CreateTransaction.NewEntryDto(id, 0m, DebitCredit.Debit), new CreateTransaction.NewEntryDto(id, 1m, DebitCredit.Credit)])).IsValid);
        Assert.True(v.Validate(new CreateTransaction.Command("X", Day, [new CreateTransaction.NewEntryDto(id, 1m, DebitCredit.Debit), new CreateTransaction.NewEntryDto(id, 1m, DebitCredit.Credit)])).IsValid);
    }

    [Fact]
    public async Task CreateAccount_RejectsDuplicateCodeWithinCompanyButAllowsInAnother()
    {
        var h = new CreateAccount.CreateAccountHandler(t.Db, t.Audit);
        await h.Handle(new CreateAccount.Command("Własne", "999", AccountType.Asset), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new CreateAccount.Command("Inne", "999", AccountType.Asset), Ct));

        t.Use(t.CompanyB);
        await h.Handle(new CreateAccount.Command("Własne", "999", AccountType.Asset), Ct); // inna firma – brak konfliktu
    }

    // ---- raporty ----

    async Task SeedYear()
    {
        var c = await t.AddContractor();
        var sale = await t.Invoice(c, net: 1000m);                                   // przychód 1000, VAT 230
        await t.Pay(sale.InvoiceId, 1230m);
        await t.Invoice(c, InvoiceType.Purchase, 300m, number: "ZAK/1");             // koszt 300, VAT 69
        await t.Post("Kapitał", "130", "800", 5000m, new DateTime(2025, 1, 2));
    }

    [Fact]
    public async Task TrialBalance_TotalsAreEqual()
    {
        await SeedYear();
        var r = await new TrialBalance.Handler(t.Db).Handle(new(null, null), Ct);
        Assert.Equal(r.TotalDebitTurnover, r.TotalCreditTurnover);
        Assert.Equal(r.Rows.Sum(x => x.Closing), 0m); // suma sald Winien − Ma wynosi zero
    }

    [Fact]
    public async Task TrialBalance_OpeningBalanceCoversEntriesBeforeFrom()
    {
        await SeedYear();
        var r = await new TrialBalance.Handler(t.Db).Handle(new(new DateTime(2025, 2, 1), null), Ct);
        var bank = r.Rows.Single(x => x.Code == "130");
        Assert.Equal(5000m, bank.Opening);       // kapitał z 2 stycznia
        Assert.Equal(1230m, bank.DebitTurnover); // wpłata z marca
        Assert.Equal(6230m, bank.Closing);
    }

    [Fact]
    public async Task ProfitAndLoss_ComputesResult()
    {
        await SeedYear();
        var r = await new ProfitAndLoss.Handler(t.Db).Handle(new(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), Ct);
        Assert.Equal(1000m, r.TotalIncome);
        Assert.Equal(300m, r.TotalExpenses);
        Assert.Equal(700m, r.Result);
    }

    [Fact]
    public async Task ProfitAndLoss_RespectsDateRange()
    {
        await SeedYear();
        var r = await new ProfitAndLoss.Handler(t.Db).Handle(new(new DateTime(2025, 4, 1), new DateTime(2025, 12, 31)), Ct);
        Assert.Equal(0m, r.Result);
    }

    [Fact]
    public async Task BalanceSheet_IsBalancedWithCurrentResult()
    {
        await SeedYear();
        var r = await new BalanceSheet.Handler(t.Db).Handle(new(new DateTime(2025, 12, 31)), Ct);
        Assert.Equal(700m, r.CurrentResult);
        Assert.Equal(r.TotalAssets, r.TotalLiabilitiesAndEquity);
    }

    [Fact]
    public async Task BalanceSheet_AsOfDateExcludesLaterEntries()
    {
        await SeedYear();
        var r = await new BalanceSheet.Handler(t.Db).Handle(new(new DateTime(2025, 1, 31)), Ct);
        Assert.Equal(5000m, r.TotalAssets);
        Assert.Equal(r.TotalAssets, r.TotalLiabilitiesAndEquity);
    }

    [Fact]
    public async Task AccountLedger_RunningBalanceUsesNaturalSign()
    {
        await SeedYear();
        var bank = await t.Acc("130");
        var r = await new AccountLedger.Handler(t.Db).Handle(new(bank.Id, null, null), Ct);
        Assert.Equal(2, r.Lines.Count);
        Assert.Equal(6230m, r.ClosingBalance);

        var capital = await t.Acc("800"); // pasywa – saldo po stronie Ma jest dodatnie
        var cap = await new AccountLedger.Handler(t.Db).Handle(new(capital.Id, null, null), Ct);
        Assert.Equal(5000m, cap.ClosingBalance);
    }

    [Fact]
    public async Task AccountLedger_UnknownAccountThrows()
    {
        await Assert.ThrowsAsync<AccountNotFoundException>(() => new AccountLedger.Handler(t.Db).Handle(new(Guid.NewGuid(), null, null), Ct));
    }

    [Fact]
    public async Task ExportJournal_EscapesFormulaInjection()
    {
        await t.Post("=HYPERLINK(\"x\")", "100", "800", 10m);
        var bytes = await new ExportJournal.Handler(t.Db).Handle(new(null, null), Ct);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"'=HYPERLINK", text);
        Assert.Contains("10,00", text);
    }

    // ---- zamknięcie roku ----

    [Fact]
    public async Task CloseYear_ZeroesResultAccountsAndMovesResultTo860()
    {
        await SeedYear();
        var r = await new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct);

        Assert.Equal("ZR/2025/0001", r.Number);
        Assert.Equal(700m, r.Result);
        Assert.Equal(0m, await t.Net("700"));
        Assert.Equal(0m, await t.Net("402"));
        Assert.Equal(-700m, await t.Net("860"));
        Assert.Equal(new DateTime(2025, 12, 31), (await t.Db.PeriodLocks.SingleAsync()).LockedThrough);
    }

    [Fact]
    public async Task CloseYear_KeepsReportsAndBalanceConsistent()
    {
        await SeedYear();
        await new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct);

        var pl = await new ProfitAndLoss.Handler(t.Db).Handle(new(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), Ct);
        Assert.Equal(700m, pl.Result); // księgowanie ZR/ jest pomijane w rachunku wyników

        var bs = await new BalanceSheet.Handler(t.Db).Handle(new(new DateTime(2025, 12, 31)), Ct);
        Assert.Equal(0m, bs.CurrentResult);
        Assert.Equal(bs.TotalAssets, bs.TotalLiabilitiesAndEquity);
    }

    [Fact]
    public async Task CloseYear_LossGoesToDebitOf860()
    {
        var c = await t.AddContractor();
        await t.Invoice(c, InvoiceType.Purchase, 100m, number: "Z1");
        var r = await new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct);
        Assert.Equal(-100m, r.Result);
        Assert.Equal(100m, await t.Net("860"));
    }

    [Fact]
    public async Task CloseYear_RejectsSecondCloseNothingToCloseAndBadYear()
    {
        var h = new CloseYear.Handler(t.Db, t.Me, t.Audit);
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(2025), Ct)); // brak obrotów
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(1999), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(DateTime.Today.Year + 1), Ct));

        await SeedYear();
        await h.Handle(new(2025), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(2025), Ct));
    }

    [Fact]
    public async Task CloseYear_BlocksPostingIntoClosedPeriodButNotNextYear()
    {
        await SeedYear();
        await new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Post("Spóźnione", "100", "800", 1m, new DateTime(2025, 12, 31)));
        await t.Post("Nowy rok", "100", "800", 1m, new DateTime(2026, 1, 1));
    }

    [Fact]
    public async Task CloseYear_IsPerCompany()
    {
        await SeedYear();
        t.Use(t.CompanyB);
        var c = await t.AddContractor("Klient B");
        await t.Invoice(c, net: 10m);
        await new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct);

        t.Use(t.CompanyA);
        Assert.Equal(1000m, -await t.Net("700")); // firma A nie została zamknięta
        Assert.Empty(await t.Db.PeriodLocks.ToListAsync());
    }
}
