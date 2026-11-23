using LedgerCore.Application.Features.Accounts.Queries.List;
using LedgerCore.Application.Features.Contractors;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Application.Features.Reports;
using LedgerCore.Application.Features.Transactions.Queries;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

/// <summary>Dane firmy A nie mogą być widoczne ani modyfikowalne w firmie B.</summary>
public class CompanyIsolationTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    async Task<(Guid contractor, CreateInvoice.Result invoice)> SeedA()
    {
        t.Use(t.CompanyA);
        var c = await t.AddContractor("Klient A", "5260250995");
        var inv = await t.Invoice(c, net: 100m);
        await t.Pay(inv.InvoiceId, 50m);
        return (c, inv);
    }

    [Fact]
    public async Task Accounts_AreSeparatedPerCompany()
    {
        var a = await t.Db.Accounts.ToListAsync();
        t.Use(t.CompanyB);
        var b = await t.Db.Accounts.ToListAsync();
        Assert.Equal(a.Count, b.Count);
        Assert.Empty(a.Select(x => x.Id).Intersect(b.Select(x => x.Id)));
        Assert.All(a, x => Assert.Equal(t.CompanyA, x.CompanyId));
        Assert.All(b, x => Assert.Equal(t.CompanyB, x.CompanyId));
    }

    [Fact]
    public async Task Transactions_AndEntries_AreInvisibleInOtherCompany()
    {
        await SeedA();
        Assert.True(await t.Db.Transactions.AnyAsync());
        t.Use(t.CompanyB);
        Assert.Empty(await t.Db.Transactions.ToListAsync());
        Assert.Empty(await t.Db.JournalEntries.ToListAsync());
        var list = await new ListTransactions.Handler(t.Db).Handle(new(null, null, null, null), Ct);
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task Invoices_PaymentsAndLines_AreInvisibleInOtherCompany()
    {
        await SeedA();
        t.Use(t.CompanyB);
        Assert.Empty(await t.Db.Invoices.ToListAsync());
        Assert.Empty(await t.Db.InvoiceLines.ToListAsync());
        Assert.Empty(await t.Db.Payments.ToListAsync());
        var list = await new ListInvoices.Handler(t.Db).Handle(new(null, null, null, null, null, null), Ct);
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task Contractors_AreInvisibleAndNipCanRepeatInOtherCompany()
    {
        await SeedA();
        t.Use(t.CompanyB);
        Assert.Empty(await new ListContractors.Handler(t.Db).Handle(new(null, true), Ct));
        await t.AddContractor("Ten sam klient", "5260250995"); // ten sam NIP w innej firmie jest dozwolony
        Assert.Single(await t.Db.Contractors.ToListAsync());

        t.Use(t.CompanyA);
        Assert.Single(await t.Db.Contractors.ToListAsync());
    }

    [Fact]
    public async Task AuditEntriesAndPeriodLocks_AreSeparated()
    {
        await SeedA();
        Assert.True(await t.Db.AuditEntries.AnyAsync());
        t.Use(t.CompanyB);
        Assert.Empty(await t.Db.AuditEntries.ToListAsync());
        Assert.Empty(await t.Db.PeriodLocks.ToListAsync());
    }

    [Fact]
    public async Task Reports_ShowOnlyCurrentCompanyData()
    {
        await SeedA();
        t.Use(t.CompanyB);
        var tb = await new TrialBalance.Handler(t.Db).Handle(new(null, null), Ct);
        Assert.Empty(tb.Rows);
        var dash = await new Dashboard.Handler(t.Db).Handle(new(), Ct);
        Assert.Equal(0, dash.TransactionCount);
        Assert.Equal(0m, dash.Receivables);
        var accounts = await new ListAccounts.Handler(t.Db).Handle(new(), Ct);
        Assert.All(accounts, a => Assert.Equal(0m, a.Balance));
    }

    [Fact]
    public async Task CannotPayOrReadInvoiceOfAnotherCompany()
    {
        var (_, inv) = await SeedA();
        t.Use(t.CompanyB);
        await Assert.ThrowsAsync<LedgerCore.Domain.Exceptions.DomainValidationException>(() => t.Pay(inv.InvoiceId, 10m));
        await Assert.ThrowsAsync<LedgerCore.Domain.Exceptions.DomainValidationException>(
            () => new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(inv.InvoiceId), Ct));
    }

    [Fact]
    public async Task CannotInvoiceContractorOfAnotherCompany()
    {
        var (contractor, _) = await SeedA();
        t.Use(t.CompanyB);
        await Assert.ThrowsAsync<LedgerCore.Domain.Exceptions.DomainValidationException>(() => t.Invoice(contractor));
    }

    [Fact]
    public async Task NewRecordsAreStampedWithCurrentCompany()
    {
        t.Use(t.CompanyB);
        var c = await t.AddContractor("B");
        var inv = await t.Invoice(c);
        var tx = await t.Db.Transactions.SingleAsync(x => x.Id == inv.TransactionId);
        Assert.Equal(t.CompanyB, tx.CompanyId);
        Assert.All(await t.Db.JournalEntries.ToListAsync(), e => Assert.Equal(t.CompanyB, e.CompanyId));
    }

    [Fact]
    public async Task SavingWithoutCompanySelected_Throws()
    {
        t.Use(Guid.Empty);
        t.Db.Contractors.Add(new Contractor("X", null, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task EveryCompanyOwnedEntity_HasQueryFilter()
    {
        var owned = t.Db.Model.GetEntityTypes().Where(e => typeof(ICompanyOwned).IsAssignableFrom(e.ClrType)).ToList();
        Assert.NotEmpty(owned);
        Assert.All(owned, e => Assert.NotEmpty(e.GetDeclaredQueryFilters()));
        await Task.CompletedTask;
    }
}
