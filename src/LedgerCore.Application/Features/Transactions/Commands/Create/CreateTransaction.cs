using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Transactions.Commands.Create;

public static class CreateTransaction
{
    public record NewEntryDto(Guid AccountId, decimal Amount, DebitCredit Side);
    public record Command(string Description, DateTime Date, List<NewEntryDto> Entries) : IRequest<Result>;
    public record Result(Guid TransactionId, string Number);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Description).NotEmpty().WithMessage("Opis jest wymagany").MaximumLength(500);
            RuleFor(x => x.Date).Must(d => d.Year is >= 2000 and <= 2100).WithMessage("Nieprawidłowa data księgowania");
            RuleFor(x => x.Entries).NotNull().Must(e => e != null && e.Count >= 2)
                .WithMessage("Księgowanie wymaga co najmniej dwóch zapisów");
            RuleForEach(x => x.Entries).ChildRules(e =>
            {
                e.RuleFor(x => x.AccountId).NotEmpty().WithMessage("Wybierz konto w każdym zapisie");
                e.RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Kwota musi być większa od zera");
                e.RuleFor(x => x.Side).IsInEnum();
            });
        }
    }

    public class Handler(IAppDbContext db, IUserDetails userDetails, LedgerCore.Application.Common.Ledger.AuditLog audit) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var accountIds = request.Entries.Select(e => e.AccountId).Distinct().ToList();
            var accounts = await db.Accounts.Where(a => accountIds.Contains(a.Id)).ToListAsync(ct);

            var missing = accountIds.Except(accounts.Select(a => a.Id)).FirstOrDefault();
            if (missing != Guid.Empty) throw new AccountNotFoundException(missing);

            var inactive = accounts.FirstOrDefault(a => !a.IsActive);
            if (inactive != null)
                throw new DomainValidationException($"Konto {inactive.Code} {inactive.Name} jest nieaktywne");

            await LedgerCore.Application.Common.Ledger.PeriodGuard.EnsureOpen(db, request.Date, ct);
            var number = await TransactionNumbers.Next(db, request.Date, ct);
            var userId = userDetails.User?.Id;
            var tx = new Transaction(request.Description, request.Date, number, userId);
            foreach (var e in request.Entries) tx.AddEntry(e.AccountId, e.Amount, e.Side);
            tx.Validate();

            db.Transactions.Add(tx);
            await audit.Add("Księgowanie", $"Dodano księgowanie {tx.Number}: {tx.Description} ({tx.TotalDebit:0.00})", ct);
            await db.SaveChangesAsync(ct);
            return new Result(tx.Id, tx.Number);
        }
    }
}

public static class TransactionNumbers
{
    /// <summary>Kolejny numer dokumentu w roku księgowania, np. DK/2026/0007.</summary>
    public static async Task<string> Next(IAppDbContext db, DateTime date, CancellationToken ct)
    {
        var prefix = $"DK/{date.Year}/";
        var existing = await db.Transactions.AsNoTracking()
            .Where(t => t.Number.StartsWith(prefix)).Select(t => t.Number).ToListAsync(ct);
        var max = existing
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:0000}";
    }
}
