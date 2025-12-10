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
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Code).Length(3);
        }
    }
}