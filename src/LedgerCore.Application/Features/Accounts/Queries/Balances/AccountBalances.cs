using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Accounts.Queries.Balances;

/// <summary>
/// Salda wielu kont naraz (podgląd wpływu księgowania na salda). Saldo w znaku naturalnym konta (jak karta konta i plan kont).
/// Konta spoza bieżącej firmy są pomijane (filtr firmy) – w odpowiedzi ich nie ma.
/// </summary>
public static class AccountBalances
{
    public const int MaxAccounts = 50;

    public record Query(List<Guid> AccountIds) : IRequest<List<Item>>;
    public record Item(Guid AccountId, decimal Balance);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, List<Item>>
    {
        public async Task<List<Item>> Handle(Query r, CancellationToken ct)
        {
            var ids = (r.AccountIds ?? []).Where(i => i != Guid.Empty).Distinct().ToList();
            if (ids.Count > MaxAccounts) throw new DomainValidationException($"Można pobrać salda najwyżej {MaxAccounts} kont naraz");
            if (ids.Count == 0) return [];

            var accounts = await db.Accounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
            var sums = await LedgerQueries.SumsFor(db, null, null, false, ids, ct);
            return accounts.Select(a => new Item(a.Id, LedgerQueries.Natural(a, LedgerQueries.Of(sums, a.Id).Net))).ToList();
        }
    }
}
