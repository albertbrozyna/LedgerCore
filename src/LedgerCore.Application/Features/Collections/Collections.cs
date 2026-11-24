using System.Net;
using System.Text;
using System.Text.Json;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Collections;

// ======================= stopy odsetek (administracja, wspólne dla instalacji) =======================

public static class InterestRates
{
    public record Item(Guid Id, DateTime ValidFrom, decimal AnnualPercent, string? Note);

    public record ListQuery : IRequest<List<Item>>;
    public class ListHandler(IAppDbContext db) : IRequestHandler<ListQuery, List<Item>>
    {
        public async Task<List<Item>> Handle(ListQuery r, CancellationToken ct) =>
            (await db.InterestRates.AsNoTracking().OrderByDescending(x => x.ValidFrom).ToListAsync(ct))
            .Select(x => new Item(x.Id, x.ValidFrom, x.AnnualPercent, x.Note)).ToList();
    }

    public record AddCommand(DateTime ValidFrom, decimal AnnualPercent, string? Note) : IRequest<Guid>;
    public class AddValidator : AbstractValidator<AddCommand>
    {
        public AddValidator()
        {
            RuleFor(x => x.ValidFrom).Must(d => d.Year is >= 1990 and <= 2100).WithMessage("Nieprawidłowa data obowiązywania stopy");
            RuleFor(x => x.Note).MaximumLength(200);
        }
    }
    public class AddHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<AddCommand, Guid>
    {
        public async Task<Guid> Handle(AddCommand r, CancellationToken ct)
        {
            var rate = new InterestRate(r.ValidFrom, r.AnnualPercent, r.Note);
            if (await db.InterestRates.AnyAsync(x => x.ValidFrom == rate.ValidFrom, ct))
                throw new DomainValidationException($"Stopa obowiązująca od {rate.ValidFrom:yyyy-MM-dd} już istnieje – usuń ją lub wybierz inną datę");
            db.InterestRates.Add(rate);
            await audit.Add("Stopy odsetek", $"Dodano stopę {rate.AnnualPercent:0.00}% obowiązującą od {rate.ValidFrom:yyyy-MM-dd}", ct);
            await db.SaveChangesAsync(ct);
            return rate.Id;
        }
    }

