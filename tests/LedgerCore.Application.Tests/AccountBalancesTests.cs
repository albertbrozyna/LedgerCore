using LedgerCore.Application.Features.Accounts.Queries.Balances;
using LedgerCore.Application.Features.Accounts.Queries.List;
using LedgerCore.Domain.Exceptions;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class AccountBalancesTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    Task<List<AccountBalances.Item>> Get(params Guid[] ids) =>
        new AccountBalances.Handler(t.Db).Handle(new(ids.ToList()), Ct);

    [Fact]
    public async Task Returns_balances_in_natural_sign_and_equals_account_list()
    {
        await t.Post("Kapitał", "130", "800", 1000m);
        await t.Post("Koszt", "402", "130", 250.50m);
        var ids = new[] { (await t.Acc("130")).Id, (await t.Acc("800")).Id, (await t.Acc("402")).Id, (await t.Acc("700")).Id };

        var balances = (await Get(ids)).ToDictionary(x => x.AccountId, x => x.Balance);
        Assert.Equal(749.50m, balances[ids[0]]);   // aktywa: Wn − Ma
        Assert.Equal(1000m, balances[ids[1]]);     // kapitał: Ma − Wn
        Assert.Equal(250.50m, balances[ids[2]]);   // koszty: Wn − Ma
        Assert.Equal(0m, balances[ids[3]]);        // konto bez obrotów

        var list = await new ListAccounts.Handler(t.Db).Handle(new(), Ct);
        foreach (var id in ids) Assert.Equal(list.Single(a => a.Id == id).Balance, balances[id]);
    }

    [Fact]
    public async Task Ignores_empty_duplicate_and_unknown_ids_and_accounts_of_other_company()
    {
        await t.Post("Kapitał", "130", "800", 10m);
        var bank = (await t.Acc("130")).Id;
        Assert.Empty(await Get());
        Assert.Single(await Get(bank, bank, Guid.Empty, Guid.NewGuid()));

        t.Use(t.CompanyB);
        Assert.Empty(await Get(bank));                          // konto firmy A jest dla firmy B „nieistniejące”
        var ownBank = (await t.Acc("130")).Id;
        Assert.Equal(0m, Assert.Single(await Get(ownBank)).Balance);
    }

    [Fact]
    public async Task Rejects_too_many_accounts()
    {
        var ids = Enumerable.Range(0, AccountBalances.MaxAccounts + 1).Select(_ => Guid.NewGuid()).ToArray();
        await Assert.ThrowsAsync<DomainValidationException>(() => Get(ids));
    }
}
