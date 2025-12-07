using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using static CreateTransaction;


namespace LedgerCore.Application.Features.Transactions.Commands.Create;


public static partial class CreateTransaction
{
    public class Handler : IRequestHandler<Command, Result>
    {

        private readonly IAppDbContext _context;

        public Handler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var transaction = new Transaction(request.Description, request.Time);


            var accountIds = request.Entries.Select(e => e.AccountId).Distinct().ToList();

            var existingAccounts = await _context.Accounts.Where(a => accountIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync();

            if(accountIds.Count != existingAccounts.Count)
            {
                var missingId = accountIds.Except(existingAccounts).First();
                throw new AccountNotFoundException(missingId);
            }

            foreach (var entry in request.Entries)
            {
                transaction.AddEntry(entry.AccountId, entry.Amount, entry.Side);
            }

            transaction.Validate();


            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync(cancellationToken);

            return new Result(transaction.Id);
        }
    }
}