using System.Text.Json;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Templates;

public record TemplateLine(Guid AccountId, DebitCredit Side, decimal? Amount);

public static class ListTemplates
{
    public record Query : IRequest<List<Item>>;
    public record Item(Guid Id, string Name, string Description, List<TemplateLine> Lines);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, List<Item>>
    {
        static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
        public async Task<List<Item>> Handle(Query r, CancellationToken ct) =>
            (await db.TransactionTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct))
            .Select(t => new Item(t.Id, t.Name, t.Description,
                JsonSerializer.Deserialize<List<TemplateLine>>(t.LinesJson, Opts) ?? new List<TemplateLine>())).ToList();
    }
}

public static class SaveTemplate
{
    public record Command(string Name, string Description, List<TemplateLine> Lines) : IRequest<Guid>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa szablonu jest wymagana").MaximumLength(100);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.Lines).Must(l => l != null && l.Count >= 2).WithMessage("Szablon wymaga co najmniej dwóch pozycji");
            RuleForEach(x => x.Lines).ChildRules(l =>
            {
                l.RuleFor(x => x.AccountId).NotEmpty().WithMessage("Wybierz konto w każdej pozycji szablonu");
                l.RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue).WithMessage("Kwota musi być większa od zera");
            });
        }
    }

    public class Handler(IAppDbContext db, AuditLog audit) : IRequestHandler<Command, Guid>
    {
        public async Task<Guid> Handle(Command r, CancellationToken ct)
        {
            var name = r.Name.Trim();
            if (await db.TransactionTemplates.AnyAsync(t => t.Name == name, ct))
                throw new DomainValidationException($"Szablon „{name}” już istnieje");
            var ids = r.Lines.Select(l => l.AccountId).Distinct().ToList();
            if (await db.Accounts.CountAsync(a => ids.Contains(a.Id), ct) != ids.Count)
                throw new DomainValidationException("Szablon wskazuje nieistniejące konto");
            var t = new TransactionTemplate(name, r.Description ?? "", JsonSerializer.Serialize(r.Lines));
            db.TransactionTemplates.Add(t);
            await audit.Add("Szablon", $"Dodano szablon księgowania „{name}”", ct);
            await db.SaveChangesAsync(ct);
            return t.Id;
        }
    }
}

public static class DeleteTemplate
{
    public record Command(Guid Id) : IRequest<Unit>;
    public class Handler(IAppDbContext db, AuditLog audit) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command r, CancellationToken ct)
        {
            var t = await db.TransactionTemplates.FirstOrDefaultAsync(x => x.Id == r.Id, ct)
                    ?? throw new DomainValidationException("Nie znaleziono szablonu");
            db.TransactionTemplates.Remove(t);
            await audit.Add("Szablon", $"Usunięto szablon księgowania „{t.Name}”", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
