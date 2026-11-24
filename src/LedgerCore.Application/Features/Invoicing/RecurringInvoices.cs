using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Invoicing;

public static class RecurringInvoices
{
    public record LineDto(string Description, decimal Net, VatRate VatRate);

    public record Item(Guid Id, string Name, Guid ContractorId, string ContractorName, RecurrenceFrequency Frequency, int DayOfMonth, int DueDays,
        DateTime NextDate, DateTime? EndDate, bool IsActive, Guid? ResultAccountId, KpirKind? Kind, decimal? RyczaltRate, string? Notes,
        decimal Net, decimal Gross, int GeneratedCount, List<LineDto> Lines);

    public record ListQuery : IRequest<List<Item>>;

    public class ListHandler(IAppDbContext db) : IRequestHandler<ListQuery, List<Item>>
    {
        public async Task<List<Item>> Handle(ListQuery r, CancellationToken ct)
        {
            var list = await db.RecurringInvoices.AsNoTracking().Include(x => x.Lines).Include(x => x.Contractor)
                .OrderBy(x => x.Name).ToListAsync(ct);
            var counts = await db.Invoices.AsNoTracking().Where(i => i.RecurringInvoiceId != null)
                .GroupBy(i => i.RecurringInvoiceId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct);
            return list.Select(x =>
            {
                var net = x.Lines.Sum(l => l.Net);
                var vat = x.Lines.Sum(l => decimal.Round(l.Net * l.VatRate.Percent() / 100m, 2, MidpointRounding.AwayFromZero));
                return new Item(x.Id, x.Name, x.ContractorId, x.Contractor?.Name ?? "", x.Frequency, x.DayOfMonth, x.DueDays, x.NextDate, x.EndDate,
                    x.IsActive, x.ResultAccountId, x.KpirKind, x.RyczaltRate, x.Notes, net, net + vat,
                    counts.FirstOrDefault(c => c.Id == x.Id)?.N ?? 0, x.Lines.Select(l => new LineDto(l.Description, l.Net, l.VatRate)).ToList());
            }).ToList();
        }
    }

    public record SaveCommand(Guid? Id, string Name, Guid ContractorId, RecurrenceFrequency Frequency, int DayOfMonth, int DueDays, DateTime NextDate,
        DateTime? EndDate, Guid? ResultAccountId, KpirKind? Kind, decimal? RyczaltRate, string? Notes, List<LineDto> Lines) : IRequest<Guid>;