    public record DeleteCommand(Guid Id) : IRequest<Unit>;
    public class DeleteHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<DeleteCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteCommand r, CancellationToken ct)
        {
            var rate = await db.InterestRates.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono stopy odsetek");
            db.InterestRates.Remove(rate);
            await audit.Add("Stopy odsetek", $"Usunięto stopę {rate.AnnualPercent:0.00}% obowiązującą od {rate.ValidFrom:yyyy-MM-dd}", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static async Task<List<(DateTime From, decimal Percent)>> Load(IAppDbContext db, CancellationToken ct) =>
        (await db.InterestRates.AsNoTracking().OrderBy(x => x.ValidFrom).ToListAsync(ct)).Select(x => (x.ValidFrom, x.AnnualPercent)).ToList();
}

// ======================= ustawienia poczty (per firma) =======================

public static class NoticeTemplates
{
    public const string DefaultReminderSubject = "Wezwanie do zapłaty – {kontrahent}";
    public const string DefaultReminderBody =
        "Szanowni Państwo,\n\nz naszej ewidencji wynika, że następujące faktury nie zostały opłacone w terminie: {faktury}.\n" +
        "Kwota zaległości: {kwota}, odsetki za opóźnienie na dzień {data}: {odsetki}, razem do zapłaty: {razem}.\n" +
        "Prosimy o uregulowanie należności do dnia {termin}.\n\nZ poważaniem,\n{firma}";
    public const string DefaultInterestSubject = "Nota odsetkowa – {kontrahent}";
    public const string DefaultInterestBody =
        "Szanowni Państwo,\n\nz powodu opóźnienia w zapłacie faktur ({faktury}) naliczamy odsetki ustawowe za opóźnienie.\n" +
        "Odsetki na dzień {data}: {odsetki}. Prosimy o ich zapłatę do dnia {termin}.\n\nZ poważaniem,\n{firma}";

    public static string Render(string template, IReadOnlyDictionary<string, string> vars)
    {
        var s = template;
        foreach (var (k, v) in vars) s = s.Replace("{" + k + "}", v);
        return s;
    }
}

public static class MailConfig
{
    public record Dto(bool Configured, string Host, int Port, bool UseTls, string? UserName, bool HasPassword, string FromAddress, string? FromName,
        string ReminderSubject, string ReminderBody, string InterestSubject, string InterestBody);

    public record GetQuery : IRequest<Dto>;
    public class GetHandler(IAppDbContext db) : IRequestHandler<GetQuery, Dto>
    {
        public async Task<Dto> Handle(GetQuery r, CancellationToken ct)
        {
            var s = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            // hasło nigdy nie opuszcza serwera – tylko informacja, czy jest ustawione
            return new Dto(s != null && s.Host.Length > 0, s?.Host ?? "", s?.Port ?? 587, s?.UseTls ?? true, s?.UserName, !string.IsNullOrEmpty(s?.ProtectedPassword),
                s?.FromAddress ?? "", s?.FromName,
                s?.ReminderSubject ?? NoticeTemplates.DefaultReminderSubject, s?.ReminderBody ?? NoticeTemplates.DefaultReminderBody,
                s?.InterestSubject ?? NoticeTemplates.DefaultInterestSubject, s?.InterestBody ?? NoticeTemplates.DefaultInterestBody);
        }
    }

    /// <summary>Password: null = bez zmian, pusty tekst = usuń hasło, inna wartość = nowe hasło.</summary>
    public record SaveCommand(string Host, int Port, bool UseTls, string? UserName, string? Password, string FromAddress, string? FromName,
        string? ReminderSubject, string? ReminderBody, string? InterestSubject, string? InterestBody) : IRequest<Unit>;

    public class SaveValidator : AbstractValidator<SaveCommand>
    {
        public SaveValidator()
        {
            RuleFor(x => x.Host).NotEmpty().WithMessage("Podaj adres serwera SMTP").MaximumLength(200);
            RuleFor(x => x.Port).InclusiveBetween(1, 65535).WithMessage("Port musi mieścić się w przedziale 1–65535");
            RuleFor(x => x.FromAddress).NotEmpty().WithMessage("Podaj adres nadawcy").EmailAddress().WithMessage("Nieprawidłowy adres nadawcy").MaximumLength(200);
            RuleFor(x => x.UserName).MaximumLength(200);
            RuleFor(x => x.FromName).MaximumLength(200);
            RuleFor(x => x.ReminderSubject).MaximumLength(200);
            RuleFor(x => x.InterestSubject).MaximumLength(200);
            RuleFor(x => x.ReminderBody).MaximumLength(4000);
            RuleFor(x => x.InterestBody).MaximumLength(4000);
        }
    }

    public class SaveHandler(IAppDbContext db, ISecretProtector protector, AuditLog audit) : IRequestHandler<SaveCommand, Unit>
    {
        public async Task<Unit> Handle(SaveCommand r, CancellationToken ct)
        {
            var s = await db.MailSettings.FirstOrDefaultAsync(ct);
            if (s == null) { s = new MailSettings(Guid.NewGuid()); db.MailSettings.Add(s); }
            s.Update(r.Host, r.Port, r.UseTls, r.UserName, r.FromAddress, r.FromName, r.ReminderSubject, r.ReminderBody, r.InterestSubject, r.InterestBody);
            if (r.Password != null) s.SetProtectedPassword(r.Password.Length == 0 ? null : protector.Protect(r.Password));
            await audit.Add("Poczta", $"Zmieniono ustawienia poczty wychodzącej (serwer {s.Host}:{s.Port})", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static async Task<SmtpSettingsSnapshot> Snapshot(IAppDbContext db, ISecretProtector protector, CancellationToken ct)
    {
        var s = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s == null || s.Host.Length == 0 || s.FromAddress.Length == 0)
            throw new DomainValidationException("Poczta wychodząca nie jest skonfigurowana – uzupełnij ustawienia SMTP w Administracji");
        string? password = null;
        if (!string.IsNullOrEmpty(s.ProtectedPassword))
        {
            password = protector.Unprotect(s.ProtectedPassword);
            if (password == null) throw new DomainValidationException("Nie można odczytać zapisanego hasła poczty (zmieniono komputer lub folder danych) – wpisz hasło SMTP ponownie w Administracji");
        }
        return new SmtpSettingsSnapshot(s.Host, s.Port, s.UseTls, s.UserName, password, s.FromAddress, s.FromName);
    }

    public record TestCommand(string To) : IRequest<Unit>;
    public class TestValidator : AbstractValidator<TestCommand>
    {
        public TestValidator() => RuleFor(x => x.To).NotEmpty().WithMessage("Podaj adres, na który wysłać wiadomość testową").EmailAddress().WithMessage("Nieprawidłowy adres odbiorcy");
    }
    public class TestHandler(IAppDbContext db, ISecretProtector protector, IMailer mailer, AuditLog audit) : IRequestHandler<TestCommand, Unit>
    {
        public async Task<Unit> Handle(TestCommand r, CancellationToken ct)
        {
            var smtp = await Snapshot(db, protector, ct);
            await mailer.Send(smtp, new OutgoingMail(r.To, "LedgerCore – wiadomość testowa", "<p>Konfiguracja poczty wychodzącej działa.</p>", "Konfiguracja poczty wychodzącej działa."), ct);
            await audit.Add("Poczta", $"Wysłano wiadomość testową na adres {r.To}", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

// ======================= wezwania i noty =======================

public record NoticeLine(Guid InvoiceId, string Number, DateTime IssueDate, DateTime DueDate, decimal AmountDue, decimal Principal, int Days, decimal Interest, string RateInfo);

public static class Overdue
{
    /// <summary>Faktury sprzedaży po terminie: nieanulowane, niebędące korektami, z pozostałą kwotą do zapłaty i terminem wcześniejszym niż dzień obliczenia.</summary>
    public static async Task<List<Invoice>> Load(IAppDbContext db, DateTime asOf, Guid? contractorId, IReadOnlyCollection<Guid>? ids, CancellationToken ct)
    {
        var q = db.Invoices.AsNoTracking().Include(i => i.Contractor).Include(i => i.Payments)
            .Where(i => i.Type == InvoiceType.Sales && !i.IsCanceled && i.CorrectedInvoiceId == null && i.DueDate < asOf.Date);
        if (contractorId.HasValue) q = q.Where(i => i.ContractorId == contractorId.Value);
        var list = (await q.ToListAsync(ct)).Where(i => i.Remaining > 0).ToList();
        if (ids is { Count: > 0 }) list = list.Where(i => ids.Contains(i.Id)).ToList();
        return list.OrderBy(i => i.DueDate).ThenBy(i => i.Number).ToList();
    }

    public static NoticeLine Line(Invoice i, DateTime asOf, IReadOnlyList<(DateTime From, decimal Percent)>? rates)
    {
        var due = i.Gross + i.CorrectedAmount;
        if (rates == null) return new NoticeLine(i.Id, i.Number, i.IssueDate, i.DueDate, due, i.Remaining, (asOf.Date - i.DueDate.Date).Days, 0m, "");
        var r = InterestCalculator.Compute(due, i.Payments.Select(p => (p.Date, p.Amount)), i.DueDate, asOf, rates);
        return new NoticeLine(i.Id, i.Number, i.IssueDate, i.DueDate, due, i.Remaining, r.Days, r.Interest, r.RateInfo);
    }
}

/// <summary>Lista faktur po terminie (do wyboru w oknie windykacji) wraz z odsetkami na dzień obliczenia.</summary>
public static class ListOverdue
{
    public record Query(DateTime AsOf, Guid? ContractorId) : IRequest<Response>;
    public record Item(Guid InvoiceId, string Number, Guid ContractorId, string ContractorName, string? ContractorEmail, DateTime IssueDate,
        DateTime DueDate, decimal Gross, decimal Remaining, int DaysOverdue, decimal? Interest);
    /// <summary>InterestError: powód braku odsetek (np. nieuzupełniona tabela stóp) – wtedy Interest jest null.</summary>
    public record Response(List<Item> Items, string? InterestError);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var asOf = r.AsOf.Date;
            var invoices = await Overdue.Load(db, asOf, r.ContractorId, null, ct);
            var rates = await InterestRates.Load(db, ct);
            string? error = null;
            var items = new List<Item>();
            foreach (var i in invoices)
            {
                decimal? interest = null;
                if (error == null)
                {
                    try { interest = Overdue.Line(i, asOf, rates).Interest; }
                    catch (DomainValidationException e) { error = e.Message; }
                }
                items.Add(new Item(i.Id, i.Number, i.ContractorId, i.Contractor!.Name, i.Contractor.Email, i.IssueDate, i.DueDate, i.Gross + i.CorrectedAmount,
                    i.Remaining, (asOf - i.DueDate.Date).Days, interest));
            }
            if (error != null) items = items.Select(x => x with { Interest = null }).ToList();
            return new Response(items, error);
        }
    }
}

public static class NoticeBuilder
{
    public record Request(Guid ContractorId, List<Guid>? InvoiceIds, NoticeKind Kind, DateTime AsOf, int PaymentDays, bool IncludeInterest);

    public record Draft(Guid ContractorId, string ContractorName, string? ContractorEmail, NoticeKind Kind, DateTime AsOf, DateTime Deadline,
        List<NoticeLine> Lines, decimal Principal, decimal Interest, decimal Total, string Subject, string Body);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.ContractorId).NotEmpty().WithMessage("Wybierz kontrahenta");
            RuleFor(x => x.Kind).IsInEnum();
            RuleFor(x => x.PaymentDays).InclusiveBetween(1, 90).WithMessage("Termin zapłaty musi mieścić się w przedziale 1–90 dni");
            RuleFor(x => x.AsOf).Must(d => d.Year is >= 2000 and <= 2100).WithMessage("Nieprawidłowa data obliczenia");
        }
    }

    static string Money(decimal v) => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"));

    public static async Task<Draft> Build(IAppDbContext db, Request r, CancellationToken ct)
    {
        var contractor = await db.Contractors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.ContractorId, ct)
                         ?? throw new DomainValidationException("Nie znaleziono kontrahenta");
        var asOf = r.AsOf.Date;
        var invoices = await Overdue.Load(db, asOf, r.ContractorId, r.InvoiceIds, ct);
        if (invoices.Count == 0)
            throw new DomainValidationException("Brak faktur po terminie dla wybranego kontrahenta – opłacone, anulowane i korekty są pomijane");

        var withInterest = r.Kind == NoticeKind.InterestNote || r.IncludeInterest;
        var rates = withInterest ? await InterestRates.Load(db, ct) : null;
        var lines = invoices.Select(i => Overdue.Line(i, asOf, rates)).ToList();
        if (r.Kind == NoticeKind.InterestNote) lines = lines.Where(l => l.Interest > 0).ToList();
        if (lines.Count == 0) throw new DomainValidationException("Dla wybranych faktur odsetki wynoszą 0,00 zł – nie ma czego naliczać");

        var principal = lines.Sum(l => l.Principal);
        var interest = lines.Sum(l => l.Interest);
        var total = r.Kind == NoticeKind.InterestNote ? interest : principal + interest;
        var deadline = asOf.AddDays(r.PaymentDays);

        var settings = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var companyName = await db.Companies.AsNoTracking().Where(c => c.Id == db.CurrentCompanyId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        var vars = new Dictionary<string, string>
        {
            ["firma"] = companyName, ["kontrahent"] = contractor.Name, ["faktury"] = string.Join(", ", lines.Select(l => l.Number)),
            ["kwota"] = Money(principal) + " zł", ["odsetki"] = Money(interest) + " zł", ["razem"] = Money(total) + " zł",
            ["termin"] = deadline.ToString("yyyy-MM-dd"), ["data"] = asOf.ToString("yyyy-MM-dd"),
        };
        var note = r.Kind == NoticeKind.InterestNote;
        var subject = NoticeTemplates.Render((note ? settings?.InterestSubject : settings?.ReminderSubject) ?? (note ? NoticeTemplates.DefaultInterestSubject : NoticeTemplates.DefaultReminderSubject), vars);
        var body = NoticeTemplates.Render((note ? settings?.InterestBody : settings?.ReminderBody) ?? (note ? NoticeTemplates.DefaultInterestBody : NoticeTemplates.DefaultReminderBody), vars);
        return new Draft(contractor.Id, contractor.Name, contractor.Email, r.Kind, asOf, deadline, lines, principal, interest, total, subject, body);
    }

    /// <summary>Treść HTML wiadomości: tekst (akapity) i tabela pozycji. Wszystkie wartości są kodowane (HtmlEncode).</summary>
    public static string Html(string body, IEnumerable<NoticeLine> lines, NoticeKind kind, decimal principal, decimal interest, decimal total)
    {
        var sb = new StringBuilder("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px\">");
        foreach (var p in body.Replace("\r\n", "\n").Split("\n\n")) sb.Append("<p>").Append(WebUtility.HtmlEncode(p).Replace("\n", "<br>")).Append("</p>");
        sb.Append("<table cellpadding=\"6\" style=\"border-collapse:collapse;border:1px solid #999\"><tr><th>Faktura</th><th>Termin</th><th>Do zapłaty</th><th>Dni zwłoki</th><th>Odsetki</th></tr>");
        foreach (var l in lines)
            sb.Append($"<tr><td>{WebUtility.HtmlEncode(l.Number)}</td><td>{l.DueDate:yyyy-MM-dd}</td><td align=\"right\">{Money(l.Principal)}</td><td align=\"right\">{l.Days}</td><td align=\"right\">{Money(l.Interest)}</td></tr>");
        sb.Append($"<tr><td colspan=\"2\"><b>Razem</b></td><td align=\"right\"><b>{Money(principal)}</b></td><td></td><td align=\"right\"><b>{Money(interest)}</b></td></tr></table>");
        sb.Append($"<p><b>Do zapłaty: {Money(total)} zł</b></p></div>");
        return sb.ToString();
    }
}

public static class PreviewNotice
{
    public record Query(NoticeBuilder.Request Request) : IRequest<NoticeBuilder.Draft>;
    public class Handler(IAppDbContext db) : IRequestHandler<Query, NoticeBuilder.Draft>
    {
        public Task<NoticeBuilder.Draft> Handle(Query q, CancellationToken ct) => NoticeBuilder.Build(db, q.Request, ct);
    }
}

public static class IssueNotice
{
    /// <summary>Subject/Body/To – opcjonalne nadpisanie treści z podglądu (e-mail).</summary>
    public record Command(NoticeBuilder.Request Request, NoticeChannel Channel, string? To, string? Subject, string? Body) : IRequest<Response>;
    public record Response(Guid Id, NoticeStatus Status, string? Error);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Request).NotNull().SetValidator(new NoticeBuilder.RequestValidator());
            RuleFor(x => x.Channel).IsInEnum();
            RuleFor(x => x.To).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.To)).WithMessage("Nieprawidłowy adres odbiorcy");
            RuleFor(x => x.Subject).MaximumLength(300);
            RuleFor(x => x.Body).MaximumLength(4000);
        }
    }

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit, ISecretProtector protector, IMailer mailer) : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command c, CancellationToken ct)
        {
            var draft = await NoticeBuilder.Build(db, c.Request, ct);
            var subject = string.IsNullOrWhiteSpace(c.Subject) ? draft.Subject : c.Subject.Trim();
            var body = string.IsNullOrWhiteSpace(c.Body) ? draft.Body : c.Body.Trim();

            SmtpSettingsSnapshot? smtp = null;
            string? to = null;
            if (c.Channel == NoticeChannel.Email)
            {
                to = (string.IsNullOrWhiteSpace(c.To) ? draft.ContractorEmail : c.To)?.Trim();
                if (string.IsNullOrEmpty(to)) throw new DomainValidationException("Kontrahent nie ma adresu e-mail – podaj adres odbiorcy");
                smtp = await MailConfig.Snapshot(db, protector, ct);
            }

            var user = me.User;
            var notice = new CollectionNotice(draft.ContractorId, draft.Kind, c.Channel, user is null ? "system" : $"{user.FirstName} {user.LastName}",
                draft.AsOf, draft.Deadline, draft.Principal, draft.Interest, subject, body,
                JsonSerializer.Serialize(draft.Lines), draft.Lines.Select(l => l.InvoiceId));
            db.CollectionNotices.Add(notice);

            string? error = null;
            if (smtp != null)
            {
                try
                {
                    await mailer.Send(smtp, new OutgoingMail(to!, subject, NoticeBuilder.Html(body, draft.Lines, draft.Kind, draft.Principal, draft.Interest, draft.Total), body), ct);
                    notice.MarkSent(to!);
                }
                catch (MailDeliveryException e) { error = e.Message; notice.MarkFailed(to!, e.Message); }
            }

            var what = draft.Kind == NoticeKind.Reminder ? "wezwanie do zapłaty" : "notę odsetkową";
            await audit.Add("Windykacja", c.Channel == NoticeChannel.Email
                ? $"{(error == null ? "Wysłano e-mailem" : "Nie udało się wysłać e-mailem")} {what} do {draft.ContractorName} ({to}), faktur: {draft.Lines.Count}, razem {draft.Total:0.00}{(error == null ? "" : $" – {error}")}"
                : $"Wystawiono {what} dla {draft.ContractorName} (wydruk), faktur: {draft.Lines.Count}, razem {draft.Total:0.00}", ct);
            await db.SaveChangesAsync(ct);
            return new Response(notice.Id, notice.Status, error);
        }
    }
}

