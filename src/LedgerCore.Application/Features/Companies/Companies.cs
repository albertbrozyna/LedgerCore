using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Setup;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Companies;

public static class CompanyAccess
{
    public const string AdminRole = "Admin";

    /// <summary>Firmy dostępne dla użytkownika: Administrator widzi wszystkie aktywne, pozostali tylko przypisane.</summary>
    public static async Task<List<Company>> Accessible(IAppDbContext db, IIdentityService identity, Guid userId, CancellationToken ct)
    {
        var roles = await identity.GetUserRoles(userId);
        var q = db.Companies.AsNoTracking().Where(c => c.IsActive);
        if (!roles.Contains(AdminRole))
        {
            var ids = await db.UserCompanies.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.CompanyId).ToListAsync(ct);
            q = q.Where(c => ids.Contains(c.Id));
        }
        return await q.OrderBy(c => c.Name).ToListAsync(ct);
    }
}

public static class ListMyCompanies
{
    public record Query : IRequest<List<Item>>;
    public record Item(Guid Id, string Name, string? Nip, AccountingMode AccountingMode = AccountingMode.FullBooks);

    public class Handler(IAppDbContext db, IIdentityService identity, IUserDetails me) : IRequestHandler<Query, List<Item>>
    {
        public async Task<List<Item>> Handle(Query r, CancellationToken ct) =>
            me.User is null ? [] : (await CompanyAccess.Accessible(db, identity, me.User.Id, ct)).Select(c => new Item(c.Id, c.Name, c.Nip, c.AccountingMode)).ToList();
    }
}

public static class ListAllCompanies
{
    public record Query : IRequest<List<Item>>;
    public record Item(Guid Id, string Name, string? Nip, string? Address, bool IsActive, DateTime CreatedAt, AccountingMode AccountingMode = AccountingMode.FullBooks);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, List<Item>>
    {
        public async Task<List<Item>> Handle(Query r, CancellationToken ct) =>
            (await db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct))
            .Select(c => new Item(c.Id, c.Name, c.Nip, c.Address, c.IsActive, c.CreatedAt, c.AccountingMode)).ToList();
    }
}

public static class CreateCompany
{
    public record Command(string Name, string? Nip, string? Address, bool SeedChartOfAccounts = true, AccountingMode Mode = AccountingMode.FullBooks) : IRequest<Guid>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa firmy jest wymagana").MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(300);
        }
    }

    public class Handler(IAppDbContext db, AuditLog audit, ICompanyContext ctx) : IRequestHandler<Command, Guid>
    {
        public async Task<Guid> Handle(Command r, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(r.Nip) && !Contractor.IsValidNip(r.Nip))
                throw new DomainValidationException("Nieprawidłowy NIP firmy (zła suma kontrolna)");
            var nip = Contractor.NormalizeNip(r.Nip);
            if (nip != null && await db.Companies.AnyAsync(c => c.Nip == nip, ct))
                throw new DomainValidationException("Firma z tym NIP już istnieje");

            var c = new Company(r.Name);
            c.Update(r.Name, r.Nip, r.Address, null, null, null, null);
            if (!Enum.IsDefined(r.Mode)) throw new DomainValidationException("Nieprawidłowy tryb ewidencji");
            c.SetAccountingMode(r.Mode);
            db.Companies.Add(c);

            // Nowe rekordy trafią do nowej firmy; wpis audytu zostaje w firmie, z której administrator ją utworzył.
            await audit.Add("Firma", $"Dodano firmę {c.Name} (tryb: {r.Mode})", ct);
            if (r.SeedChartOfAccounts && r.Mode == AccountingMode.FullBooks) // KPiR i ryczałt nie używają planu kont
            {
                var previous = ctx.CompanyId;
                await db.SaveChangesAsync(ct); // zapisuje audyt we właściwej firmie
                ctx.CompanyId = c.Id;
                try { DefaultChartOfAccounts.AddTo(db); await db.SaveChangesAsync(ct); }
                finally { ctx.CompanyId = previous; }
            }
            else await db.SaveChangesAsync(ct);
            return c.Id;
        }
    }
}

public static class UpdateCompanyStatus
{
    public record Command(Guid Id, string Name, bool IsActive) : IRequest<Unit>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa firmy jest wymagana").MaximumLength(200);
    }

    public class Handler(IAppDbContext db, AuditLog audit, ICompanyContext ctx) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command r, CancellationToken ct)
        {
            var c = await db.Companies.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono firmy");
            if (!r.IsActive && c.Id == ctx.CompanyId) throw new DomainValidationException("Nie można dezaktywować firmy, w której aktualnie pracujesz");
            if (!r.IsActive && await db.Companies.CountAsync(x => x.IsActive && x.Id != c.Id, ct) == 0)
                throw new DomainValidationException("Musi pozostać co najmniej jedna aktywna firma");
            c.Update(r.Name, c.Nip, c.Address, c.Email, c.Phone, c.BankAccount, c.InvoiceFooter);
            c.SetActive(r.IsActive);
            await audit.Add("Firma", $"Zmieniono firmę {c.Name} ({(c.IsActive ? "aktywna" : "nieaktywna")})", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

public static class UserCompanies
{
    public record GetQuery(Guid UserId) : IRequest<List<Guid>>;
    public record SetCommand(Guid UserId, List<Guid> CompanyIds) : IRequest<Unit>;

    public class GetHandler(IAppDbContext db) : IRequestHandler<GetQuery, List<Guid>>
    {
        public async Task<List<Guid>> Handle(GetQuery r, CancellationToken ct) =>
            await db.UserCompanies.AsNoTracking().Where(u => u.UserId == r.UserId).Select(u => u.CompanyId).ToListAsync(ct);
    }

    public class SetHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<SetCommand, Unit>
    {
        public async Task<Unit> Handle(SetCommand r, CancellationToken ct)
        {
            var wanted = r.CompanyIds.Distinct().ToList();
            if (await db.Companies.CountAsync(c => wanted.Contains(c.Id), ct) != wanted.Count)
                throw new DomainValidationException("Wskazano nieistniejącą firmę");
            var existing = await db.UserCompanies.Where(u => u.UserId == r.UserId).ToListAsync(ct);
            db.UserCompanies.RemoveRange(existing.Where(e => !wanted.Contains(e.CompanyId)));
            foreach (var id in wanted.Where(id => existing.All(e => e.CompanyId != id)))
                db.UserCompanies.Add(new UserCompany(r.UserId, id));
            var email = await db.Users.AsNoTracking().Where(u => u.Id == r.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct) ?? r.UserId.ToString();
            await audit.Add("Użytkownik", $"Zmieniono dostęp użytkownika {email} do firm (liczba firm: {wanted.Count})", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