    public class SaveValidator : AbstractValidator<SaveCommand>
    {
        public SaveValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa szablonu jest wymagana").MaximumLength(200);
            RuleFor(x => x.ContractorId).NotEmpty().WithMessage("Wybierz kontrahenta");
            RuleFor(x => x.Notes).MaximumLength(500);
            RuleFor(x => x.Lines).NotNull().Must(l => l != null && l.Count > 0).WithMessage("Szablon wymaga co najmniej jednej pozycji");
            RuleForEach(x => x.Lines).ChildRules(l =>
            {
                l.RuleFor(x => x.Description).NotEmpty().WithMessage("Opis pozycji jest wymagany").MaximumLength(300);
                l.RuleFor(x => x.Net).GreaterThan(0).WithMessage("Kwota netto musi być większa od zera");
            });
        }
    }

    public class SaveHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<SaveCommand, Guid>
    {
        public async Task<Guid> Handle(SaveCommand r, CancellationToken ct)
        {
            var contractor = await db.Contractors.FirstOrDefaultAsync(c => c.Id == r.ContractorId, ct) ?? throw new DomainValidationException("Nie znaleziono kontrahenta");
            if (!contractor.IsActive) throw new DomainValidationException("Kontrahent jest nieaktywny");
            if (r.ResultAccountId is { } acc)
            {
                var a = await db.Accounts.FirstOrDefaultAsync(x => x.Id == acc, ct) ?? throw new DomainValidationException("Nie znaleziono konta przychodu");
                if (a.Type != AccountType.Income) throw new DomainValidationException("Faktura sprzedaży musi być księgowana na konto przychodów");
            }
            // klasyfikacja KPiR/ryczałt sprawdzana tak jak przy zwykłej fakturze – błąd wychodzi przy zapisie szablonu, nie dopiero przy generowaniu
            var mode = await CompanyMode.Get(db, ct);
            var (kind, rate) = Features.Registers.RegisterRules.Classify(mode, InvoiceType.Sales, r.Kind, r.RyczaltRate);
            var lines = r.Lines.Select(l => (l.Description, l.Net, l.VatRate)).ToList();

            if (r.Id is { } id)
            {
                var t = await db.RecurringInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainValidationException("Nie znaleziono szablonu");
                db.RecurringInvoiceLines.RemoveRange(t.Lines);
                t.Update(r.Name, r.ContractorId, r.Frequency, r.DayOfMonth, r.DueDays, r.NextDate, r.EndDate, r.ResultAccountId, kind, rate, r.Notes, lines);
                foreach (var l in t.Lines) db.RecurringInvoiceLines.Add(l);
                await audit.Add("Faktura cykliczna", $"Zmieniono szablon faktury cyklicznej „{t.Name}”", ct);
                await db.SaveChangesAsync(ct);
                return t.Id;
            }
            var n = new RecurringInvoice(r.Name, r.ContractorId, r.Frequency, r.DayOfMonth, r.DueDays, r.NextDate, r.EndDate, r.ResultAccountId, kind, rate, r.Notes, lines);
            db.RecurringInvoices.Add(n);
            await audit.Add("Faktura cykliczna", $"Dodano szablon faktury cyklicznej „{n.Name}” ({contractor.Name})", ct);
            await db.SaveChangesAsync(ct);
            return n.Id;
        }
    }

    public record SetActiveCommand(Guid Id, bool Active) : IRequest<Unit>;
    public class SetActiveHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<SetActiveCommand, Unit>
    {
        public async Task<Unit> Handle(SetActiveCommand r, CancellationToken ct)
        {
            var t = await db.RecurringInvoices.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono szablonu");
            if (r.Active && t.EndDate != null && t.NextDate > t.EndDate)
                throw new DomainValidationException("Szablon zakończył się – przesuń datę zakończenia, aby go wznowić");
            t.SetActive(r.Active);
            await audit.Add("Faktura cykliczna", $"{(r.Active ? "Włączono" : "Wstrzymano")} szablon faktury cyklicznej „{t.Name}”", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public record DeleteCommand(Guid Id) : IRequest<Unit>;
    public class DeleteHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<DeleteCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteCommand r, CancellationToken ct)
        {
            var t = await db.RecurringInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono szablonu");
            db.RecurringInvoices.Remove(t); // wystawione faktury zostają (tylko tracą powiązanie z szablonem w historii)
            await audit.Add("Faktura cykliczna", $"Usunięto szablon faktury cyklicznej „{t.Name}”", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public record Generated(string Template, string Number, DateTime IssueDate, decimal Gross);
    public record Skipped(string Template, DateTime Date, string Reason);
    public record GenerateResult(List<Generated> Generated, List<Skipped> Skipped);

    /// <summary>Wystawia faktury za wszystkie zaległe okresy (do dnia <c>AsOf</c>). Okres zamknięty lub błąd danych wstrzymuje tylko dany szablon.</summary>
    public record GenerateCommand(DateTime? AsOf, Guid? TemplateId = null) : IRequest<GenerateResult>;

    public class GenerateHandler(IAppDbContext db, ISender sender, AuditLog audit) : IRequestHandler<GenerateCommand, GenerateResult>
    {
        const int MaxPerTemplate = 60; // zabezpieczenie przed pętlą (np. szablon sprzed 5 lat)

        public async Task<GenerateResult> Handle(GenerateCommand r, CancellationToken ct)
        {
            var asOf = (r.AsOf ?? DateTime.Today).Date;
            var generated = new List<Generated>();
            var skipped = new List<Skipped>();
            var query = db.RecurringInvoices.Include(x => x.Lines).Where(x => x.IsActive);
            if (r.TemplateId is { } only) query = query.Where(x => x.Id == only);
            var templates = await query.OrderBy(x => x.Name).ToListAsync(ct);
            if (r.TemplateId != null && templates.Count == 0) throw new DomainValidationException("Nie znaleziono aktywnego szablonu");

            foreach (var t in templates)
            {
                for (var guard = 0; t.IsActive && t.IsDue(asOf) && guard < MaxPerTemplate; guard++)
                {
                    var period = t.NextDate;
                    var existing = await db.Invoices.AsNoTracking().Where(i => i.RecurringInvoiceId == t.Id && i.RecurringPeriod == period)
                        .Select(i => i.Number).FirstOrDefaultAsync(ct);
                    if (existing == null)
                    {
                        CreateInvoice.Result res;
                        try
                        {
                            res = await sender.Send(new CreateInvoice.Command(InvoiceType.Sales, null, t.ContractorId, period, period, period.AddDays(t.DueDays),
                                t.ResultAccountId, t.Notes, t.Lines.Select(l => new CreateInvoice.LineDto(l.Description, l.Net, l.VatRate)).ToList(),
                                t.KpirKind, t.RyczaltRate, t.Id, period), ct);
                        }
                        catch (Exception e) when (e is DomainException or ValidationException)
                        {
                            skipped.Add(new Skipped(t.Name, period, e is ValidationException ve ? string.Join("; ", ve.Errors.Select(x => x.ErrorMessage)) : e.Message));
                            break; // kolejne okresy tego szablonu czekają na usunięcie przyczyny (np. odblokowanie okresu)
                        }
                        var inv = await db.Invoices.AsNoTracking().Where(i => i.Id == res.InvoiceId).Select(i => new { i.Number, i.Gross }).FirstAsync(ct);
                        generated.Add(new Generated(t.Name, inv.Number, period, inv.Gross));
                        await audit.Add("Faktura cykliczna", $"Wygenerowano fakturę {inv.Number} z szablonu „{t.Name}” za okres {period:yyyy-MM-dd}", ct);
                    }
                    t.Advance();
                    await db.SaveChangesAsync(ct);
                }
            }
            return new GenerateResult(generated, skipped);
        }
    }
}
