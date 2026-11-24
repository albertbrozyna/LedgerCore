using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Features.Collections;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using static LedgerCore.Application.Tests.TestDb;

namespace LedgerCore.Application.Tests;

public class FakeProtector : ISecretProtector
{
    public string Protect(string plain) => "enc:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plain));
    public string? Unprotect(string protectedValue) => protectedValue.StartsWith("enc:") ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue[4..])) : null;
}

public class FakeMailer : IMailer
{
    public List<(SmtpSettingsSnapshot Smtp, OutgoingMail Mail)> Sent { get; } = [];
    public string? FailWith { get; set; }
    public Task Send(SmtpSettingsSnapshot smtp, OutgoingMail mail, CancellationToken ct)
    {
        if (FailWith != null) throw new MailDeliveryException(FailWith);
        Sent.Add((smtp, mail));
        return Task.CompletedTask;
    }
}

/// <summary>Czyste testy wzoru odsetek (ręcznie policzone przykłady).</summary>
public class InterestCalculatorTests
{
    static readonly DateTime Jan1 = new(2025, 1, 1);
    static List<(DateTime, decimal)> Rate(decimal pct) => [(new DateTime(2020, 1, 1), pct)];

    [Fact]
    public void Thousand_zloty_45_days_at_11_5_percent_is_14_18()
    {
        // 1000 × 11,5% × 45 / 365 = 14,178… → 14,18 (zwłoka od 2 stycznia do 15 lutego włącznie = 45 dni)
        var r = InterestCalculator.Compute(1000m, [], Jan1, new DateTime(2025, 2, 15), Rate(11.5m));
        Assert.Equal(45, r.Days);
        Assert.Equal(14.18m, r.Interest);
        Assert.Equal("11,50%", r.RateInfo);
    }

    [Fact]
    public void Rate_change_splits_the_period_by_effective_dates()
    {
        // 19 dni po 10% (2–20 I) i 10 dni po 12% (21–30 I): (1000×0,10×19 + 1000×0,12×10)/365 = 8,4931… → 8,49
        var rates = new List<(DateTime, decimal)> { (new DateTime(2020, 1, 1), 10m), (new DateTime(2025, 1, 21), 12m) };
        var r = InterestCalculator.Compute(1000m, [], Jan1, new DateTime(2025, 1, 30), rates);
        Assert.Equal(29, r.Days);
        Assert.Equal(8.49m, r.Interest);
        Assert.Equal("10,00% → 12,00%", r.RateInfo);
    }

    [Fact]
    public void Partial_payment_reduces_the_base_from_the_day_after_payment()
    {
        // 10 dni od kwoty 1000 (2–11 I, dzień wpłaty jest jeszcze oprocentowany) i 9 dni od 600 (12–20 I): (10000+5400)×0,10/365 = 4,2191… → 4,22
        var r = InterestCalculator.Compute(1000m, [(new DateTime(2025, 1, 11), 400m)], Jan1, new DateTime(2025, 1, 20), Rate(10m));
        Assert.Equal(19, r.Days);
        Assert.Equal(4.22m, r.Interest);
    }

    [Fact]
    public void Interest_stops_when_fully_paid_and_is_zero_when_not_overdue()
    {
        var paid = InterestCalculator.Compute(500m, [(new DateTime(2025, 1, 6), 500m)], Jan1, new DateTime(2025, 3, 1), Rate(10m));
        Assert.Equal(5, paid.Days);                                  // 2–6 I
        Assert.Equal(0.68m, paid.Interest);                          // 500×0,10×5/365 = 0,6849… → 0,68
        Assert.Equal(0m, InterestCalculator.Compute(500m, [], Jan1, Jan1, Rate(10m)).Interest);
        Assert.Equal(0, InterestCalculator.Compute(500m, [], Jan1, new DateTime(2024, 12, 1), Rate(10m)).Days);
    }

    [Fact]
    public void Prepayment_before_due_date_reduces_the_base_from_the_first_day()
    {
        var r = InterestCalculator.Compute(1000m, [(new DateTime(2024, 12, 20), 1000m)], Jan1, new DateTime(2025, 2, 1), Rate(10m));
        Assert.Equal(0m, r.Interest);
    }

