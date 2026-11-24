using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Features.Bank;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class BankParserTests
{
    static string File(string name) => System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
    const string Own = "11114020040000300201355387";

    [Theory]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("-1 234,56", -1234.56)]
    [InlineData("1234.50", 1234.50)]
    [InlineData("+500,00 PLN", 500)]
    [InlineData("(12,00)", -12)]
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("1.234", 1234)]
    [InlineData("12,5", 12.5)]
    public void Parses_amounts(string text, double expected) => Assert.Equal((decimal)expected, BankParsers.ParseAmount(text));

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("12,345")]
    [InlineData("1,2,3x")]
    public void Rejects_non_amounts(string text) => Assert.Null(BankParsers.ParseAmount(text));

    [Theory]
    [InlineData("2026-11-05", 2026, 11, 5)]
    [InlineData("05.11.2026", 2026, 11, 5)]
    [InlineData("05/11/2026", 2026, 11, 5)]
    [InlineData("20261105", 2026, 11, 5)]
    public void Parses_dates(string text, int y, int m, int d) => Assert.Equal(new DateTime(y, m, d), BankParsers.ParseDate(text));

    [Fact]
    public void Invalid_date_is_null() => Assert.Null(BankParsers.ParseDate("31.02.2026"));

    [Fact]
    public void Mt940_file_amounts_dates_titles_and_balances()
    {
        var r = new Mt940Parser().Parse(File("wyciag.mt940"));
        Assert.Equal(3, r.Rows.Count);
        Assert.Equal(0, r.Skipped);
        Assert.Equal([1230.00m, -15.50m, -200.00m], r.Rows.Select(x => x.Amount));
        Assert.Equal([new DateTime(2026, 11, 5), new DateTime(2026, 11, 6), new DateTime(2026, 11, 7)], r.Rows.Select(x => x.Date));
        Assert.Equal("Zapłata za FV 2026/0001 usługa księgowa Łódź", r.Rows[0].Title);
        Assert.Equal("Zażółć Gęślą Jaźń sp. z o.o. ul. Długa 1", r.Rows[0].Counterparty);
        Assert.Equal("94109010140000071219812874", r.Rows[0].CounterpartyAccount);
        Assert.Equal("Opłata za prowadzenie rachunku", r.Rows[1].Title);        // :86: bez podpól
        Assert.Equal("FZ/77/2026 licencja", r.Rows[2].Title);
        Assert.Equal(10000.00m, r.Opening);
        Assert.Equal(11014.50m, r.Closing);
        Assert.Equal(new DateTime(2026, 11, 7), r.ClosingDate);
        Assert.Equal(Own, r.AccountNumber);
    }

    [Fact]
    public void Mt940_reversals_and_debit_closing_balance()
    {
        var text = ":25:/PL1\n:60F:D261101PLN100,00\n:61:2611051105RC10,00NTRFNONREF\n:86:storno uznania\n:61:2611061106RD5,00NTRFNONREF\n:86:storno obciążenia\n:62F:D261106PLN105,00\n";
        var r = new Mt940Parser().Parse(text);
        Assert.Equal([-10m, 5m], r.Rows.Select(x => x.Amount));
        Assert.Equal(-100m, r.Opening);
        Assert.Equal(-105m, r.Closing);
    }

    [Fact]
    public void Mt940_malformed_line_is_skipped()
    {
        var r = new Mt940Parser().Parse(":61:to nie jest linia\n:86:x\n:61:2611051105C1,00NTRFNONREF\n:86:ok\n");
        Assert.Single(r.Rows);
        Assert.Equal(1, r.Skipped);
    }

    [Fact]
    public void Elixir_file_direction_from_own_account_and_polish_titles()
    {
        var r = new ElixirParser(Own).Parse(File("wyciag.elixir"));
        Assert.Equal(3, r.Rows.Count);
        Assert.Equal(1, r.Skipped);                                             // operacja niedotycząca rachunku firmy
        Assert.Equal([1230.00m, -15.50m, -200.00m], r.Rows.Select(x => x.Amount));
        Assert.Equal(new DateTime(2026, 11, 5), r.Rows[0].Date);
        Assert.Equal("Zapłata za FV 2026/0001 usługa księgowa", r.Rows[0].Title);
        Assert.Equal("Zażółć Gęślą Jaźń sp. z o.o. ul. Długa 1 Łódź", r.Rows[0].Counterparty);   // wpływ – kontrahent to zleceniodawca
        Assert.Equal("Dostawca SA ul. Handlowa 5", r.Rows[2].Counterparty);                      // wypłata – kontrahent to odbiorca
        Assert.Equal("22223333444455556666777788", r.Rows[2].CounterpartyAccount);
    }

    [Fact]
    public void Elixir_accepts_formatted_own_account_and_requires_it()
    {
        Assert.Equal(3, new ElixirParser("PL 11 1140 2004 0000 3002 0135 5387").Parse(File("wyciag.elixir")).Rows.Count);
        var e = Assert.Throws<DomainValidationException>(() => new ElixirParser(null).Parse(File("wyciag.elixir")));
        Assert.Contains("numer rachunku firmy", e.Message);
    }

    [Fact]
    public void Csv_header_after_preamble_with_hash_prefixed_names()
    {
        var csv = "mBank S.A. Bankowość Detaliczna\n#Rachunek;11 1140 2004 0000 3002 0135 5387\n\n#Data operacji;#Opis operacji;#Rachunek;#Kategoria;#Kwota;\n"
                  + "2026-11-05;Zapłata FV/2026/1;94 1090 1014 0000 0712 1981 2874;Wpływy;1 230,00;\n2026-11-06;Opłata;;Opłaty;-15,50;\nzła;linia;;;;\n";
        var r = new CsvParser(CsvProfile.Mbank).Parse(csv);
        Assert.Equal([1230m, -15.5m], r.Rows.Select(x => x.Amount));
        Assert.Equal("Zapłata FV/2026/1", r.Rows[0].Title);
        Assert.Equal("94109010140000071219812874", r.Rows[0].CounterpartyAccount);
        Assert.Equal(1, r.Skipped);
    }

    [Fact]
    public void Csv_quoted_fields_with_delimiters_and_ing_profile()
    {
        var csv = "Data transakcji;Data księgowania;Dane kontrahenta;Tytuł;Nr rachunku;Nazwa banku;Szczegóły;Nr transakcji;Kwota transakcji (waluta rachunku);Waluta\n"
                  + "05.11.2026;05.11.2026;\"Kowalski; Jan\";\"Faktura \"\"A\"\" 12\";94109010140000071219812874;ING;;1;-99,99;PLN\n";
        var r = new CsvParser(CsvProfile.Ing).Parse(csv);
        var row = Assert.Single(r.Rows);
        Assert.Equal(-99.99m, row.Amount);
        Assert.Equal("Kowalski; Jan", row.Counterparty);
        Assert.Equal("Faktura \"A\" 12", row.Title);
    }

    [Fact]
    public void Csv_without_header_uses_default_column_order()
    {
        var r = new CsvParser(CsvProfile.Auto).Parse("2026-11-05,100.00,Tytuł,Kontrahent\n2026-11-06,-5,Opłata,Bank");
        Assert.Equal([100m, -5m], r.Rows.Select(x => x.Amount));
        Assert.Equal("Kontrahent", r.Rows[0].Counterparty);
    }
}

