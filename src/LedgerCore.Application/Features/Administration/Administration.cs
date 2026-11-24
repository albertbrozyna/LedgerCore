using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Administration;

public static class GetPeriodLock
{
    public record Query : IRequest<Response>;
    public record Response(DateTime? LockedThrough);
    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct) => new(await PeriodGuard.LockedThrough(db, ct));
    }
}

/// <summary>Zamknięcie okresu: po ustawieniu daty nie można księgować (ani stornować) dokumentów z datą ≤ tej daty.</summary>
public static class SetPeriodLock
{
    public record Command(DateTime? LockedThrough) : IRequest<Unit>;
    public class Handler(IAppDbContext db, AuditLog audit) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command r, CancellationToken ct)
        {
            var row = await db.PeriodLocks.FirstOrDefaultAsync(ct);
            if (row == null) { row = new PeriodLock(); db.PeriodLocks.Add(row); }
            row.Set(r.LockedThrough);
            await audit.Add("Zamknięcie okresu", r.LockedThrough.HasValue
                ? $"Zamknięto okres do {r.LockedThrough:yyyy-MM-dd}" : "Odblokowano wszystkie okresy", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

public static class ListAudit
{
    public record Query(string? Search, int Page = 1, int PageSize = 50, string? Sort = null, string? Dir = null) : IRequest<Response>;
    public static readonly string[] SortKeys = ["at", "user", "action"];
    public record Item(Guid Id, DateTime At, string UserName, string Action, string Details);
    public record Response(List<Item> Items, int TotalCount, int Page, int PageSize);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var q = db.AuditEntries.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(r.Search))
            {
                var s = r.Search.Trim();
                q = q.Where(a => EF.Functions.Like(a.Details, $"%{s}%") || EF.Functions.Like(a.Action, $"%{s}%") || EF.Functions.Like(a.UserName, $"%{s}%"));
            }
            var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 200);
            var total = await q.CountAsync(ct);
            if (r.Sort is not null && !SortKeys.Contains(r.Sort.ToLowerInvariant()))
                throw new DomainValidationException($"Nieprawidłowe sortowanie: {r.Sort}");
            var desc = !string.Equals(r.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            var ordered = (r.Sort?.ToLowerInvariant()) switch
            {
                "user" => desc ? q.OrderByDescending(a => a.UserName) : q.OrderBy(a => a.UserName),
                "action" => desc ? q.OrderByDescending(a => a.Action) : q.OrderBy(a => a.Action),
                _ => desc ? q.OrderByDescending(a => a.At) : q.OrderBy(a => a.At),
            };
            var rows = await ordered.ThenByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
            return new Response(rows.Select(a => new Item(a.Id, a.At, a.UserName, a.Action, a.Details)).ToList(), total, page, size);
        }
    }
}
