using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using MediatR;
using static LedgerCore.Application.Features.Accounts.Commands.Create.CreateAccount;

namespace LedgerCore.Application.Features.Accounts.Commands.Create
{

    public static partial class CreateAccount
    {


        public class CreateAccountHandler : IRequestHandler<Command, Result>
        {
            private readonly IAppDbContext _context;

            public CreateAccountHandler(IAppDbContext context)
            {
                _context = context;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var account = new Account(request.Name, request.Code, request.Type);

                _context.Accounts.Add(account);

                await _context.SaveChangesAsync(cancellationToken);

                return new Result(account.Id);
            }
        }

    }
}

