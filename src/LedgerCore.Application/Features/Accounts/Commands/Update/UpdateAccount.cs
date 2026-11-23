using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Accounts.Commands.Update;

public static class UpdateAccount
{
    public record Command(Guid Id, string Name, bool IsActive) : IRequest<Unit>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa konta jest wymagana").MaximumLength(200);
    }

    public class Handler(IAppDbContext db, LedgerCore.Application.Common.Ledger.AuditLog audit) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command request, CancellationToken ct)
        {
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
                          ?? throw new AccountNotFoundException(request.Id);
            account.Rename(request.Name);
            account.SetActive(request.IsActive);
            await audit.Add("Konto", $"Zmieniono konto {account.Code}: nazwa \"{account.Name}\", {(account.IsActive ? "aktywne" : "nieaktywne")}", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
