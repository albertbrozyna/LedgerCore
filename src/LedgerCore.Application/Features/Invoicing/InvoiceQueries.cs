using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Invoicing;

public static class InvoiceStatus
{
    public const string Unpaid = "unpaid";       // nieopłacone (w terminie lub po)
    public const string Overdue = "overdue";     // po terminie
    public const string Paid = "paid";
    public const string Canceled = "canceled";
    public const string Correction = "correction"; // faktura korygująca (nie podlega płatności)

    public static string Of(Invoice i, DateTime today) =>
        i.IsCanceled ? Canceled : i.IsCorrection ? Correction : i.Remaining == 0 ? Paid : i.DueDate < today ? Overdue : Unpaid;
}

public static class ListInvoices
{
    public record Query(InvoiceType? Type, Guid? ContractorId, DateTime? From, DateTime? To, string? Status, string? Search,
        int Page = 1, int PageSize = 25) : IRequest<Response>;

    public record Item(Guid Id, InvoiceType Type, string Number, Guid ContractorId, string ContractorName, DateTime IssueDate,
        DateTime DueDate, decimal Net, decimal Vat, decimal Gross, decimal PaidAmount, decimal Remaining, string Status,
        bool IsCorrection, Guid? CorrectedInvoiceId, string? CorrectedNumber);

    public record Response(List<Item> Items, int TotalCount, int Page, int PageSize, decimal TotalGross, decimal TotalRemaining);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var today = DateTime.Today;
            var q = db.Invoices.AsNoTracking().Include(i => i.Contractor).AsQueryable();
            if (r.Type.HasValue) q = q.Where(i => i.Type == r.Type);
            if (r.ContractorId.HasValue) q = q.Where(i => i.ContractorId == r.ContractorId);
            if (r.From.HasValue) q = q.Where(i => i.IssueDate >= r.From.Value.Date);
            if (r.To.HasValue) q = q.Where(i => i.IssueDate <= r.To.Value.Date);
            if (!string.IsNullOrWhiteSpace(r.Search))
            {
                var s = r.Search.Trim();
                q = q.Where(i => EF.Functions.Like(i.Number, $"%{s}%") || EF.Functions.Like(i.Contractor!.Name, $"%{s}%"));
            }

            // Status liczony w pamięci (zależy od dzisiejszej daty i sum decimal, których SQLite nie porównuje po stronie bazy)
            var all = (await q.ToListAsync(ct)).Where(i => r.Status switch
            {
                InvoiceStatus.Unpaid => !i.IsCanceled && i.Remaining > 0,   // do zapłaty (także po terminie)
                InvoiceStatus.Overdue => InvoiceStatus.Of(i, today) == InvoiceStatus.Overdue,
                InvoiceStatus.Paid => InvoiceStatus.Of(i, today) == InvoiceStatus.Paid,
                InvoiceStatus.Canceled => i.IsCanceled,
                _ => true
            }).ToList();

            var ordered = all.OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.CreatedAt).ToList();
            var page = Math.Max(1, r.Page);
            var size = Math.Clamp(r.PageSize, 1, 200);
            var originalIds = ordered.Where(i => i.CorrectedInvoiceId != null).Select(i => i.CorrectedInvoiceId!.Value).Distinct().ToList();
            var originalNumbers = await db.Invoices.AsNoTracking().Where(i => originalIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Number, ct);
            var items = ordered.Skip((page - 1) * size).Take(size).Select(i => new Item(i.Id, i.Type, i.Number, i.ContractorId,
                i.Contractor!.Name, i.IssueDate, i.DueDate, i.Net, i.Vat, i.Gross, i.PaidAmount, i.Remaining, InvoiceStatus.Of(i, today),
                i.IsCorrection, i.CorrectedInvoiceId, i.CorrectedInvoiceId != null && originalNumbers.TryGetValue(i.CorrectedInvoiceId.Value, out var on) ? on : null)).ToList();
            var live = ordered.Where(i => !i.IsCanceled).ToList();
            return new Response(items, ordered.Count, page, size, live.Sum(i => i.Gross), live.Sum(i => i.Remaining));
        }
    }
}

