using LedgerCore.Application.Features.Administration;
using LedgerCore.Application.Features.Companies;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Application.Features.Registers;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Application.Features.Transactions.Commands.Reverse;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

/// <summary>KPiR i ryczałt (T-007): ewidencja jako widok nad fakturami, bez księgowań na konta.</summary>
public class RegistersTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    async Task SetMode(AccountingMode mode)
    {
        var c = await t.Db.Companies.SingleAsync(x => x.Id == t.Company.CompanyId);
        c.SetAccountingMode(mode);
        await t.Db.SaveChangesAsync();
    }

    Task<CreateInvoice.Result> Inv(Guid contractor, InvoiceType type, decimal net, DateTime issue, KpirKind? kind = null, decimal? rate = null,
        string? number = null, VatRate vat = VatRate.R23, DateTime? sale = null, string desc = "Usługa") =>
        new CreateInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new CreateInvoice.Command(type, type == InvoiceType.Purchase ? number ?? $"ZAK/{Guid.NewGuid():N}"[..12] : null,
            contractor, issue, sale, issue.AddDays(14), null, null, [new CreateInvoice.LineDto(desc, net, vat)], kind, rate), Ct);

    Task<KpirRegister.Response> Kpir(string from, string to) =>
        new KpirRegister.Handler(t.Db).Handle(new(DateTime.Parse(from), DateTime.Parse(to)), Ct);

    Task<RyczaltRegister.Response> Ryczalt(string from, string to) =>
        new RyczaltRegister.Handler(t.Db).Handle(new(DateTime.Parse(from), DateTime.Parse(to)), Ct);

    // ---------------- KPiR ----------------

    [Fact]
    public async Task Kpir_three_sales_and_two_purchases_sum_to_expected_columns_without_any_postings()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("Klient");
        await Inv(c, InvoiceType.Sales, 1000m, new DateTime(2025, 3, 5));                                        // kol. 7 (domyślnie)
        await Inv(c, InvoiceType.Sales, 250.50m, new DateTime(2025, 3, 20), KpirKind.OtherRevenue);               // kol. 8
        await Inv(c, InvoiceType.Sales, 500m, new DateTime(2025, 4, 2), KpirKind.Revenue);                        // kol. 7
        await Inv(c, InvoiceType.Purchase, 300m, new DateTime(2025, 3, 7), KpirKind.GoodsPurchase);               // kol. 10
        await Inv(c, InvoiceType.Purchase, 120m, new DateTime(2025, 4, 9));                                       // domyślnie kol. 13

        var r = await Kpir("2025-01-01", "2025-12-31");
        Assert.Equal(5, r.Rows.Count);
        Assert.Equal(1500m, r.Period.Revenue);
        Assert.Equal(250.50m, r.Period.OtherRevenue);
        Assert.Equal(1750.50m, r.Period.TotalRevenue);
        Assert.Equal(300m, r.Period.GoodsPurchase);
        Assert.Equal(120m, r.Period.OtherExpense);
        Assert.Equal(420m, r.Period.TotalExpense);
        Assert.Equal(1330.50m, r.Income);
        Assert.Equal(Enumerable.Range(1, 5), r.Rows.Select(x => x.Lp));

        // żadnych księgowań na kontach ani powiązań z dziennikiem
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
        Assert.All(await t.Db.Invoices.ToListAsync(), i => Assert.Null(i.TransactionId));
    }

    [Fact]
    public async Task Kpir_columns_row_totals_and_research_costs()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("Dostawca");
        await Inv(c, InvoiceType.Purchase, 100m, new DateTime(2025, 5, 1), KpirKind.PurchaseCosts);
        await Inv(c, InvoiceType.Purchase, 40m, new DateTime(2025, 5, 2), KpirKind.Research);
        var r = await Kpir("2025-05-01", "2025-05-31");
        var side = r.Rows.Single(x => x.PurchaseCosts == 100m);
        Assert.Equal(100m, side.TotalExpense);
        var bar = r.Rows.Single(x => x.Research == 40m);
        Assert.Equal(40m, bar.OtherExpense);        // B+R jest w kol. 13 i dodatkowo w kol. 16
        Assert.Equal(40m, bar.TotalExpense);
        Assert.Equal(140m, r.Period.TotalExpense);
        Assert.Equal(40m, r.Period.Research);
    }

    [Fact]
    public async Task Kpir_uses_sale_date_for_sales_and_issue_date_for_purchases_and_orders_by_event_date()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("K");
        await Inv(c, InvoiceType.Sales, 100m, new DateTime(2025, 2, 10), sale: new DateTime(2025, 1, 28)); // sprzedaż w styczniu, faktura w lutym
        await Inv(c, InvoiceType.Purchase, 10m, new DateTime(2025, 1, 30));
        var jan = await Kpir("2025-01-01", "2025-01-31");
        Assert.Equal(2, jan.Rows.Count);
        Assert.Equal(new DateTime(2025, 1, 28), jan.Rows[0].Date);   // lp. 1 = wcześniejsze zdarzenie
        Assert.Equal(100m, jan.Period.Revenue);
        var feb = await Kpir("2025-02-01", "2025-02-28");
        Assert.Empty(feb.Rows);
        Assert.Equal(100m, feb.YearToDate.Revenue);                    // narastająco od początku roku
    }

    [Fact]
    public async Task Kpir_months_have_period_and_cumulative_totals_and_lp_continues_through_the_year()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("K");
        await Inv(c, InvoiceType.Sales, 100m, new DateTime(2025, 1, 10));
        await Inv(c, InvoiceType.Sales, 200m, new DateTime(2025, 2, 10));
        await Inv(c, InvoiceType.Purchase, 50m, new DateTime(2025, 2, 11), KpirKind.GoodsPurchase);
        await Inv(c, InvoiceType.Sales, 400m, new DateTime(2025, 3, 10));

        var r = await Kpir("2025-02-01", "2025-03-31");
        Assert.Equal(3, r.Rows.Count);                                                  // 3 wpisy w lutym i marcu
        Assert.Equal(2, r.Rows[0].Lp);                                                  // lp. liczone od początku roku (styczeń = 1)
        Assert.Equal(["2025-02", "2025-03"], r.Months.Select(m => m.Month));
        Assert.Equal(200m, r.Months[0].Period.Revenue);
        Assert.Equal(300m, r.Months[0].YearToDate.Revenue);
        Assert.Equal(250m, r.Months[0].Income);
        Assert.Equal(400m, r.Months[1].Period.Revenue);
        Assert.Equal(700m, r.Months[1].YearToDate.Revenue);
        Assert.Equal(650m, r.Months[1].Income);
        Assert.Equal(650m, r.Income);
        Assert.Equal(124m, r.AdvanceInfo);                                              // 19% z 650 = 123,5 → 124 zł (informacyjnie)
    }

    [Fact]
    public async Task Kpir_skips_canceled_and_includes_corrections_with_sign_on_correction_date()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("K");
        var sale = await Inv(c, InvoiceType.Sales, 1000m, new DateTime(2025, 6, 1));
        var gone = await Inv(c, InvoiceType.Sales, 777m, new DateTime(2025, 6, 2));
        await new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(gone.InvoiceId), Ct);
        await new CreateCorrection.Handler(t.Db, t.Me, t.Audit).Handle(
            new(sale.InvoiceId, null, new DateTime(2025, 7, 3), "Rabat", [new("Rabat", -100m, VatRate.R23)]), Ct);

        var june = await Kpir("2025-06-01", "2025-06-30");
        Assert.Equal(1000m, june.Period.Revenue);
        var july = await Kpir("2025-07-01", "2025-07-31");
        Assert.Equal(-100m, july.Period.Revenue);
        Assert.Equal("korekta", july.Rows.Single().Remarks);
        Assert.Equal(900m, july.YearToDate.Revenue);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Kpir_rejects_wrong_classification_and_bad_ranges()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("K");
        await Assert.ThrowsAsync<DomainValidationException>(() => Inv(c, InvoiceType.Sales, 10m, Day, KpirKind.GoodsPurchase));
        await Assert.ThrowsAsync<DomainValidationException>(() => Inv(c, InvoiceType.Purchase, 10m, Day, KpirKind.Revenue));
        await Assert.ThrowsAsync<DomainValidationException>(() => Kpir("2025-01-01", "2026-01-31"));
        await Assert.ThrowsAsync<DomainValidationException>(() => Kpir("2025-05-01", "2025-04-01"));
    }

    [Fact]
    public async Task Kpir_payments_need_no_account_and_do_not_post()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("K");
        var inv = await Inv(c, InvoiceType.Sales, 100m, Day);          // brutto 123
        await new RecordPayment.Handler(t.Db, t.Me, t.Audit).Handle(new(inv.InvoiceId, Day, 100m, Guid.Empty), Ct);
        var stored = await t.Db.Invoices.Include(i => i.Payments).SingleAsync();
        Assert.Equal(23m, stored.Remaining);
        Assert.Single(stored.Payments);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
        var detail = await new GetInvoice.Handler(t.Db).Handle(new(inv.InvoiceId), Ct);
        Assert.Null(detail.Payments.Single().TransactionId);
        Assert.Equal(KpirKind.Revenue, detail.KpirKind);
    }

    [Fact]
    public async Task Account_operations_are_blocked_outside_full_books()
    {
        await SetMode(AccountingMode.Kpir);
        var cash = (await t.Acc("100")).Id; var cap = (await t.Acc("800")).Id;
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new CreateTransaction.Handler(t.Db, t.Me, t.Audit)
            .Handle(new("X", Day, [new(cash, 1m, DebitCredit.Debit), new(cap, 1m, DebitCredit.Credit)]), Ct));
        Assert.Contains("KPiR", ex.Message);
        await Assert.ThrowsAsync<DomainValidationException>(() => new ReverseTransaction.Handler(t.Db, t.Me, t.Audit).Handle(new(Guid.NewGuid(), Day), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct));

        await SetMode(AccountingMode.Ryczalt);
        var ex2 = await Assert.ThrowsAsync<DomainValidationException>(() => new CloseYear.Handler(t.Db, t.Me, t.Audit).Handle(new(2025), Ct));
        Assert.Contains("ryczałtu", ex2.Message);
    }

    [Fact]
    public async Task Full_books_company_is_unaffected_and_registers_ignore_its_invoices()
    {
        var c = await t.AddContractor("K");
        var r = await Inv(c, InvoiceType.Sales, 100m, Day, KpirKind.OtherRevenue, 5.5m);   // klasyfikacja jest ignorowana w pełnej księgowości
        Assert.NotEqual(Guid.Empty, r.TransactionId);
        var inv = await t.Db.Invoices.SingleAsync();
        Assert.Null(inv.KpirKind); Assert.Null(inv.RyczaltRate);
        Assert.Empty((await Kpir("2025-01-01", "2025-12-31")).Rows);
        Assert.Empty((await Ryczalt("2025-01-01", "2025-12-31")).Rows);
    }

    [Fact]
    public async Task Registers_are_isolated_per_company()
    {
        await SetMode(AccountingMode.Kpir);
        var c = await t.AddContractor("K");
        await Inv(c, InvoiceType.Sales, 100m, Day);
        t.Use(t.CompanyB);
        await SetMode(AccountingMode.Kpir);
        Assert.Empty((await Kpir("2025-01-01", "2025-12-31")).Rows);
        t.Use(t.CompanyA);
        Assert.Single((await Kpir("2025-01-01", "2025-12-31")).Rows);
    }

    // ---------------- ryczałt ----------------

    [Fact]
    public async Task Ryczalt_sums_revenue_by_rate_and_rounds_tax_to_full_zloty_per_rate()
    {
        await SetMode(AccountingMode.Ryczalt);
        var c = await t.AddContractor("K");
        await Inv(c, InvoiceType.Sales, 1000m, new DateTime(2025, 1, 10), rate: 8.5m);
        await Inv(c, InvoiceType.Sales, 333.33m, new DateTime(2025, 1, 20), rate: 8.5m);
        await Inv(c, InvoiceType.Sales, 2000m, new DateTime(2025, 2, 5), rate: 12m);
        await Inv(c, InvoiceType.Purchase, 999m, new DateTime(2025, 2, 6));   // zakupy nie wchodzą do ewidencji przychodów

        var r = await Ryczalt("2025-01-01", "2025-02-28");
        Assert.Equal(3, r.Rows.Count);
        Assert.Equal(3333.33m, r.TotalRevenue);
        var r85 = r.ByRate.Single(x => x.Rate == 8.5m);
        Assert.Equal(1333.33m, r85.Revenue);
        Assert.Equal(113m, r85.Tax);                       // 113,33 → 113
        Assert.Equal(240m, r.ByRate.Single(x => x.Rate == 12m).Tax);
        Assert.Equal(353m, r.YearToDateTax);
        Assert.Equal(["2025-01", "2025-02"], r.Months.Select(m => m.Month));
        Assert.Equal(1333.33m, r.Months[0].Revenue);
        Assert.Equal(3333.33m, r.Months[1].YearToDateRevenue);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7.0)]
    [InlineData(0.0)]
    [InlineData(21.0)]
    public async Task Ryczalt_requires_a_known_rate_for_sales(double? rate)
    {
        await SetMode(AccountingMode.Ryczalt);
        var c = await t.AddContractor("K");
        await Assert.ThrowsAsync<DomainValidationException>(() => Inv(c, InvoiceType.Sales, 10m, Day, rate: rate is null ? null : (decimal)rate));
    }

    [Fact]
    public async Task Ryczalt_corrections_and_cancellations_adjust_the_register_and_ytd_tax_uses_all_year()
    {
        await SetMode(AccountingMode.Ryczalt);
        var c = await t.AddContractor("K");
        var a = await Inv(c, InvoiceType.Sales, 1000m, new DateTime(2025, 1, 5), rate: 3m);
        await new CreateCorrection.Handler(t.Db, t.Me, t.Audit).Handle(new(a.InvoiceId, null, new DateTime(2025, 2, 5), null, [new("Zwrot", -400m, VatRate.R23)]), Ct);
        var b = await Inv(c, InvoiceType.Sales, 500m, new DateTime(2025, 2, 6), rate: 3m);
        await new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(b.InvoiceId), Ct);

        var feb = await Ryczalt("2025-02-01", "2025-02-28");
        Assert.Equal(-400m, feb.TotalRevenue);
        Assert.Equal(600m, feb.Months.Single().YearToDateRevenue);
        Assert.Equal(18m, feb.YearToDateTax);           // 3% × 600
        Assert.Contains("(korekta)", feb.Rows.Single().Document);
    }

    // ---------------- firmy i tryb ----------------

    [Fact]
    public async Task Company_created_in_kpir_mode_gets_no_chart_of_accounts()
    {
        var id = await new CreateCompany.Handler(t.Db, t.Audit, t.Company).Handle(new("Klient KPiR", null, null, true, AccountingMode.Kpir), Ct);
        var saved = await t.Db.Companies.SingleAsync(x => x.Id == id);
        Assert.Equal(AccountingMode.Kpir, saved.AccountingMode);
        t.Use(id);
        Assert.Empty(await t.Db.Accounts.ToListAsync());
        var mine = await new ListAllCompanies.Handler(t.Db).Handle(new(), Ct);
        Assert.Equal(AccountingMode.Kpir, mine.Single(x => x.Id == id).AccountingMode);
    }

    [Fact]
    public async Task Mode_change_is_allowed_for_empty_company_and_blocked_when_data_exists()
    {
        var h = new SetAccountingMode.Handler(t.Db, t.Audit);
        await h.Handle(new(AccountingMode.Kpir), Ct);
        Assert.Equal(AccountingMode.Kpir, (await t.Db.Companies.SingleAsync(x => x.Id == t.CompanyA)).AccountingMode);
        await h.Handle(new(AccountingMode.FullBooks), Ct);

        await t.Post("Kapitał", "100", "800", 5m);
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(AccountingMode.Ryczalt), Ct));
        Assert.Contains("nową firmę", ex.Message);
        Assert.Equal(AccountingMode.FullBooks, (await t.Db.Companies.SingleAsync(x => x.Id == t.CompanyA)).AccountingMode);
        await h.Handle(new(AccountingMode.FullBooks), Ct);          // ten sam tryb – bez zmian i bez błędu
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new((AccountingMode)99), Ct));

        // faktura w KPiR też blokuje zmianę
        t.Use(t.CompanyB);
        await h.Handle(new(AccountingMode.Kpir), Ct);
        var c = await t.AddContractor("K");
        await Inv(c, InvoiceType.Sales, 10m, Day);
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(AccountingMode.FullBooks), Ct));
    }
}
