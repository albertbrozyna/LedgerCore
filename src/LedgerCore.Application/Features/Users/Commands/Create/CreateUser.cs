using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Application.Features.Users.Commands.Create
{
    public static class CreateUser
    {
        public record Command(string FirstName, string LastName, string Email, string Password, UserRole Role, string? PhoneNumber) : IRequest<Result<Response>>;
        public record Response(Guid UserId);

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.FirstName).NotEmpty().WithMessage("Imię jest wymagane").MaximumLength(50);
                RuleFor(x => x.LastName).NotEmpty().WithMessage("Nazwisko jest wymagane").MaximumLength(50);
                RuleFor(x => x.Email).NotEmpty().WithMessage("E-mail jest wymagany").EmailAddress().WithMessage("Nieprawidłowy format e-mail").MaximumLength(100);
                RuleFor(x => x.Role).IsInEnum().WithMessage("Nieprawidłowa rola");
                RuleFor(x => x.Password).NotEmpty().WithMessage("Hasło jest wymagane").MinimumLength(8).WithMessage("Hasło musi mieć co najmniej 8 znaków");
            }
        }

        /// <summary>Konto tworzy administrator – jest od razu aktywne i zweryfikowane (aplikacja działa lokalnie, bez SMTP).</summary>
        public class Handler(IRegisterUserService registerUserService, LedgerCore.Application.Common.Ledger.AuditLog audit, LedgerCore.Application.Common.Interfaces.IAppDbContext db, LedgerCore.Application.Common.Interfaces.ICompanyContext company) : IRequestHandler<Command, Result<Response>>
        {
            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var user = new User(request.FirstName, request.LastName, request.Email, request.PhoneNumber ?? "");
                user.VerifyEmail();
                var result = await registerUserService.RegisterUser(user, request.Password, request.Role).Map(u => new Response(u.Id));
                if (result.IsSuccess)
                {
                    // nowy użytkownik od razu ma dostęp do firmy, w której pracuje administrator (kolejne firmy przypisuje się w menu Użytkownicy)
                    if (company.CompanyId != Guid.Empty) db.UserCompanies.Add(new LedgerCore.Domain.Entities.UserCompany(result.Value.UserId, company.CompanyId));
                    await audit.Add("Użytkownik", $"Dodano użytkownika {request.Email} (rola {request.Role})", cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }
                return result;
            }
        }
    }
}