public static class GetInvoice
{
    public record Query(Guid Id) : IRequest<Response>;
    public record LineItem(string Description, decimal Net, VatRate VatRate, decimal Vat, decimal Gross);
    public record PaymentItem(Guid Id, DateTime Date, decimal Amount, string AccountCode, string AccountName, Guid TransactionId);
    public record Response(Guid Id, InvoiceType Type, string Number, Guid ContractorId, string ContractorName, string? ContractorNip,
        DateTime IssueDate, DateTime SaleDate, DateTime DueDate, decimal Net, decimal Vat, decimal Gross, decimal PaidAmount,
        decimal Remaining, string Status, string? Notes, Guid? TransactionId, string? TransactionNumber,
        List<LineItem> Lines, List<PaymentItem> Payments,
        bool IsCorrection, Guid? CorrectedInvoiceId, string? CorrectedNumber, decimal CorrectedAmount, decimal GrossAfterCorrections,
        List<CorrectionItem> Corrections);
    public record CorrectionItem(Guid Id, string Number, DateTime IssueDate, decimal Gross, bool IsCanceled);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var i = await db.Invoices.AsNoTracking().Include(x => x.Contractor).Include(x => x.Lines).Include(x => x.Payments)
                        .FirstOrDefaultAsync(x => x.Id == r.Id, ct)
                    ?? throw new DomainValidationException("Nie znaleziono faktury");
            var txNumber = i.TransactionId == null ? null
                : await db.Transactions.Where(t => t.Id == i.TransactionId).Select(t => t.Number).FirstOrDefaultAsync(ct);
            string? correctedNumber = i.CorrectedInvoiceId == null ? null
                : await db.Invoices.Where(x => x.Id == i.CorrectedInvoiceId).Select(x => x.Number).FirstOrDefaultAsync(ct);
            var corrections = await db.Invoices.AsNoTracking().Where(x => x.CorrectedInvoiceId == i.Id).OrderBy(x => x.IssueDate).ThenBy(x => x.CreatedAt).ToListAsync(ct);
            var accIds = i.Payments.Select(p => p.AccountId).ToList();
            var accounts = await db.Accounts.AsNoTracking().Where(a => accIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            return new Response(i.Id, i.Type, i.Number, i.ContractorId, i.Contractor!.Name, i.Contractor.Nip, i.IssueDate, i.SaleDate,
                i.DueDate, i.Net, i.Vat, i.Gross, i.PaidAmount, i.Remaining, InvoiceStatus.Of(i, DateTime.Today), i.Notes,
                i.TransactionId, txNumber,
                i.Lines.Select(l => new LineItem(l.Description, l.Net, l.VatRate, l.Vat, l.Gross)).ToList(),
                i.Payments.OrderBy(p => p.Date).Select(p => new PaymentItem(p.Id, p.Date, p.Amount, accounts[p.AccountId].Code,
                    accounts[p.AccountId].Name, p.TransactionId)).ToList(),
                i.IsCorrection, i.CorrectedInvoiceId, correctedNumber, i.CorrectedAmount, i.Gross + i.CorrectedAmount,
                corrections.Select(c => new CorrectionItem(c.Id, c.Number, c.IssueDate, c.Gross, c.IsCanceled)).ToList());
        }
    }
}

