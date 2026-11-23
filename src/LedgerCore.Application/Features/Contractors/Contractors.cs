using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Contractors;

public static class ContractorRules
{
    /// <summary>NIP jest opcjonalny (np. klient indywidualny), ale jeśli podany – musi mieć poprawną sumę kontrolną i być unikalny.</summary>
    public static async Task Check(IAppDbContext db, string? nip, Guid? selfId, CancellationToken ct)
    {
        var n = Contractor.NormalizeNip(nip);
        if (n == null) return;
        if (!Contractor.IsValidNip(n)) throw new DomainValidationException("Nieprawidłowy NIP (zła suma kontrolna)");
        if (await db.Contractors.AnyAsync(c => c.Nip == n && c.Id != selfId, ct))
            throw new DomainValidationException("Kontrahent z tym NIP już istnieje");
    }
}

public static class ListContractors
{
    public record Query(string? Search, bool IncludeInactive = false) : IRequest<List<Item>>;
    public record Item(Guid Id, string Name, string? Nip, string? Address, string? Email, bool IsActive,
        decimal Receivables, decimal Payables);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, List<Item>>
    {
        public async Task<List<Item>> Handle(Query r, CancellationToken ct)
        {
            var q = db.Contractors.AsNoTracking().Where(c => r.IncludeInactive || c.IsActive);
            if (!string.IsNullOrWhiteSpace(r.Search))
            {
                var s = r.Search.Trim();
                q = q.Where(c => EF.Functions.Like(c.Name, $"%{s}%") || (c.Nip != null && EF.Functions.Like(c.Nip, $"%{s}%")));
            }
            var contractors = await q.OrderBy(c => c.Name).ToListAsync(ct);
            var open = (await db.Invoices.AsNoTracking().Where(i => !i.IsCanceled).ToListAsync(ct)).Where(i => i.Remaining > 0).ToList();
            return contractors.Select(c => new Item(c.Id, c.Name, c.Nip, c.Address, c.Email, c.IsActive,
                open.Where(i => i.ContractorId == c.Id && i.Type == Domain.Enums.InvoiceType.Sales).Sum(i => i.Remaining),
                open.Where(i => i.ContractorId == c.Id && i.Type == Domain.Enums.InvoiceType.Purchase).Sum(i => i.Remaining))).ToList();
        }
    }
}

public static class SaveContractor
{
    /// <summary>Id == null → utworzenie, w przeciwnym razie edycja.</summary>
    public record Command(Guid? Id, string Name, string? Nip, string? Address, string? Email, bool IsActive = true) : IRequest<Guid>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa kontrahenta jest wymagana").MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(300);
            RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Nieprawidłowy e-mail").MaximumLength(100);
        }
    }

    public class Handler(IAppDbContext db, AuditLog audit) : IRequestHandler<Command, Guid>
    {
        public async Task<Guid> Handle(Command r, CancellationToken ct)
        {
            await ContractorRules.Check(db, r.Nip, r.Id, ct);
            Contractor c;
            if (r.Id.HasValue)
            {
                c = await db.Contractors.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono kontrahenta");
                c.Update(r.Name, r.Nip, r.Address, r.Email, r.IsActive);
                await audit.Add("Kontrahent", $"Zmieniono dane kontrahenta {c.Name}", ct);
            }
            else
            {
                c = new Contractor(r.Name, r.Nip, r.Address, r.Email);
                db.Contractors.Add(c);
                await audit.Add("Kontrahent", $"Dodano kontrahenta {c.Name}", ct);
            }
            await db.SaveChangesAsync(ct);
            return c.Id;
        }
    }
}
