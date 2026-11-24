using LedgerCore.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Search;

/// <summary>
/// Globalne wyszukiwanie w bieżącej firmie (filtr firmy działa automatycznie): konta, kontrahenci, faktury, księgowania.
/// Maksymalnie <see cref="Limit"/> wyników na kategorię; wzorzec LIKE jest parametryzowany, a znaki %, _ i \ z zapytania są traktowane dosłownie.
/// </summary>
public static class GlobalSearch
{
    public const int Limit = 5;
    public const int MinLength = 2;
    public const int MaxLength = 100;

    public record Query(string? Q) : IRequest<Response>;

    public record AccountHit(Guid Id, string Code, string Name);
    public record ContractorHit(Guid Id, string Name, string? Nip);
    public record InvoiceHit(Guid Id, string Number, string ContractorName, decimal Gross, bool IsCanceled);
    public record TransactionHit(Guid Id, string Number, string Description, DateTime Date);
    public record Response(List<AccountHit> Accounts, List<ContractorHit> Contractors, List<InvoiceHit> Invoices, List<TransactionHit> Transactions);

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var q = (r.Q ?? "").Trim();
            if (q.Length < MinLength) return new([], [], [], []);
            if (q.Length > MaxLength) q = q[..MaxLength];
            var p = $"%{Escape(q)}%";
            var nip = new string(q.Where(char.IsDigit).ToArray());
            var nipPattern = nip.Length >= MinLength ? $"%{nip}%" : null;

            var accounts = await db.Accounts.AsNoTracking()
                .Where(a => EF.Functions.Like(a.Code, p, "\\") || EF.Functions.Like(a.Name, p, "\\"))
                .OrderBy(a => a.Code).Take(Limit).Select(a => new AccountHit(a.Id, a.Code, a.Name)).ToListAsync(ct);

            var contractors = await db.Contractors.AsNoTracking()
                .Where(c => EF.Functions.Like(c.Name, p, "\\") || (nipPattern != null && c.Nip != null && EF.Functions.Like(c.Nip, nipPattern)))
                .OrderBy(c => c.Name).Take(Limit).Select(c => new ContractorHit(c.Id, c.Name, c.Nip)).ToListAsync(ct);

            var invoiceRows = await db.Invoices.AsNoTracking()
                .Where(i => EF.Functions.Like(i.Number, p, "\\"))
                .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.CreatedAt).Take(Limit)
                .Select(i => new { i.Id, i.Number, Name = i.Contractor!.Name, i.Gross, i.IsCanceled }).ToListAsync(ct);

            var transactions = await db.Transactions.AsNoTracking()
                .Where(t => EF.Functions.Like(t.Number, p, "\\") || EF.Functions.Like(t.Description, p, "\\"))
                .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Take(Limit)
                .Select(t => new TransactionHit(t.Id, t.Number, t.Description, t.Date)).ToListAsync(ct);

            return new Response(accounts, contractors,
                invoiceRows.Select(i => new InvoiceHit(i.Id, i.Number, i.Name, i.Gross, i.IsCanceled)).ToList(), transactions);
        }
    }
}