/// <summary>Rejestr VAT (sprzedaży lub zakupu) za okres wraz z podsumowaniem według stawek.</summary>
public static class VatRegister
{
    public record Query(InvoiceType Type, DateTime From, DateTime To) : IRequest<Response>;
    public record Row(Guid InvoiceId, string Number, DateTime IssueDate, DateTime SaleDate, string ContractorName, string? Nip,
        decimal Net, decimal Vat, decimal Gross);
    public record RateSummary(VatRate Rate, decimal Net, decimal Vat);
    public record Response(List<Row> Rows, List<RateSummary> ByRate, decimal TotalNet, decimal TotalVat, decimal TotalGross);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            // Okres rozliczeniowy VAT wyznaczamy po dacie wystawienia (uproszczenie)
            var invoices = await db.Invoices.AsNoTracking().Include(i => i.Contractor).Include(i => i.Lines)
                .Where(i => i.Type == r.Type && !i.IsCanceled && i.IssueDate >= r.From.Date && i.IssueDate <= r.To.Date).ToListAsync(ct);
            var ordered = invoices.OrderBy(i => i.IssueDate).ThenBy(i => i.Number).ToList();
            var byRate = ordered.SelectMany(i => i.Lines).GroupBy(l => l.VatRate)
                .Select(g => new RateSummary(g.Key, g.Sum(l => l.Net), g.Sum(l => l.Vat))).OrderBy(x => x.Rate).ToList();
            return new Response(
                ordered.Select(i => new Row(i.Id, i.Number, i.IssueDate, i.SaleDate, i.Contractor!.Name, i.Contractor.Nip, i.Net, i.Vat, i.Gross)).ToList(),
                byRate, ordered.Sum(i => i.Net), ordered.Sum(i => i.Vat), ordered.Sum(i => i.Gross));
        }
    }
}

/// <summary>Rozliczenie VAT: należny (sprzedaż) − naliczony (zakupy) = do zapłaty / do przeniesienia.</summary>
public static class VatSummary
{
    public record Query(DateTime From, DateTime To) : IRequest<Response>;
    public record Response(decimal OutputNet, decimal OutputVat, decimal InputNet, decimal InputVat, decimal VatDue);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var inv = await db.Invoices.AsNoTracking()
                .Where(i => !i.IsCanceled && i.IssueDate >= r.From.Date && i.IssueDate <= r.To.Date).ToListAsync(ct);
            var o = inv.Where(i => i.Type == InvoiceType.Sales).ToList();
            var p = inv.Where(i => i.Type == InvoiceType.Purchase).ToList();
            return new Response(o.Sum(i => i.Net), o.Sum(i => i.Vat), p.Sum(i => i.Net), p.Sum(i => i.Vat), o.Sum(i => i.Vat) - p.Sum(i => i.Vat));
        }
    }
}

/// <summary>Rozrachunki: należności (sprzedaż) lub zobowiązania (zakupy) z podziałem na przeterminowanie, per kontrahent.</summary>
public static class Settlements
{
    public record Query(InvoiceType Type) : IRequest<Response>;
    public record Row(Guid ContractorId, string ContractorName, string? Nip, decimal Total, decimal NotDue, decimal Days1To30,
        decimal Days31To60, decimal Over60, int InvoiceCount);
    public record Response(List<Row> Rows, decimal Total, decimal Overdue);

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var today = DateTime.Today;
            var open = (await db.Invoices.AsNoTracking().Include(i => i.Contractor).Where(i => i.Type == r.Type && !i.IsCanceled).ToListAsync(ct))
                .Where(i => i.Remaining > 0).ToList();
            var rows = open.GroupBy(i => i.ContractorId).Select(g =>
            {
                decimal Bucket(Func<int, bool> f) => g.Where(i => f((today - i.DueDate).Days)).Sum(i => i.Remaining);
                return new Row(g.Key, g.First().Contractor!.Name, g.First().Contractor!.Nip, g.Sum(i => i.Remaining),
                    Bucket(d => d <= 0), Bucket(d => d is >= 1 and <= 30), Bucket(d => d is >= 31 and <= 60), Bucket(d => d > 60), g.Count());
            }).OrderByDescending(x => x.Total).ToList();
            return new Response(rows, rows.Sum(x => x.Total), rows.Sum(x => x.Days1To30 + x.Days31To60 + x.Over60));
        }
    }
}
