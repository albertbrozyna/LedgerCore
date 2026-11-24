using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Transactions.Commands.Reverse;

/// <summary>Storno – zamiast kasować księgowanie (co w księgowości jest niedozwolone) tworzymy zapis odwrotny.</summary>
public static class ReverseTransaction
{
    public record Command(Guid TransactionId, DateTime? Date) : IRequest<CreateTransaction.Result>;

    public class Handler(IAppDbContext db, IUserDetails userDetails, LedgerCore.Application.Common.Ledger.AuditLog audit) : IRequestHandler<Command, CreateTransaction.Result>
    {
        public async Task<CreateTransaction.Result> Handle(Command request, CancellationToken ct)
        {
            await LedgerCore.Application.Common.Ledger.CompanyMode.RequireFullBooks(db, ct);
            var original = await db.Transactions.Include(t => t.Entries)
                               .FirstOrDefaultAsync(t => t.Id == request.TransactionId, ct)
                           ?? throw new DomainValidationException("Nie znaleziono księgowania");

            if (original.ReversesTransactionId != null)
                throw new DomainValidationException("Nie można stornować storna");
            if (await db.Transactions.AnyAsync(t => t.ReversesTransactionId == original.Id, ct))
                throw new DomainValidationException("To księgowanie zostało już stornowane");

            if (await db.Invoices.AnyAsync(i => i.TransactionId == original.Id, ct) || await db.Payments.AnyAsync(p => p.TransactionId == original.Id, ct))
                throw new DomainValidationException("To księgowanie wynika z faktury lub płatności – anuluj fakturę zamiast stornować księgowanie");

            var date = (request.Date ?? DateTime.Today).Date;
            await LedgerCore.Application.Common.Ledger.PeriodGuard.EnsureOpen(db, date, ct);
            var number = await TransactionNumbers.Next(db, date, ct);
            var reversal = original.CreateReversal(number, date, userDetails.User?.Id);

            db.Transactions.Add(reversal);
            await audit.Add("Storno", $"Storno księgowania {original.Number} -> {reversal.Number}", ct);
            await db.SaveChangesAsync(ct);
            return new CreateTransaction.Result(reversal.Id, reversal.Number);
        }
    }
}
