using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Application.Features.Setup;

public static class SetupStatus
{
    public record Query : IRequest<Response>;
    public record Response(bool NeedsSetup);
    public class Handler(IIdentityService identity) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct) => new(!await identity.AnyUsers(ct));
    }
}

/// <summary>Pierwsze uruchomienie: tworzy konto administratora i domyślny plan kont. Działa tylko, gdy nie ma żadnych użytkowników.</summary>
public static class InitialSetup
{
    public record Command(string FirstName, string LastName, string Email, string Password, string? CompanyName = null, bool SeedChartOfAccounts = true)
        : IRequest<Result<Guid>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("Imię jest wymagane").MaximumLength(50);
            RuleFor(x => x.LastName).NotEmpty().WithMessage("Nazwisko jest wymagane").MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().WithMessage("E-mail jest wymagany").EmailAddress().WithMessage("Nieprawidłowy format e-mail");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Hasło jest wymagane").MinimumLength(8).WithMessage("Hasło musi mieć co najmniej 8 znaków");
        }
    }

    public class Handler(IIdentityService identity, IRegisterUserService register, IAppDbContext db, ICompanyContext ctx) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command r, CancellationToken ct)
        {
            if (await identity.AnyUsers(ct))
                return Result.Failure<Guid>("Konfiguracja początkowa została już wykonana");

            var user = new User(r.FirstName, r.LastName, r.Email, "");
            user.VerifyEmail();
            var result = await register.RegisterUser(user, r.Password, UserRole.Admin);
            if (result.IsFailure) return Result.Failure<Guid>(result.Error);

            // Pierwsza firma instalacji (kolejne dodaje administrator w menu „Firmy”)
            var company = new Company(string.IsNullOrWhiteSpace(r.CompanyName) ? "Moja firma" : r.CompanyName);
            db.Companies.Add(company);
            ctx.CompanyId = company.Id;
            if (r.SeedChartOfAccounts) DefaultChartOfAccounts.AddTo(db);
            await db.SaveChangesAsync(ct);
            return Result.Success(user.Id);
        }
    }
}

/// <summary>Uproszczony plan kont dla małego biura (można go dowolnie zmieniać w aplikacji).</summary>
public static class DefaultChartOfAccounts
{
    public static void AddTo(IAppDbContext db)
    {
        foreach (var (code, name, type) in Accounts) db.Accounts.Add(new Account(name, code, type));
    }

    public static readonly (string Code, string Name, AccountType Type)[] Accounts =
    [
        ("100", "Kasa", AccountType.Asset),
        ("130", "Rachunek bankowy", AccountType.Asset),
        ("201", "Rozrachunki z odbiorcami", AccountType.Asset),
        ("202", "Rozrachunki z dostawcami", AccountType.Liability),
        ("220", "Rozrachunki z urzędami (VAT, podatki, ZUS)", AccountType.Liability),
        ("234", "Rozrachunki z pracownikami", AccountType.Liability),
        ("800", "Kapitał własny", AccountType.Equity),
        ("401", "Zużycie materiałów i energii", AccountType.Expense),
        ("402", "Usługi obce", AccountType.Expense),
        ("404", "Wynagrodzenia", AccountType.Expense),
        ("409", "Pozostałe koszty", AccountType.Expense),
        ("700", "Przychody ze sprzedaży usług", AccountType.Income),
        ("760", "Pozostałe przychody", AccountType.Income),
    ];
}
