using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Transactions.Queries;

public static class ListTransactions
{
    /// <summary>Sort: date (domyślnie), number, description, amount; Dir: asc/desc. Nazwy kolumn z żądania nigdy nie trafiają do SQL – tylko wybór z listy.</summary>
    public record Query(DateTime? From, DateTime? To, Guid? AccountId, string? Search, int Page = 1, int PageSize = 25,
        string? Sort = null, string? Dir = null) : IRequest<Response>;

    public static readonly string[] SortKeys = ["date", "number", "description", "amount"];

    public record Item(Guid Id, string Number, DateTime Date, string Description, decimal Amount, int EntryCount,
        bool IsReversal, bool IsReversed);

    public record Response(List<Item> Items, int TotalCount, int Page, int PageSize);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var page = Math.Max(1, r.Page);
            var size = Math.Clamp(r.PageSize, 1, 200);

            var q = db.Transactions.AsNoTracking().AsQueryable();
            if (r.From.HasValue) q = q.Where(t => t.Date >= r.From.Value.Date);
            if (r.To.HasValue) q = q.Where(t => t.Date <= r.To.Value.Date);
            if (r.AccountId.HasValue) q = q.Where(t => t.Entries.Any(e => e.AccountId == r.AccountId.Value));
            if (!string.IsNullOrWhiteSpace(r.Search))
            {
                var s = r.Search.Trim();
                q = q.Where(t => EF.Functions.Like(t.Description, $"%{s}%") || EF.Functions.Like(t.Number, $"%{s}%"));
            }

            var total = await q.CountAsync(ct);
            var desc = !string.Equals(r.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            if (r.Sort is not null && !SortKeys.Contains(r.Sort.ToLowerInvariant()))
                throw new DomainValidationException($"Nieprawidłowe sortowanie: {r.Sort}");
            IOrderedQueryable<Transaction> ordered = (r.Sort?.ToLowerInvariant()) switch
            {
                "number" => desc ? q.OrderByDescending(t => t.Number) : q.OrderBy(t => t.Number),
                "description" => desc ? q.OrderByDescending(t => t.Description) : q.OrderBy(t => t.Description),
                "amount" => desc
                    ? q.OrderByDescending(t => t.Entries.Where(e => e.Side == DebitCredit.Debit).Sum(e => e.AmountCents))
                    : q.OrderBy(t => t.Entries.Where(e => e.Side == DebitCredit.Debit).Sum(e => e.AmountCents)),
                _ => desc ? q.OrderByDescending(t => t.Date) : q.OrderBy(t => t.Date),
            };
            var pageRows = await ordered.ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
                .Skip((page - 1) * size).Take(size)
                .Select(t => new
                {
                    t.Id, t.Number, t.Date, t.Description, t.ReversesTransactionId,
                    Debits = t.Entries.Where(e => e.Side == DebitCredit.Debit).Select(e => e.Amount).ToList(),
                    Count = t.Entries.Count
                }).ToListAsync(ct);

            var ids = pageRows.Select(p => p.Id).ToList();
            var reversed = (await db.Transactions.AsNoTracking()
                .Where(t => t.ReversesTransactionId != null && ids.Contains(t.ReversesTransactionId.Value))
                .Select(t => t.ReversesTransactionId!.Value).ToListAsync(ct)).ToHashSet();

            var items = pageRows.Select(p => new Item(p.Id, p.Number, p.Date, p.Description, p.Debits.Sum(),
                p.Count, p.ReversesTransactionId != null, reversed.Contains(p.Id))).ToList();
            return new Response(items, total, page, size);
        }
    }
}

public static class GetTransaction
{
    public record Query(Guid Id) : IRequest<Response>;

    public record EntryItem(Guid AccountId, string AccountCode, string AccountName, decimal Amount, DebitCredit Side);

    public record Response(Guid Id, string Number, DateTime Date, DateTime CreatedAt, string Description,
        Guid? ReversesTransactionId, string? ReversesNumber, Guid? ReversedByTransactionId, string? ReversedByNumber,
        List<EntryItem> Entries);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var t = await db.Transactions.AsNoTracking().Include(x => x.Entries).ThenInclude(e => e.Account)
                        .FirstOrDefaultAsync(x => x.Id == r.Id, ct)
                    ?? throw new DomainValidationException("Nie znaleziono księgowania");

            string? revNumber = null;
            if (t.ReversesTransactionId != null)
                revNumber = await db.Transactions.Where(x => x.Id == t.ReversesTransactionId).Select(x => x.Number).FirstOrDefaultAsync(ct);
            var reversedBy = await db.Transactions.AsNoTracking().Where(x => x.ReversesTransactionId == t.Id)
                .Select(x => new { x.Id, x.Number }).FirstOrDefaultAsync(ct);

            return new Response(t.Id, t.Number, t.Date, t.CreatedAt, t.Description, t.ReversesTransactionId, revNumber,
                reversedBy?.Id, reversedBy?.Number,
                t.Entries.OrderBy(e => e.Side).ThenBy(e => e.Account!.Code)
                    .Select(e => new EntryItem(e.AccountId, e.Account!.Code, e.Account.Name, e.Amount, e.Side)).ToList());
        }
    }
}
