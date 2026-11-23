using FluentValidation;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Application.Features.Accounts.Commands.Create;

public static partial class CreateAccount
{
    public record Command(string Name, string Code, AccountType Type) : IRequest<Result>;
    public record Result(Guid AccountId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa konta jest wymagana").MaximumLength(200);
            RuleFor(x => x.Code).NotEmpty().WithMessage("Numer konta jest wymagany").MaximumLength(30);
            RuleFor(x => x.Type).IsInEnum().WithMessage("Nieprawidłowy typ konta");
        }
    }
}
