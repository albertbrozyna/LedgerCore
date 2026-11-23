using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Accounts.Commands.Create
{
    public static partial class CreateAccount
    {
        public class CreateAccountHandler(IAppDbContext context, LedgerCore.Application.Common.Ledger.AuditLog audit) : IRequestHandler<Command, Result>
        {
            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var code = request.Code.Trim();
                if (await context.Accounts.AnyAsync(a => a.Code == code, cancellationToken))
                    throw new DomainValidationException($"Konto o numerze {code} już istnieje");

                var account = new Account(request.Name, code, request.Type);
                context.Accounts.Add(account);
                await audit.Add("Konto", $"Dodano konto {account.Code} {account.Name}", cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                return new Result(account.Id);
            }
        }
    }
}