    [Fact]
    public void Missing_rate_for_the_period_is_a_clear_error()
    {
        var ex = Assert.Throws<DomainValidationException>(() => InterestCalculator.Compute(100m, [], Jan1, new DateTime(2025, 2, 1), []));
        Assert.Contains("tabelę stóp", ex.Message);
        var late = new List<(DateTime, decimal)> { (new DateTime(2025, 1, 15), 10m) };       // stopa obowiązuje dopiero od 15 stycznia
        var ex2 = Assert.Throws<DomainValidationException>(() => InterestCalculator.Compute(100m, [], Jan1, new DateTime(2025, 2, 1), late));
        Assert.Contains("2025-01-02", ex2.Message);
    }
}

public class CollectionsTests : IDisposable
{
    readonly TestDb t = new();
    readonly FakeMailer mailer = new();
    readonly FakeProtector protector = new();
    static readonly DateTime AsOf = new(2025, 5, 8);        // 45 dni po terminie 24.03.2025 (faktura z 10.03 + 14 dni)

    public void Dispose() => t.Dispose();

    async Task Rate(decimal pct = 11.5m) =>
        await new InterestRates.AddHandler(t.Db, t.Audit).Handle(new(new DateTime(2020, 1, 1), pct, "test"), Ct);

    async Task Smtp(string? password = "sekret") =>
        await new MailConfig.SaveHandler(t.Db, protector, t.Audit).Handle(new("smtp.test", 2525, false, "user", password, "biuro@firma.test", "Biuro", null, null, null, null), Ct);

    async Task<(Guid contractor, CreateInvoice.Result inv)> Overdue1000(string name = "Dłużnik", string? email = null)
    {
        var id = await new LedgerCore.Application.Features.Contractors.SaveContractor.Handler(t.Db, t.Audit)
            .Handle(new(null, name, null, null, email), Ct);
        return (id, await t.Invoice(id, net: 1000m, rate: VatRate.R0));
    }

    NoticeBuilder.Request Req(Guid c, NoticeKind kind = NoticeKind.Reminder, bool interest = true, List<Guid>? ids = null) => new(c, ids, kind, AsOf, 7, interest);

    // ---- kryteria akceptacji ----

    [Fact]
    public async Task Interest_for_thousand_zloty_45_days_matches_the_manual_example()
    {
        await Rate();
        var (c, _) = await Overdue1000();
        var draft = await new PreviewNotice.Handler(t.Db).Handle(new(Req(c)), Ct);
        var line = Assert.Single(draft.Lines);
        Assert.Equal(45, line.Days);
        Assert.Equal(14.18m, line.Interest);
        Assert.Equal(1000m, draft.Principal);
        Assert.Equal(1014.18m, draft.Total);
        Assert.Equal(new DateTime(2025, 5, 15), draft.Deadline);
    }

    [Fact]
    public async Task Reminder_covers_only_overdue_unpaid_non_canceled_non_correction_sales_invoices()
    {
        await Rate();
        var c = await t.AddContractor("K");
        var overdue = await t.Invoice(c, net: 100m);                                            // po terminie
        var paid = await t.Invoice(c, net: 100m); await t.Pay(paid.InvoiceId, 123m);            // opłacona
        var canceled = await t.Invoice(c, net: 100m);
        await new CancelInvoice.Handler(t.Db, t.Me, t.Audit).Handle(new(canceled.InvoiceId), Ct);
        var correction = await new CreateCorrection.Handler(t.Db, t.Me, t.Audit).Handle(
            new(overdue.InvoiceId, null, new DateTime(2025, 3, 12), null, [new("Rabat", -10m, VatRate.R23)]), Ct);
        await t.Invoice(c, net: 100m, issue: new DateTime(2025, 5, 1));                         // termin jeszcze nie minął
        await t.Invoice(c, InvoiceType.Purchase, 100m, number: "ZAK/9");                        // zakup nie jest należnością

        var draft = await new PreviewNotice.Handler(t.Db).Handle(new(Req(c)), Ct);
        var line = Assert.Single(draft.Lines);
        Assert.Equal(overdue.InvoiceId, line.InvoiceId);
        Assert.NotEqual(correction.InvoiceId, line.InvoiceId);
        Assert.Equal(110.7m, line.Principal);                      // 123 − 12,30 korekty
    }

