using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Accounts.Queries.List;

public static class ListAccounts
{
    public record Query(bool IncludeInactive = false) : IRequest<List<AccountDto>>;

    /// <summary>Balance jest w znaku naturalnym konta (aktywa/koszty: Winien-Ma, reszta: Ma-Winien).</summary>
    public record AccountDto(Guid Id, string Code, string Name, AccountType Type, bool IsActive,
        decimal DebitTotal, decimal CreditTotal, decimal Balance);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, List<AccountDto>>
    {
        public async Task<List<AccountDto>> Handle(Query request, CancellationToken ct)
        {
            var accounts = await db.Accounts.AsNoTracking()
                .Where(a => request.IncludeInactive || a.IsActive).ToListAsync(ct);
            var sums = await LedgerQueries.Sums(db, null, null, false, null, ct);

            return accounts.OrderBy(a => a.Code, StringComparer.Ordinal).Select(a =>
            {
                var s = LedgerQueries.Of(sums, a.Id);
                return new AccountDto(a.Id, a.Code, a.Name, a.Type, a.IsActive, s.Debit, s.Credit, LedgerQueries.Natural(a, s.Net));
            }).ToList();
        }
    }
}