public class BankTests : IDisposable
{
    readonly TestDb t = new();
    public void Dispose() => t.Dispose();
    static string File(string name) => System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
    const string Own = "11114020040000300201355387";

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

    async Task<ImportStatement.Result> Import(string content, BankFormat f = BankFormat.Mt940, bool rules = true, string? acc = null, Guid? account = null) =>
        await new ImportStatement.Handler(t.Db, t.Me, t.Audit).Handle(
            new ImportStatement.Command(account ?? (await t.Acc("130")).Id, f, CsvProfile.Auto, content, "wyciag.sta", acc, rules), Ct);

    async Task<Guid> Rule(string text = "Opłata za prowadzenie", string target = "402", RuleDirection dir = RuleDirection.Out) =>
        await new BankRules.SaveHandler(t.Db, t.Audit).Handle(new(null, "Opłata bankowa", text, dir, (await t.Acc(target)).Id, null, true), Ct);

    [Fact]
    public async Task Reimporting_the_same_file_creates_no_second_set_and_no_second_payments()
    {
        var c = await t.AddContractor();
        var sale = await t.Invoice(c, net: 1000m);
        var first = await Import(File("wyciag.mt940"), rules: false);
        Assert.Equal((3, 0), (first.Imported, first.Duplicates));
        var op = first.Rows.First(r => r.Amount == 1230m);
        await new BankTransactions.PostToInvoiceHandler(t.Db, Sender(), t.Audit).Handle(new(op.Id, sale.InvoiceId), Ct);

        var second = await Import(File("wyciag.mt940"), rules: false);
        Assert.Equal((0, 3), (second.Imported, second.Duplicates));
        Assert.All(second.Rows, r => Assert.True(r.IsDuplicate));
        Assert.Equal(BankTransactionStatus.Posted, second.Rows.First(r => r.Amount == 1230m).Status);
        Assert.Equal(3, await t.Db.BankTransactions.CountAsync());
        Assert.Equal(1, await t.Db.Payments.CountAsync());
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            new BankTransactions.PostToInvoiceHandler(t.Db, Sender(), t.Audit).Handle(new(op.Id, sale.InvoiceId), Ct));
    }

    [Fact]
    public async Task Overlapping_statements_import_only_new_operations_and_identical_rows_stay_separate()
    {
        var csv = "Data;Kwota;Tytuł\n2026-11-05;-10,00;Opłata\n2026-11-05;-10,00;Opłata\n2026-11-06;-3,00;Inna";
        var a = await Import(csv, BankFormat.Csv, rules: false);
        Assert.Equal(3, a.Imported);                               // dwie identyczne operacje tego samego dnia to dwie operacje
        var b = await Import(csv + "\n2026-11-07;50,00;Nowa", BankFormat.Csv, rules: false);
        Assert.Equal((1, 3), (b.Imported, b.Duplicates));
    }

    [Fact]
    public async Task Same_operation_on_a_different_bank_account_is_not_a_duplicate()
    {
        var other = await t.Acc("100");   // kasa
        await Import(File("wyciag.mt940"), rules: false);
        var r = await Import(File("wyciag.mt940"), rules: false, account: other.Id);
        Assert.Equal(3, r.Imported);
    }

    [Fact]
    public async Task Database_enforces_unique_hash_per_account()
    {
        await Import(File("wyciag.mt940"), rules: false);
        var existing = await t.Db.BankTransactions.AsNoTracking().FirstAsync();
        t.Db.BankTransactions.Add(new BankTransaction(existing.StatementId, existing.AccountId, existing.Date, 5m, "x", "", "", existing.Hash));
        await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Bank_fee_rule_books_to_cost_account_without_invoice()
    {
        await Rule();
        var r = await Import(File("wyciag.mt940"));
        Assert.Equal(1, r.RulesPosted);
        var fee = r.Rows.Single(x => x.Amount == -15.50m);
        Assert.Equal(BankTransactionStatus.Posted, fee.Status);
        Assert.Null(fee.InvoiceId);
        Assert.Equal(15.50m, await t.Net("402"));
        Assert.Equal(-15.50m, await t.Net("130"));
        var tx = await t.Db.Transactions.SingleAsync();
        Assert.Equal(new DateTime(2026, 11, 6), tx.Date);
        Assert.Equal(0, await t.Db.Invoices.CountAsync());
        // pozostałe operacje czekają na zaksięgowanie
        Assert.Equal(2, r.Rows.Count(x => x.Status == BankTransactionStatus.New));
    }

    [Fact]
    public async Task Rule_direction_and_text_are_respected_and_inflows_debit_the_bank()
    {
        await Rule("Zapłata za FV", "700", RuleDirection.In);
        await Rule("licencja", "402", RuleDirection.In);              // zły kierunek – wypłata nie pasuje do reguły „wpływ”
        var r = await Import(File("wyciag.mt940"));
        Assert.Equal(1, r.RulesPosted);
        Assert.Equal(1230m, await t.Net("130"));
        Assert.Equal(-1230m, await t.Net("700"));
        Assert.Equal(BankTransactionStatus.New, r.Rows.Single(x => x.Amount == -200m).Status);
    }

    [Fact]
    public async Task Rule_in_closed_period_is_reported_and_operation_stays_new()
    {
        await Rule();
        var row = new PeriodLock();
        row.Set(new DateTime(2026, 11, 30));
        t.Db.PeriodLocks.Add(row);
        await t.Db.SaveChangesAsync(Ct);
        var r = await Import(File("wyciag.mt940"));
        Assert.Equal(0, r.RulesPosted);
        Assert.Contains("zamknięty", Assert.Single(r.RuleErrors));
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Rules_can_be_applied_later_to_new_operations()
    {
        var r = await Import(File("wyciag.mt940"), rules: false);
        Assert.Equal(0, r.RulesPosted);
        await Rule();
        var applied = await new ApplyBankRules.Handler(t.Db, t.Me, t.Audit).Handle(new((await t.Acc("130")).Id), Ct);
        Assert.Equal(1, applied.Posted);
        var again = await new ApplyBankRules.Handler(t.Db, t.Me, t.Audit).Handle(new((await t.Acc("130")).Id), Ct);
        Assert.Equal(0, again.Posted);
    }

    [Fact]
    public async Task Rule_validation()
    {
        var h = new BankRules.SaveHandler(t.Db, t.Audit);
        var acc = (await t.Acc("402")).Id;
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(null, "R", "ab", RuleDirection.Any, acc, null, true), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(null, "R", "tekst", RuleDirection.Any, Guid.NewGuid(), null, true), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new(null, "", "tekst", RuleDirection.Any, acc, null, true), Ct));
    }

    [Fact]
    public async Task Posting_to_invoice_checks_direction_and_skipped_operations()
    {
        var c = await t.AddContractor();
        var sale = await t.Invoice(c, net: 1000m);
        var r = await Import(File("wyciag.mt940"), rules: false);
        var inflow = r.Rows.First(x => x.Amount == 1230m);
        var outflow = r.Rows.First(x => x.Amount == -200m);
        var post = new BankTransactions.PostToInvoiceHandler(t.Db, Sender(), t.Audit);
        var e = await Assert.ThrowsAsync<DomainValidationException>(() => post.Handle(new(outflow.Id, sale.InvoiceId), Ct));
        Assert.Contains("faktury zakupu", e.Message);

        var skip = new BankTransactions.SetSkippedHandler(t.Db, t.Audit);
        await skip.Handle(new(inflow.Id, true), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => post.Handle(new(inflow.Id, sale.InvoiceId), Ct));
        await skip.Handle(new(inflow.Id, false), Ct);
        Assert.Equal(1230m, await post.Handle(new(inflow.Id, sale.InvoiceId), Ct));
        Assert.Equal(BankTransactionStatus.Posted, (await t.Db.BankTransactions.FindAsync(inflow.Id))!.Status);
    }

    [Fact]
    public async Task Reconciliation_shows_difference_with_unposted_list_and_matches_after_posting()
    {
        await t.Post("Kapitał", "130", "800", 10000m, new DateTime(2026, 11, 1));
        await Rule();
        var c = await t.AddContractor();
        var sale = await t.Invoice(c, net: 1000m, issue: new DateTime(2026, 11, 2));
        var purchase = await t.Invoice(c, InvoiceType.Purchase, net: 162.60m, number: "FZ/77/2026", issue: new DateTime(2026, 11, 2));
        var r = await Import(File("wyciag.mt940"));        // opłata zaksięgowana regułą; zostają +1230 i −200
        var h = new BankReconciliation.Handler(t.Db);
        var before = await h.Handle(new((await t.Acc("130")).Id, null, null), Ct);
        Assert.Equal(9984.50m, before.LedgerBalance);
        Assert.Equal(11014.50m, before.StatementBalance);
        Assert.Equal(new DateTime(2026, 11, 7), before.AsOf);
        Assert.False(before.Matches);
        Assert.Equal(-1030m, before.Difference);
        Assert.Equal(2, before.Unposted.Count);
        Assert.Equal(1030m, before.UnpostedSum);
        Assert.Equal(0m, before.DifferenceAfterUnposted);               // różnica w całości to niezaksięgowane operacje

        var post = new BankTransactions.PostToInvoiceHandler(t.Db, Sender(), t.Audit);
        await post.Handle(new(r.Rows.First(x => x.Amount == 1230m).Id, sale.InvoiceId), Ct);
        await post.Handle(new(r.Rows.First(x => x.Amount == -200m).Id, purchase.InvoiceId), Ct);
        var after = await h.Handle(new((await t.Acc("130")).Id, null, null), Ct);
        Assert.True(after.Matches);
        Assert.Equal(0m, after.Difference);
        Assert.Empty(after.Unposted);

        var manual = await h.Handle(new((await t.Acc("130")).Id, new DateTime(2026, 11, 7), 11000m), Ct);
        Assert.Equal(14.50m, manual.Difference);
        Assert.Equal("wpisane ręcznie", manual.StatementSource);
    }

    [Fact]
    public async Task Without_closing_balance_there_is_no_difference_only_ledger_balance()
    {
        var r = await new BankReconciliation.Handler(t.Db).Handle(new((await t.Acc("130")).Id, new DateTime(2026, 11, 7), null), Ct);
        Assert.Null(r.Difference);
        Assert.False(r.Matches);
        Assert.Equal("brak", r.StatementSource);
    }

    [Fact]
    public async Task Import_validation_errors_are_in_polish()
    {
        var e = await Assert.ThrowsAsync<DomainValidationException>(() => Import("to nie jest wyciąg"));
        Assert.Contains("Nie znaleziono żadnych operacji", e.Message);
        var revenue = (await t.Acc("700")).Id;
        var notBank = await Assert.ThrowsAsync<DomainValidationException>(() => Import(File("wyciag.mt940"), account: revenue));
        Assert.Contains("kasa/bank", notBank.Message);
        var noOwn = await Assert.ThrowsAsync<DomainValidationException>(() => Import(File("wyciag.elixir"), BankFormat.Elixir));
        Assert.Contains("numer rachunku firmy", noOwn.Message);
        Assert.Equal(0, await t.Db.BankTransactions.CountAsync());
    }

    [Fact]
    public async Task Elixir_import_end_to_end()
    {
        var r = await Import(File("wyciag.elixir"), BankFormat.Elixir, rules: false, acc: Own);
        Assert.Equal((3, 0, 1), (r.Imported, r.Duplicates, r.Skipped));
        Assert.Null(r.ClosingBalance);
    }

    [Fact]
    public async Task Data_and_rules_are_isolated_per_company()
    {
        await Rule();
        await Import(File("wyciag.mt940"), rules: false);
        t.Use(t.CompanyB);
        Assert.Empty(await new BankTransactions.ListHandler(t.Db).Handle(new(null, null, null), Ct));
        Assert.Empty(await new BankRules.ListHandler(t.Db).Handle(new BankRules.ListQuery(), Ct));
        // ten sam plik w drugiej firmie to nowy import (osobne rachunki i skróty)
        var again = await Import(File("wyciag.mt940"), rules: false);
        Assert.Equal(3, again.Imported);
        t.Use(t.CompanyA);
        Assert.Equal(3, (await new BankTransactions.ListHandler(t.Db).Handle(new(null, null, null), Ct)).Count);
    }
}
