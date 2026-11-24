using LedgerCore.Application.Features.Search;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class SearchTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    Task<GlobalSearch.Response> Find(string? q) => new GlobalSearch.Handler(t.Db).Handle(new(q), Ct);

    [Fact]
    public async Task Finds_accounts_by_code_and_name()
    {
        Assert.Contains((await Find("700")).Accounts, a => a.Code == "700");
        Assert.Contains((await Find("kasa")).Accounts, a => a.Code == "100");
    }

    [Fact]
    public async Task Finds_contractors_by_name_and_by_nip_with_or_without_separators()
    {
        await t.AddContractor("Alfa Sp. z o.o.", "5260250995");
        Assert.Single((await Find("alfa")).Contractors);
        Assert.Single((await Find("526-025")).Contractors);
        Assert.Single((await Find("5260250995")).Contractors);
        Assert.Empty((await Find("9999999")).Contractors);
    }

    [Fact]
    public async Task Finds_invoices_by_number_and_transactions_by_number_or_description()
    {
        var c = await t.AddContractor("Beta");
        var inv = await t.Invoice(c);
        var tx = await t.Post("Czynsz za biuro", "100", "800", 10m);

        var byNumber = await Find(inv.Number);
        Assert.Equal(inv.InvoiceId, Assert.Single(byNumber.Invoices).Id);
        Assert.Equal("Beta", byNumber.Invoices[0].ContractorName);
        Assert.Contains((await Find("czynsz")).Transactions, x => x.Id == tx.TransactionId);
        Assert.Contains((await Find(tx.Number)).Transactions, x => x.Id == tx.TransactionId);
    }

    [Fact]
    public async Task Limits_results_to_five_per_category()
    {
        for (var i = 0; i < 8; i++) await t.AddContractor($"Hurt {i}");
        for (var i = 0; i < 8; i++) await t.Post($"Opłata {i}", "100", "800", 1m);
        var r = await Find("hurt");
        Assert.Equal(GlobalSearch.Limit, r.Contractors.Count);
        Assert.Equal(GlobalSearch.Limit, (await Find("opłata")).Transactions.Count);
    }

    [Fact]
    public async Task Returns_only_current_company_data()
    {
        var c = await t.AddContractor("Tajny Kontrahent", "5260250995");
        await t.Invoice(c);
        await t.Post("Tajne księgowanie", "100", "800", 5m);

        t.Use(t.CompanyB);
        var r = await Find("tajn");
        Assert.Empty(r.Contractors); Assert.Empty(r.Transactions);
        Assert.Empty((await Find("FV/2025")).Invoices);
        Assert.Empty((await Find("5260250995")).Contractors);
        Assert.NotEmpty((await Find("kasa")).Accounts);                 // własne konta firmy B
        Assert.All((await Find("100")).Accounts, a => Assert.NotEqual(Guid.Empty, a.Id));

        t.Use(t.CompanyA);
        Assert.Single((await Find("tajny")).Contractors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task Too_short_query_returns_nothing(string? q)
    {
        var r = await Find(q);
        Assert.Empty(r.Accounts); Assert.Empty(r.Contractors); Assert.Empty(r.Invoices); Assert.Empty(r.Transactions);
    }

    [Theory]
    [InlineData("%%")]
    [InlineData("__")]
    [InlineData("\\\\")]
    [InlineData("' OR 1=1 --")]
    public async Task Wildcards_and_sql_fragments_are_treated_literally(string q)
    {
        await t.AddContractor("Zwykły");
        await t.Post("Zwykłe księgowanie", "100", "800", 1m);
        var r = await Find(q);
        Assert.Empty(r.Contractors); Assert.Empty(r.Transactions); Assert.Empty(r.Invoices); Assert.Empty(r.Accounts);
    }

    [Fact]
    public async Task Literal_percent_in_description_can_be_found()
    {
        await t.Post("Rabat 10% za terminową płatność", "100", "800", 1m);
        Assert.Single((await Find("10%")).Transactions);
    }
}
