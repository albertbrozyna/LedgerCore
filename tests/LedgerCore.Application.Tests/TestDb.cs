using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Contractors;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Application.Features.Setup;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Infrastructure.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Tests;

public class FakeUserDetails : IUserDetails
{
    public User User { get; set; } = null!;
}

/// <summary>
/// Prawdziwy LedgerDbContext na bazie SQLite w pamięci z fałszywym ICompanyContext.
/// Dwie firmy (A i B) z domyślnym planem kont; <see cref="Use"/> przełącza bieżącą firmę (jak nagłówek X-Company-Id).
/// </summary>
public sealed class TestDb : IDisposable
{
    readonly SqliteConnection _connection = new("DataSource=:memory:");
    public CompanyContext Company { get; } = new();
    public LedgerDbContext Db { get; }
    public FakeUserDetails Me { get; } = new();
    public AuditLog Audit { get; }
    public Guid CompanyA { get; }
    public Guid CompanyB { get; }

    public static readonly CancellationToken Ct = CancellationToken.None;
    public static readonly DateTime Day = new(2025, 3, 10);

    public TestDb()
    {
        _connection.Open();
        Db = new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite(_connection).Options, Company);
        Db.Database.EnsureCreated();
        Audit = new AuditLog(Db, Me);

        var a = new Company("Firma A");
        var b = new Company("Firma B");
        Db.Companies.AddRange(a, b);
        CompanyA = a.Id;
        CompanyB = b.Id;
        foreach (var id in new[] { CompanyA, CompanyB })
        {
            Company.CompanyId = id;
            DefaultChartOfAccounts.AddTo(Db);
            Db.SaveChanges();
        }
        Company.CompanyId = CompanyA;
    }

    public void Use(Guid companyId) => Company.CompanyId = companyId;

    public async Task<Account> Acc(string code) => await Db.Accounts.AsNoTracking().FirstAsync(a => a.Code == code);

    public async Task<Guid> AddContractor(string name = "Klient", string? nip = null) =>
        await new SaveContractor.Handler(Db, Audit).Handle(new SaveContractor.Command(null, name, nip, null, null), Ct);

    public Task<CreateInvoice.Result> Invoice(Guid contractorId, InvoiceType type = InvoiceType.Sales, decimal net = 100m,
        VatRate rate = VatRate.R23, string? number = null, DateTime? issue = null)
    {
        var d = issue ?? Day;
        return new CreateInvoice.Handler(Db, Me, Audit).Handle(
            new CreateInvoice.Command(type, type == InvoiceType.Purchase ? number ?? "ZAK/1" : number, contractorId, d, d, d.AddDays(14), null, null,
                [new CreateInvoice.LineDto("Usługa", net, rate)]), Ct);
    }

    public async Task Pay(Guid invoiceId, decimal amount, string account = "130", DateTime? date = null) =>
        await new RecordPayment.Handler(Db, Me, Audit).Handle(new RecordPayment.Command(invoiceId, date ?? Day, amount, (await Acc(account)).Id), Ct);

    public async Task<CreateTransaction.Result> Post(string description, string debit, string credit, decimal amount, DateTime? date = null) =>
        await new CreateTransaction.Handler(Db, Me, Audit).Handle(new CreateTransaction.Command(description, date ?? Day,
            [new CreateTransaction.NewEntryDto((await Acc(debit)).Id, amount, DebitCredit.Debit),
             new CreateTransaction.NewEntryDto((await Acc(credit)).Id, amount, DebitCredit.Credit)]), Ct);

    /// <summary>Saldo netto (Winien − Ma) konta o danym kodzie, w bieżącej firmie.</summary>
    public async Task<decimal> Net(string code)
    {
        var acc = await Acc(code);
        return LedgerQueries.Net(await LedgerQueries.LoadEntries(Db, null, null, acc.Id, Ct));
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