    [Fact]
    public async Task No_overdue_invoices_is_a_clear_error()
    {
        await Rate();
        var c = await t.AddContractor("K");
        await t.Invoice(c, net: 100m, issue: new DateTime(2025, 5, 1));
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new PreviewNotice.Handler(t.Db).Handle(new(Req(c)), Ct));
        Assert.Contains("Brak faktur po terminie", ex.Message);
    }

    [Fact]
    public async Task Smtp_password_is_stored_encrypted_and_never_returned()
    {
        await Smtp("tajne-haslo");
        var stored = await t.Db.MailSettings.AsNoTracking().SingleAsync();
        Assert.NotNull(stored.ProtectedPassword);
        Assert.DoesNotContain("tajne-haslo", stored.ProtectedPassword);
        var dto = await new MailConfig.GetHandler(t.Db).Handle(new(), Ct);
        Assert.True(dto.HasPassword);
        Assert.DoesNotContain("tajne-haslo", System.Text.Json.JsonSerializer.Serialize(dto));

        // brak hasła w poleceniu = bez zmian; pusty tekst = usunięcie
        await Smtp(null);
        Assert.True((await new MailConfig.GetHandler(t.Db).Handle(new(), Ct)).HasPassword);
        await Smtp("");
        Assert.False((await new MailConfig.GetHandler(t.Db).Handle(new(), Ct)).HasPassword);
    }

    [Fact]
    public async Task Misconfigured_smtp_gives_clear_messages()
    {
        var test = new MailConfig.TestHandler(t.Db, protector, mailer, t.Audit);
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => test.Handle(new("ktos@test.pl"), Ct));
        Assert.Contains("nie jest skonfigurowana", ex.Message);

        await Smtp();
        mailer.FailWith = "Nie można połączyć się z serwerem poczty smtp.test:2525 – sprawdź adres, port i połączenie z internetem";
        var ex2 = await Assert.ThrowsAsync<MailDeliveryException>(() => test.Handle(new("ktos@test.pl"), Ct));
        Assert.Contains("smtp.test:2525", ex2.Message);
        Assert.DoesNotContain("sekret", ex2.Message);

        mailer.FailWith = null;
        await test.Handle(new("ktos@test.pl"), Ct);
        Assert.Single(mailer.Sent);
        Assert.Equal("sekret", mailer.Sent[0].Smtp.Password);       // do mailera trafia odszyfrowane hasło
    }

    [Fact]
    public async Task Unreadable_stored_password_asks_to_enter_it_again()
    {
        await Smtp();
        (await t.Db.MailSettings.SingleAsync()).SetProtectedPassword("uszkodzone");
        await t.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new MailConfig.TestHandler(t.Db, protector, mailer, t.Audit).Handle(new("a@b.pl"), Ct));
        Assert.Contains("wpisz hasło SMTP ponownie", ex.Message);
    }

    // ---- wystawianie i historia ----

    IssueNotice.Handler Issuer => new(t.Db, t.Me, t.Audit, protector, mailer);

    [Fact]
    public async Task Print_notice_is_recorded_with_snapshot_and_appears_in_history_of_invoice_and_contractor()
    {
        await Rate();
        var (c, inv) = await Overdue1000();
        var r = await Issuer.Handle(new(Req(c), NoticeChannel.Print, null, null, null), Ct);
        Assert.Equal(NoticeStatus.Issued, r.Status);

        Assert.Single(await new ListNotices.Handler(t.Db).Handle(new(c, null), Ct));
        var byInvoice = await new ListNotices.Handler(t.Db).Handle(new(null, inv.InvoiceId), Ct);
        Assert.Equal(r.Id, Assert.Single(byInvoice).Id);
        Assert.True(await t.Db.AuditEntries.AnyAsync(a => a.Action == "Windykacja" && a.Details.Contains("wydruk")));

        // migawka: po zapłacie dokument nadal pokazuje to, co wystawiono
        await t.Pay(inv.InvoiceId, 1000m);
        var doc = await new GetNotice.Handler(t.Db).Handle(new(r.Id), Ct);
        Assert.Equal(1000m, doc.Principal);
        Assert.Equal(14.18m, doc.Interest);
        Assert.Equal(inv.Number, Assert.Single(doc.Lines).Number);
        Assert.Equal("Dłużnik", doc.Recipient.Name);
        Assert.Equal("Firma A", doc.Issuer.Name);
    }

    [Fact]
    public async Task Email_notice_is_sent_with_html_and_text_and_marked_sent()
    {
        await Rate(); await Smtp();
        var (c, _) = await Overdue1000("Dłużnik <script>x</script>", "dluznik@test.pl");
        var r = await Issuer.Handle(new(Req(c), NoticeChannel.Email, null, null, null), Ct);
        Assert.Equal(NoticeStatus.Sent, r.Status);
        var (smtp, mail) = Assert.Single(mailer.Sent);
        Assert.Equal("dluznik@test.pl", mail.To);
        Assert.Equal("smtp.test", smtp.Host);
        Assert.Contains("Wezwanie do zapłaty", mail.Subject);
        Assert.Contains("1000,00 zł", mail.TextBody.Replace(" ", ""));
        Assert.Contains("<table", mail.HtmlBody);
        Assert.DoesNotContain("<script>", mail.HtmlBody);                              // dane są kodowane
        var n = await t.Db.CollectionNotices.AsNoTracking().SingleAsync();
        Assert.Equal("dluznik@test.pl", n.SentTo);
    }

    [Fact]
    public async Task Failed_delivery_is_recorded_with_error_and_does_not_throw()
    {
        await Rate(); await Smtp();
        mailer.FailWith = "Serwer poczty odrzucił login lub hasło – sprawdź dane logowania SMTP";
        var (c, _) = await Overdue1000(email: "d@test.pl");
        var r = await Issuer.Handle(new(Req(c), NoticeChannel.Email, null, null, null), Ct);
        Assert.Equal(NoticeStatus.Failed, r.Status);
        Assert.Contains("login lub hasło", r.Error);
        var item = Assert.Single(await new ListNotices.Handler(t.Db).Handle(new(c, null), Ct));
        Assert.Equal(NoticeStatus.Failed, item.Status);
        Assert.True(await t.Db.AuditEntries.AnyAsync(a => a.Details.Contains("Nie udało się wysłać")));
    }

    [Fact]
    public async Task Email_requires_recipient_and_configured_smtp_and_creates_no_notice_on_validation_errors()
    {
        await Rate();
        var (c, _) = await Overdue1000();                                              // kontrahent bez e-maila
        var noSmtp = await Assert.ThrowsAsync<DomainValidationException>(() => Issuer.Handle(new(Req(c), NoticeChannel.Email, "x@y.pl", null, null), Ct));
        Assert.Contains("nie jest skonfigurowana", noSmtp.Message);
        await Smtp();
        var noTo = await Assert.ThrowsAsync<DomainValidationException>(() => Issuer.Handle(new(Req(c), NoticeChannel.Email, null, null, null), Ct));
        Assert.Contains("adresu e-mail", noTo.Message);
        Assert.Equal(0, await t.Db.CollectionNotices.CountAsync());
        var ok = await Issuer.Handle(new(Req(c), NoticeChannel.Email, "reczny@test.pl", "Własny temat", "Własna treść"), Ct);
        Assert.Equal(NoticeStatus.Sent, ok.Status);
        Assert.Equal(("reczny@test.pl", "Własny temat"), (mailer.Sent[0].Mail.To, mailer.Sent[0].Mail.Subject));
        Assert.Equal("Własna treść", mailer.Sent[0].Mail.TextBody);
    }

    [Fact]
    public async Task Interest_note_contains_only_interest_and_needs_rates()
    {
        var (c, _) = await Overdue1000();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new PreviewNotice.Handler(t.Db).Handle(new(Req(c, NoticeKind.InterestNote)), Ct));
        Assert.Contains("tabelę stóp", ex.Message);

        await Rate();
        var draft = await new PreviewNotice.Handler(t.Db).Handle(new(Req(c, NoticeKind.InterestNote)), Ct);
        Assert.Equal(14.18m, draft.Total);                      // do zapłaty tylko odsetki
        Assert.Contains("Nota odsetkowa", draft.Subject);
        Assert.Contains("14,18", draft.Body);
        // wezwanie bez odsetek nie wymaga tabeli stóp
        var plain = await new PreviewNotice.Handler(t.Db).Handle(new(Req(c, interest: false)), Ct);
        Assert.Equal(0m, plain.Interest);
        Assert.Equal(1000m, plain.Total);
    }

    [Fact]
    public async Task Custom_templates_are_rendered_with_variables()
    {
        await Rate();
        await new MailConfig.SaveHandler(t.Db, protector, t.Audit).Handle(
            new("h", 25, false, null, null, "a@b.pl", null, "Pilne: {kontrahent}", "Zapłać {razem} do {termin} ({faktury})", null, null), Ct);
        var (c, inv) = await Overdue1000();
        var d = await new PreviewNotice.Handler(t.Db).Handle(new(Req(c)), Ct);
        Assert.Equal("Pilne: Dłużnik", d.Subject);
        Assert.StartsWith("Zapłać 1", d.Body);
        Assert.Contains("2025-05-15", d.Body);
        Assert.Contains(inv.Number, d.Body);
    }

    [Fact]
    public async Task Overdue_list_has_interest_or_explains_why_not()
    {
        var (c, inv) = await Overdue1000();
        var noRates = await new ListOverdue.Handler(t.Db).Handle(new(AsOf, null), Ct);
        Assert.NotNull(noRates.InterestError);
        Assert.Null(Assert.Single(noRates.Items).Interest);

        await Rate();
        var withRates = await new ListOverdue.Handler(t.Db).Handle(new(AsOf, c), Ct);
        Assert.Null(withRates.InterestError);
        var item = Assert.Single(withRates.Items);
        Assert.Equal(14.18m, item.Interest);
        Assert.Equal(45, item.DaysOverdue);
        Assert.Equal(inv.InvoiceId, item.InvoiceId);
    }

    // ---- stopy odsetek ----

    [Fact]
    public async Task Interest_rates_validate_range_and_uniqueness_and_can_be_deleted()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => new InterestRates.AddHandler(t.Db, t.Audit).Handle(new(new DateTime(2025, 1, 1), 101m, null), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => new InterestRates.AddHandler(t.Db, t.Audit).Handle(new(new DateTime(2025, 1, 1), -1m, null), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => new InterestRates.AddHandler(t.Db, t.Audit).Handle(new(new DateTime(2025, 1, 1), 10.123m, null), Ct));
        var id = await new InterestRates.AddHandler(t.Db, t.Audit).Handle(new(new DateTime(2025, 1, 1), 10m, null), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => new InterestRates.AddHandler(t.Db, t.Audit).Handle(new(new DateTime(2025, 1, 1), 11m, null), Ct));
        await new InterestRates.DeleteHandler(t.Db, t.Audit).Handle(new(id), Ct);
        Assert.Empty(await new InterestRates.ListHandler(t.Db).Handle(new(), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => new InterestRates.DeleteHandler(t.Db, t.Audit).Handle(new(id), Ct));
    }

    // ---- izolacja firm ----

    [Fact]
    public async Task Notices_settings_and_contractors_are_isolated_per_company()
    {
        await Rate(); await Smtp();
        var (c, inv) = await Overdue1000();
        await Issuer.Handle(new(Req(c), NoticeChannel.Print, null, null, null), Ct);

        t.Use(t.CompanyB);
        Assert.Empty(await new ListNotices.Handler(t.Db).Handle(new(null, null), Ct));
        Assert.Empty(await new ListNotices.Handler(t.Db).Handle(new(null, inv.InvoiceId), Ct));
        Assert.False((await new MailConfig.GetHandler(t.Db).Handle(new(), Ct)).Configured);              // ustawienia poczty są per firma
        await Assert.ThrowsAsync<DomainValidationException>(() => new PreviewNotice.Handler(t.Db).Handle(new(Req(c)), Ct));   // obcy kontrahent
        Assert.Single(await new InterestRates.ListHandler(t.Db).Handle(new(), Ct));                      // stopy ustawowe są wspólne
    }
}
