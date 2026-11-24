using LedgerCore.Application.Features.Administration;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Application.Features.Transactions.Queries;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

/// <summary>Sortowanie po stronie serwera (T-004): whitelist kolumn, kierunek, stabilna kolejność, izolacja firm.</summary>
public class SortingTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    async Task<ListTransactions.Response> Tx(string? sort, string? dir, int page = 1, int size = 25) =>
        await new ListTransactions.Handler(t.Db).Handle(new(null, null, null, null, page, size, sort, dir), Ct);

    async Task SeedTx()
    {
        await t.Post("Beta", "100", "800", 30m, new DateTime(2025, 3, 1));
        await t.Post("Alfa", "100", "800", 10m, new DateTime(2025, 1, 1));
        await t.Post("Gamma", "100", "800", 20m, new DateTime(2025, 2, 1));
    }

    [Fact]
    public async Task Transactions_default_is_newest_first()
    {
        await SeedTx();
        Assert.Equal(["Beta", "Gamma", "Alfa"], (await Tx(null, null)).Items.Select(i => i.Description));
    }

    [Theory]
    [InlineData("date", "asc", "Alfa,Gamma,Beta")]
    [InlineData("date", "desc", "Beta,Gamma,Alfa")]
    [InlineData("description", "asc", "Alfa,Beta,Gamma")]
    [InlineData("description", "desc", "Gamma,Beta,Alfa")]
    [InlineData("amount", "asc", "Alfa,Gamma,Beta")]
    [InlineData("amount", "desc", "Beta,Gamma,Alfa")]
    [InlineData("AMOUNT", "ASC", "Alfa,Gamma,Beta")]
    public async Task Transactions_sort_by_column(string sort, string dir, string expected)
    {
        await SeedTx();
        Assert.Equal(expected.Split(','), (await Tx(sort, dir)).Items.Select(i => i.Description));
    }

    [Fact]
    public async Task Transactions_sort_by_number_follows_document_numbers()
    {
        await SeedTx(); // numery DK/2025/0001..0003 w kolejności księgowania: Beta, Alfa, Gamma
        Assert.Equal(["Beta", "Alfa", "Gamma"], (await Tx("number", "asc")).Items.Select(i => i.Description));
        Assert.Equal(["Gamma", "Alfa", "Beta"], (await Tx("number", "desc")).Items.Select(i => i.Description));
    }

    [Fact]
    public async Task Transactions_sorting_applies_before_paging()
    {
        await SeedTx();
        var p1 = await Tx("amount", "desc", 1, 2);
        var p2 = await Tx("amount", "desc", 2, 2);
        Assert.Equal(["Beta", "Gamma"], p1.Items.Select(i => i.Description));
        Assert.Equal(["Alfa"], p2.Items.Select(i => i.Description));
        Assert.Equal(3, p2.TotalCount);
    }

    [Theory]
    [InlineData("amount; DROP TABLE transactions")]
    [InlineData("Description desc")]
    [InlineData("companyid")]
    [InlineData("")]
    public async Task Transactions_reject_sort_keys_outside_whitelist(string sort)
    {
        await SeedTx();
        await Assert.ThrowsAsync<DomainValidationException>(() => Tx(sort, "asc"));
    }

    [Fact]
    public async Task Transactions_sorting_stays_within_company()
    {
        await SeedTx();
        t.Use(t.CompanyB);
        Assert.Empty((await Tx("amount", "desc")).Items);
    }

    async Task<ListInvoices.Response> Inv(string? sort, string? dir) =>
        await new ListInvoices.Handler(t.Db).Handle(new(null, null, null, null, null, null, 1, 25, sort, dir), Ct);

    async Task SeedInvoices()
    {
        var zeta = await t.AddContractor("Zeta");
        var alfa = await t.AddContractor("Alfa");
        await t.Invoice(zeta, net: 300m, issue: new DateTime(2025, 1, 10));
        await t.Invoice(alfa, net: 100m, issue: new DateTime(2025, 2, 10));
        await t.Invoice(zeta, net: 200m, issue: new DateTime(2025, 3, 10));
    }

    [Theory]
    [InlineData("issue", "asc", "FV/2025/0001,FV/2025/0002,FV/2025/0003")]
    [InlineData("issue", "desc", "FV/2025/0003,FV/2025/0002,FV/2025/0001")]
    [InlineData("gross", "asc", "FV/2025/0002,FV/2025/0003,FV/2025/0001")]
    [InlineData("gross", "desc", "FV/2025/0001,FV/2025/0003,FV/2025/0002")]
    [InlineData("number", "desc", "FV/2025/0003,FV/2025/0002,FV/2025/0001")]
    [InlineData("remaining", "asc", "FV/2025/0002,FV/2025/0003,FV/2025/0001")]
    public async Task Invoices_sort_by_column(string sort, string dir, string expected)
    {
        await SeedInvoices();
        Assert.Equal(expected.Split(','), (await Inv(sort, dir)).Items.Select(i => i.Number));
    }

    [Fact]
    public async Task Invoices_sort_by_contractor_ignores_case_and_keeps_stable_order()
    {
        await SeedInvoices();
        var names = (await Inv("contractor", "asc")).Items.Select(i => i.ContractorName).ToList();
        Assert.Equal(["Alfa", "Zeta", "Zeta"], names);
    }

    [Fact]
    public async Task Invoices_reject_unknown_sort_key_and_totals_do_not_depend_on_sorting()
    {
        await SeedInvoices();
        await Assert.ThrowsAsync<DomainValidationException>(() => Inv("password", "asc"));
        Assert.Equal((await Inv("gross", "asc")).TotalGross, (await Inv("number", "desc")).TotalGross);
    }

    [Fact]
    public async Task Audit_sorts_by_user_and_action_and_rejects_unknown_keys()
    {
        await t.AddContractor("A");
        await t.AddContractor("B");
        await t.Post("X", "100", "800", 1m);
        var h = new ListAudit.Handler(t.Db);
        var byAction = await h.Handle(new(null, 1, 50, "action", "asc"), Ct);
        Assert.Equal(byAction.Items.Select(i => i.Action).OrderBy(a => a, StringComparer.Ordinal), byAction.Items.Select(i => i.Action));
        var newest = await h.Handle(new(null, 1, 50, "at", "desc"), Ct);
        Assert.Equal(newest.Items.Select(i => i.At).OrderByDescending(a => a), newest.Items.Select(i => i.At));
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(null, 1, 50, "details", "asc"), Ct));
    }
}