public static class ListNotices
{
    public record Query(Guid? ContractorId, Guid? InvoiceId) : IRequest<List<Item>>;
    public record Item(Guid Id, Guid ContractorId, string ContractorName, NoticeKind Kind, NoticeChannel Channel, NoticeStatus Status, DateTime CreatedAt,
        string CreatedBy, DateTime CalculationDate, decimal Principal, decimal Interest, string? SentTo, string? Error, int InvoiceCount);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, List<Item>>
    {
        public async Task<List<Item>> Handle(Query r, CancellationToken ct)
        {
            var q = db.CollectionNotices.AsNoTracking().Include(n => n.Invoices).AsQueryable();
            if (r.ContractorId.HasValue) q = q.Where(n => n.ContractorId == r.ContractorId.Value);
            if (r.InvoiceId.HasValue) q = q.Where(n => n.Invoices.Any(i => i.InvoiceId == r.InvoiceId.Value));
            var list = (await q.ToListAsync(ct)).OrderByDescending(n => n.CreatedAt).Take(200).ToList();
            var names = await db.Contractors.AsNoTracking().Where(c => list.Select(n => n.ContractorId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
            return list.Select(n => new Item(n.Id, n.ContractorId, names.GetValueOrDefault(n.ContractorId, "?"), n.Kind, n.Channel, n.Status, n.CreatedAt, n.CreatedBy,
                n.CalculationDate, n.Principal, n.Interest, n.SentTo, n.Error, n.Invoices.Count)).ToList();
        }
    }
}

/// <summary>Zapisany dokument do wydruku: dane stron, pozycje (migawka), kwoty i treść.</summary>
public static class GetNotice
{
    public record Query(Guid Id) : IRequest<Response>;
    public record Party(string Name, string? Nip, string? Address, string? BankAccount, string? Email);
    public record Response(Guid Id, NoticeKind Kind, NoticeChannel Channel, NoticeStatus Status, DateTime CreatedAt, DateTime CalculationDate, DateTime PaymentDeadline,
        Party Issuer, Party Recipient, List<NoticeLine> Lines, decimal Principal, decimal Interest, decimal Total, string Subject, string Body);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var n = await db.CollectionNotices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono dokumentu");
            var contractor = await db.Contractors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == n.ContractorId, ct);
            var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == db.CurrentCompanyId, ct);
            var lines = JsonSerializer.Deserialize<List<NoticeLine>>(n.LinesJson) ?? [];
            var total = n.Kind == NoticeKind.InterestNote ? n.Interest : n.Principal + n.Interest;
            return new Response(n.Id, n.Kind, n.Channel, n.Status, n.CreatedAt, n.CalculationDate, n.PaymentDeadline,
                new Party(company?.Name ?? "", company?.Nip, company?.Address, company?.BankAccount, company?.Email),
                new Party(contractor?.Name ?? "", contractor?.Nip, contractor?.Address, null, contractor?.Email),
                lines, n.Principal, n.Interest, total, n.Subject, n.Body);
        }
    }
}
