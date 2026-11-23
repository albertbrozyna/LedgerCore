using FluentValidation;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Application.Features.Transactions.Commands.Reverse;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class InvoicingTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    [Fact]
    public async Task CreateInvoice_Sales_BooksReceivableRevenueAndVat()
    {
        var c = await t.AddContractor();
        var r = await t.Invoice(c, net: 100m); // brutto 123

        Assert.Equal("FV/2025/0001", r.Number);
        Assert.Equal(123m, await t.Net("201"));
        Assert.Equal(-100m, await t.Net("700"));
        Assert.Equal(-23m, await t.Net("220"));
        var tx = await t.Db.Transactions.Include(x => x.Entries).SingleAsync(x => x.Id == r.TransactionId);
        Assert.Equal(tx.TotalDebit, tx.TotalCredit);
    }

    [Fact]
    public async Task CreateInvoice_Sales_NumbersAreSequentialPerYear()
    {
        var c = await t.AddContractor();
        Assert.Equal("FV/2025/0001", (await t.Invoice(c)).Number);
        Assert.Equal("FV/2025/0002", (await t.Invoice(c)).Number);
        Assert.Equal("FV/2026/0001", (await t.Invoice(c, issue: new DateTime(2026, 1, 5))).Number);
    }

    [Fact]
    public async Task CreateInvoice_Purchase_BooksCostVatAndPayable()
    {
        var c = await t.AddContractor();
        await t.Invoice(c, InvoiceType.Purchase, 200m, VatRate.R23, "ZAK/7"); // brutto 246
        Assert.Equal(200m, await t.Net("402"));
        Assert.Equal(46m, await t.Net("220"));
        Assert.Equal(-246m, await t.Net("202"));
    }

    [Fact]
    public async Task CreateInvoice_ZeroVat_DoesNotNeedVatAccount()
    {
        var c = await t.AddContractor();
        await t.Invoice(c, net: 50m, rate: VatRate.R0);
        Assert.Equal(50m, await t.Net("201"));
        Assert.Equal(0m, await t.Net("220"));
    }

    [Fact]
    public async Task CreateInvoice_Purchase_RequiresNumberAndRejectsDuplicates()
    {
        var c = await t.AddContractor();
        var handler = new CreateInvoice.Handler(t.Db, t.Me, t.Audit);
        var noNumber = new CreateInvoice.Command(InvoiceType.Purchase, " ", c, Day, Day, Day, null, null, [new("A", 10m, VatRate.R23)]);
        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(noNumber, Ct));

        await t.Invoice(c, InvoiceType.Purchase, number: "ZAK/1");
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Invoice(c, InvoiceType.Purchase, number: "ZAK/1"));
    }

    [Fact]
    public async Task CreateInvoice_RejectsUnknownAndInactiveContractor()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Invoice(Guid.NewGuid()));

        var c = await t.AddContractor();
        var contractor = await t.Db.Contractors.SingleAsync(x => x.Id == c);
        contractor.Update(contractor.Name, null, null, null, false);
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Invoice(c));
    }

    [Fact]
    public async Task CreateInvoice_RejectsWrongResultAccountType()
    {
        var c = await t.AddContractor();
        var cash = await t.Acc("100");
        var cmd = new CreateInvoice.Command(InvoiceType.Sales, null, c, Day, Day, Day, cash.Id, null, [new("A", 10m, VatRate.R23)]);
        await Assert.ThrowsAsync<DomainValidationException>(() => new CreateInvoice.Handler(t.Db, t.Me, t.Audit).Handle(cmd, Ct));
    }

    [Fact]
    public async Task CreateInvoice_RespectsPeriodLock()
    {
        var c = await t.AddContractor();
        await t.Invoice(c, issue: new DateTime(2025, 6, 1));
        await new LedgerCore.Application.Features.Administration.CloseYear.Handler(t.Db, t.Me, t.Audit)
            .Handle(new(2025), Ct);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => t.Invoice(c, issue: new DateTime(2025, 12, 31)));
        Assert.Contains("zamknięty", ex.Message);
    }

    [Fact]
    public async Task CreateInvoice_FailsWithoutSystemAccounts()
    {
        var c = await t.AddContractor();
        t.Db.Accounts.Remove(await t.Db.Accounts.SingleAsync(a => a.Code == "201"));
        await t.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => t.Invoice(c));
        Assert.Contains("201", ex.Message);
    }

    [Fact]
    public async Task CreateInvoice_WritesAuditEntry()
    {
        var c = await t.AddContractor();
        await t.Invoice(c);
        Assert.True(await t.Db.AuditEntries.AnyAsync(a => a.Action == "Faktura"));
    }

    [Fact]
    public void CreateInvoice_Validator_RejectsBadInput()
    {
        var v = new CreateInvoice.Validator();
        Assert.False(v.Validate(new CreateInvoice.Command(InvoiceType.Sales, null, Guid.Empty, Day, Day, Day, null, null, [])).IsValid);
        Assert.False(v.Validate(new CreateInvoice.Command(InvoiceType.Sales, null, Guid.NewGuid(), Day, Day, Day, null, null,
            [new("A", 0m, VatRate.R23)])).IsValid);
        Assert.True(v.Validate(new CreateInvoice.Command(InvoiceType.Sales, null, Guid.NewGuid(), Day, Day, Day, null, null,
            [new("A", 1m, VatRate.R23)])).IsValid);
    }

    [Fact]
    public async Task RecordPayment_Sales_BooksBankAgainstReceivables()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c); // 123
        await t.Pay(inv.InvoiceId, 100m);
        Assert.Equal(100m, await t.Net("130"));
        Assert.Equal(23m, await t.Net("201"));
        var stored = await t.Db.Invoices.Include(i => i.Payments).SingleAsync(i => i.Id == inv.InvoiceId);
        Assert.Equal(23m, stored.Remaining);
        Assert.Single(stored.Payments);
    }

    [Fact]
    public async Task RecordPayment_Purchase_BooksPayableAgainstBank()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c, InvoiceType.Purchase, 100m);
        await t.Pay(inv.InvoiceId, 123m);
        Assert.Equal(-123m, await t.Net("130"));
        Assert.Equal(0m, await t.Net("202"));
    }

    [Fact]
    public async Task RecordPayment_RejectsOverpaymentAndNonAssetAccount()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Pay(inv.InvoiceId, 123.01m));
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Pay(inv.InvoiceId, 10m, account: "700"));
        Assert.Equal(0m, await t.Net("130"));
    }

    [Fact]
    public async Task RecordPayment_RejectsUnknownInvoice()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Pay(Guid.NewGuid(), 10m));
    }

    [Fact]
    public async Task CancelInvoice_ReversesBookingAndBlocksFurtherPayment()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        await new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(inv.InvoiceId), Ct);

        Assert.Equal(0m, await t.Net("201"));
        Assert.Equal(0m, await t.Net("700"));
        Assert.True((await t.Db.Invoices.SingleAsync()).IsCanceled);
        Assert.Equal(1, await t.Db.Transactions.CountAsync(x => x.ReversesTransactionId == inv.TransactionId));
        await Assert.ThrowsAsync<DomainValidationException>(() => t.Pay(inv.InvoiceId, 10m));
    }

    [Fact]
    public async Task CancelInvoice_RejectedWhenPaid()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        await t.Pay(inv.InvoiceId, 10m);
        await Assert.ThrowsAsync<DomainValidationException>(() => new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(inv.InvoiceId), Ct));
    }

    [Fact]
    public async Task ReverseTransaction_RejectsInvoiceBookingButReversesManualOne()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        var rev = new ReverseTransaction.Handler(t.Db, t.Me, t.Audit);
        await Assert.ThrowsAsync<DomainValidationException>(() => rev.Handle(new(inv.TransactionId, Day), Ct));

        var manual = await t.Post("Wpłata kapitału", "130", "800", 500m);
        var storno = await rev.Handle(new(manual.TransactionId, Day), Ct);
        Assert.Equal(0m, await t.Net("130"));
        await Assert.ThrowsAsync<DomainValidationException>(() => rev.Handle(new(manual.TransactionId, Day), Ct)); // już stornowane
        await Assert.ThrowsAsync<DomainValidationException>(() => rev.Handle(new(storno.TransactionId, Day), Ct)); // storno storna
    }

    // ---- korekty ----

    CreateCorrection.Handler Correct => new(t.Db, t.Me, t.Audit);

    [Fact]
    public async Task CreateCorrection_Decrease_ReducesReceivableRevenueAndVat()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c); // 100 + 23
        var corr = await Correct.Handle(new(inv.InvoiceId, null, Day.AddDays(1), "Rabat", [new("Rabat", -40m, VatRate.R23)]), Ct);

        Assert.StartsWith("KFV/2025/", corr.Number);
        Assert.Equal(123m - 49.20m, await t.Net("201"));
        Assert.Equal(-60m, await t.Net("700"));
        Assert.Equal(-13.80m, await t.Net("220"));
        var original = await t.Db.Invoices.SingleAsync(i => i.Id == inv.InvoiceId);
        Assert.Equal(73.80m, original.Remaining);
    }

    [Fact]
    public async Task CreateCorrection_Increase_AddsToReceivable()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        await Correct.Handle(new(inv.InvoiceId, null, Day, null, [new("Dopłata", 10m, VatRate.R23)]), Ct);
        Assert.Equal(135.30m, await t.Net("201"));
        Assert.Equal(110m, -await t.Net("700"));
    }

    [Fact]
    public async Task CreateCorrection_RejectedBelowPaidAndOnCorrectionAndCanceled()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        await t.Pay(inv.InvoiceId, 123m);
        await Assert.ThrowsAsync<DomainValidationException>(() => Correct.Handle(new(inv.InvoiceId, null, Day, null, [new("R", -10m, VatRate.R23)]), Ct));

        var inv2 = await t.Invoice(c);
        var corr = await Correct.Handle(new(inv2.InvoiceId, null, Day, null, [new("R", -10m, VatRate.R23)]), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => Correct.Handle(new(corr.InvoiceId, null, Day, null, [new("R", -1m, VatRate.R23)]), Ct));

        var inv3 = await t.Invoice(c);
        await new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(inv3.InvoiceId), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => Correct.Handle(new(inv3.InvoiceId, null, Day, null, [new("R", -1m, VatRate.R23)]), Ct));
    }

    [Fact]
    public async Task CancelInvoice_WithActiveCorrectionIsRejected_AndCancelingCorrectionRestoresRemaining()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c);
        var corr = await Correct.Handle(new(inv.InvoiceId, null, Day, null, [new("R", -40m, VatRate.R23)]), Ct);
        var cancel = new CancelInvoice.Handler(t.Db, t.Me, t.Audit);

        await Assert.ThrowsAsync<DomainValidationException>(() => cancel.Handle(new(inv.InvoiceId), Ct));

        await cancel.Handle(new(corr.InvoiceId), Ct);
        Assert.Equal(123m, (await t.Db.Invoices.SingleAsync(i => i.Id == inv.InvoiceId)).Remaining);
        Assert.Equal(123m, await t.Net("201"));
    }

    [Fact]
    public async Task CreateCorrection_PurchaseRequiresSupplierNumber()
    {
        var c = await t.AddContractor();
        var inv = await t.Invoice(c, InvoiceType.Purchase, 100m);
        await Assert.ThrowsAsync<DomainValidationException>(() => Correct.Handle(new(inv.InvoiceId, null, Day, null, [new("R", -10m, VatRate.R23)]), Ct));
        await Correct.Handle(new(inv.InvoiceId, "KOR/1", Day, null, [new("R", -10m, VatRate.R23)]), Ct);
        Assert.Equal(90m, await t.Net("402"));
        Assert.Equal(-110.70m, await t.Net("202"));
    }
}
