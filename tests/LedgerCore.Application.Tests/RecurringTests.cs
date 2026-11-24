using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class RecurringTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    ISender Sender()
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton<IAppDbContext>(t.Db);
        s.AddSingleton<IUserDetails>(t.Me);
        s.AddSingleton(t.Audit);
        s.AddSingleton<ICompanyContext>(t.Company);
        s.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(IAppDbContext).Assembly));
        return s.BuildServiceProvider().GetRequiredService<ISender>();
    }

    Task<Guid> Template(Guid contractor, DateTime next, RecurrenceFrequency f = RecurrenceFrequency.Monthly, int day = 15, int due = 14,
        DateTime? end = null, string name = "Abonament", decimal net = 100m, Guid? id = null) =>
        new RecurringInvoices.SaveHandler(t.Db, t.Audit).Handle(new RecurringInvoices.SaveCommand(id, name, contractor, f, day, due, next, end,
            null, null, null, null, [new RecurringInvoices.LineDto("Abonament miesięczny", net, VatRate.R23)]), Ct);

    Task<RecurringInvoices.GenerateResult> Generate(DateTime asOf, Guid? only = null) =>
        new RecurringInvoices.GenerateHandler(t.Db, Sender(), t.Audit).Handle(new RecurringInvoices.GenerateCommand(asOf, only), Ct);

    [Fact]
    public async Task Generates_overdue_invoices_and_second_run_creates_no_duplicates()
    {
        var c = await t.AddContractor();
        await Template(c, new DateTime(2025, 1, 15));

        var first = await Generate(new DateTime(2025, 3, 20));
        Assert.Equal(["FV/2025/0001", "FV/2025/0002", "FV/2025/0003"], first.Generated.Select(g => g.Number));
        Assert.Equal([new DateTime(2025, 1, 15), new DateTime(2025, 2, 15), new DateTime(2025, 3, 15)], first.Generated.Select(g => g.IssueDate));
        Assert.Empty(first.Skipped);

        var second = await Generate(new DateTime(2025, 3, 20));
        Assert.Empty(second.Generated);
        Assert.Equal(3, await t.Db.Invoices.CountAsync());

        var list = await new RecurringInvoices.ListHandler(t.Db).Handle(new RecurringInvoices.ListQuery(), Ct);
        Assert.Equal(new DateTime(2025, 4, 15), list.Single().NextDate);
        Assert.Equal(3, list.Single().GeneratedCount);
    }

    [Fact]
    public async Task Rewinding_the_template_does_not_duplicate_already_issued_periods()
    {
        var c = await t.AddContractor();
        var id = await Template(c, new DateTime(2025, 1, 15));
        await Generate(new DateTime(2025, 2, 20)); // styczeń i luty

        await Template(c, new DateTime(2025, 1, 15), id: id); // ktoś cofnął „następną datę”
        var again = await Generate(new DateTime(2025, 2, 20));

        Assert.Empty(again.Generated);
        Assert.Equal(2, await t.Db.Invoices.CountAsync());
        Assert.Equal(new DateTime(2025, 3, 15), (await t.Db.RecurringInvoices.SingleAsync()).NextDate);
    }

    [Fact]
    public async Task Database_enforces_one_invoice_per_template_period()
    {
        var c = await t.AddContractor();
        var id = await Template(c, new DateTime(2025, 1, 15));
        await Generate(new DateTime(2025, 1, 31));
        var existing = await t.Db.Invoices.SingleAsync();
        var dup = new Invoice(InvoiceType.Sales, "FV/RĘCZNY/1", c, existing.IssueDate, existing.IssueDate, existing.IssueDate, existing.ResultAccountId, null, null,
            [("Usługa", 10m, VatRate.R23)]);
        dup.MarkRecurring(id, new DateTime(2025, 1, 15));
        t.Db.Invoices.Add(dup);
        await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Generated_invoice_is_booked_and_audited_as_recurring()
    {
        var c = await t.AddContractor();
        await Template(c, new DateTime(2025, 1, 15), net: 200m);
        await Generate(new DateTime(2025, 1, 31));

        var inv = await t.Db.Invoices.Include(i => i.Lines).SingleAsync();
        Assert.Equal(246m, inv.Gross);
        Assert.NotNull(inv.TransactionId);
        Assert.Equal(new DateTime(2025, 1, 29), inv.DueDate);
        Assert.Equal(246m, await t.Net("201"));
        Assert.Equal(-200m, await t.Net("700"));
        Assert.Equal(-46m, await t.Net("220"));
        var audit = await t.Db.AuditEntries.Where(a => a.Action == "Faktura cykliczna").ToListAsync();
        Assert.Contains(audit, a => a.Details.Contains(inv.Number));
    }

    [Fact]
    public async Task Closed_period_skips_only_that_template_and_keeps_it_due()
    {
        var c = await t.AddContractor();
        await Template(c, new DateTime(2025, 1, 15), name: "A-zamknięty");
        await Template(c, new DateTime(2025, 3, 15), name: "B-otwarty");
        var row = new PeriodLock();
        row.Set(new DateTime(2025, 2, 28));
        t.Db.PeriodLocks.Add(row);
        await t.Db.SaveChangesAsync(Ct);

        var r = await Generate(new DateTime(2025, 3, 20));

        Assert.Single(r.Generated);
        Assert.Equal(new DateTime(2025, 3, 15), r.Generated[0].IssueDate);
        var skipped = Assert.Single(r.Skipped);
        Assert.Equal("A-zamknięty", skipped.Template);
        Assert.Contains("zamknięty", skipped.Reason);
        Assert.Equal(new DateTime(2025, 1, 15), (await t.Db.RecurringInvoices.SingleAsync(x => x.Name == "A-zamknięty")).NextDate);
    }

    [Fact]
    public async Task Inactive_template_and_future_template_do_not_generate()
    {
        var c = await t.AddContractor();
        var paused = await Template(c, new DateTime(2025, 1, 15), name: "Wstrzymany");
        await Template(c, new DateTime(2025, 6, 15), name: "Przyszły");
        await new RecurringInvoices.SetActiveHandler(t.Db, t.Audit).Handle(new(paused, false), Ct);

        var r = await Generate(new DateTime(2025, 3, 20));
        Assert.Empty(r.Generated);
        Assert.Empty(r.Skipped);
    }

    [Fact]
    public async Task End_date_stops_generation_and_deactivates_template()
    {
        var c = await t.AddContractor();
        await Template(c, new DateTime(2025, 1, 15), end: new DateTime(2025, 2, 20));

        var r = await Generate(new DateTime(2025, 6, 1));

        Assert.Equal(2, r.Generated.Count);
        Assert.False((await t.Db.RecurringInvoices.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Generating_a_single_template_leaves_others_untouched()
    {
        var c = await t.AddContractor();
        var a = await Template(c, new DateTime(2025, 1, 15), name: "A");
        await Template(c, new DateTime(2025, 1, 15), name: "B");
        var r = await Generate(new DateTime(2025, 1, 31), a);
        Assert.Equal("A", Assert.Single(r.Generated).Template);
    }

    [Fact]
    public async Task Templates_and_generation_are_isolated_per_company()
    {
        var a = await t.AddContractor();
        await Template(a, new DateTime(2025, 1, 15));
        t.Use(t.CompanyB);
        Assert.Empty(await new RecurringInvoices.ListHandler(t.Db).Handle(new RecurringInvoices.ListQuery(), Ct));
        var r = await Generate(new DateTime(2025, 3, 20));
        Assert.Empty(r.Generated);
        t.Use(t.CompanyA);
        Assert.Empty(await t.Db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task Template_validation_errors_are_in_polish()
    {
        var c = await t.AddContractor();
        var empty = new RecurringInvoices.SaveCommand(null, "X", c, RecurrenceFrequency.Monthly, 15, 14, new DateTime(2025, 1, 1), null, null, null, null, null, []);
        Assert.Contains("co najmniej jednej pozycji", (await Assert.ThrowsAsync<ValidationException>(() =>
            new RecurringInvoices.SaveValidator().ValidateAndThrowAsync(empty))).Message);
        await Assert.ThrowsAsync<DomainValidationException>(() => Template(c, new DateTime(2025, 1, 1), day: 32));
        await Assert.ThrowsAsync<DomainValidationException>(() => Template(c, new DateTime(2025, 1, 1), due: -1));
        await Assert.ThrowsAsync<DomainValidationException>(() => Template(c, new DateTime(2025, 2, 1), end: new DateTime(2025, 1, 1)));
        await Assert.ThrowsAsync<DomainValidationException>(() => Template(Guid.NewGuid(), new DateTime(2025, 1, 1)));
    }

    [Fact]
    public async Task Deleting_a_template_keeps_issued_invoices()
    {
        var c = await t.AddContractor();
        var id = await Template(c, new DateTime(2025, 1, 15));
        await Generate(new DateTime(2025, 1, 31));
        await new RecurringInvoices.DeleteHandler(t.Db, t.Audit).Handle(new(id), Ct);
        Assert.Empty(await t.Db.RecurringInvoices.ToListAsync());
        Assert.Equal(1, await t.Db.Invoices.CountAsync());
    }

    // ---- konfigurowalna numeracja ----

    [Fact]
    public async Task Custom_pattern_is_used_for_new_sales_invoices_and_continues_numbering()
    {
        var c = await t.AddContractor();
        await t.Invoice(c); // FV/2025/0001 – wzór domyślny
        await new NumberingSettings.SaveHandler(t.Db, t.Audit).Handle(new("{NNN}/{MM}/{RR}", NumberReset.Monthly), Ct);

        var a = await t.Invoice(c, issue: new DateTime(2025, 3, 10));
        var b = await t.Invoice(c, issue: new DateTime(2025, 3, 11));
        var next = await t.Invoice(c, issue: new DateTime(2025, 4, 1));

        Assert.Equal(["001/03/25", "002/03/25", "001/04/25"], [a.Number, b.Number, next.Number]);
    }

    [Fact]
    public async Task Invalid_pattern_is_rejected_and_old_one_stays()
    {
        var h = new NumberingSettings.SaveHandler(t.Db, t.Audit);
        var e = await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new("FV/{NNNN}", NumberReset.Yearly), Ct));
        Assert.Contains("rok", e.Message);
        var cur = await new NumberingSettings.GetHandler(t.Db).Handle(new NumberingSettings.GetQuery(), Ct);
        Assert.Equal("FV/{RRRR}/{NNNN}", cur.Pattern);
    }

    [Fact]
    public async Task Preview_does_not_change_settings_and_respects_existing_numbers()
    {
        var c = await t.AddContractor();
        await t.Invoice(c, issue: new DateTime(2025, 3, 10));
        var example = await new NumberingSettings.PreviewHandler(t.Db).Handle(new("FV/{RRRR}/{NNNN}", NumberReset.Yearly, new DateTime(2025, 6, 1)), Ct);
        Assert.Equal("FV/2025/0002", example);
        var other = await new NumberingSettings.PreviewHandler(t.Db).Handle(new("S{RR}-{NN}", NumberReset.Yearly, new DateTime(2025, 6, 1)), Ct);
        Assert.Equal("S25-01", other);
        Assert.Equal("FV/{RRRR}/{NNNN}", (await t.Db.Companies.AsNoTracking().FirstAsync(x => x.Id == t.CompanyA)).SalesNumberPattern);
    }

    [Fact]
    public async Task Numbering_is_per_company()
    {
        var a = await t.AddContractor();
        await new NumberingSettings.SaveHandler(t.Db, t.Audit).Handle(new("A/{RRRR}/{NN}", NumberReset.Yearly), Ct);
        t.Use(t.CompanyB);
        var b = await t.AddContractor("Klient B");
        var invB = await t.Invoice(b);
        Assert.Equal("FV/2025/0001", invB.Number);
        t.Use(t.CompanyA);
        Assert.Equal("A/2025/01", (await t.Invoice(a)).Number);
    }

    [Fact]
    public async Task Sales_numbers_are_unique_in_the_database_but_purchase_numbers_may_repeat_between_suppliers()
    {
        var c1 = await t.AddContractor("Klient 1");
        var c2 = await t.AddContractor("Klient 2");
        var inv = await t.Invoice(c1);
        var clone = new Invoice(InvoiceType.Sales, inv.Number, c2, Day, Day, Day, (await t.Acc("700")).Id, null, null, [("X", 1m, VatRate.R23)]);
        t.Db.Invoices.Add(clone);
        await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync(Ct));
        t.Db.ChangeTracker.Clear();

        await t.Invoice(c1, InvoiceType.Purchase, number: "ZAK/1");
        await t.Invoice(c2, InvoiceType.Purchase, number: "ZAK/1");
    }
}
