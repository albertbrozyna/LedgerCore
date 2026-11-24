using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Invoicing;

/// <summary>Numeracja faktur sprzedaży według wzoru ustawionego w firmie (domyślnie FV/{RRRR}/{NNNN}).</summary>
public static class SalesNumbers
{
    public static async Task<NumberPattern> PatternFor(IAppDbContext db, CancellationToken ct)
    {
        var c = await db.Companies.AsNoTracking().Where(x => x.Id == db.CurrentCompanyId)
            .Select(x => new { x.SalesNumberPattern, x.SalesNumberReset }).FirstOrDefaultAsync(ct);
        return c is null ? NumberPattern.Parse(NumberPattern.Default, NumberReset.Yearly) : NumberPattern.Parse(c.SalesNumberPattern, c.SalesNumberReset);
    }

    /// <summary>Następny wolny numer dla daty wystawienia (numer kolejny liczony w okresie numeracji, nigdy nie powtarza istniejącego).</summary>
    public static async Task<string> Next(IAppDbContext db, DateTime date, CancellationToken ct)
    {
        var pattern = await PatternFor(db, ct);
        var prefix = pattern.LiteralPrefix;
        var q = db.Invoices.AsNoTracking().Where(i => i.Type == InvoiceType.Sales);
        if (prefix.Length > 0) q = q.Where(i => i.Number.StartsWith(prefix));
        var numbers = await q.Select(i => i.Number).ToListAsync(ct);
        var candidate = pattern.Format(date, pattern.NextSequence(date, numbers));
        // wzór bez zerowania (lub z ręcznie zmienionym wzorem) mógłby trafić w istniejący numer – szukamy pierwszego wolnego
        var taken = (await db.Invoices.AsNoTracking().Where(i => i.Type == InvoiceType.Sales && i.Number == candidate).AnyAsync(ct));
        if (taken) throw new DomainValidationException($"Numer {candidate} jest już zajęty – sprawdź wzór numeracji w ustawieniach firmy");
        return candidate;
    }
}

public static class NumberingSettings
{
    public record Response(string Pattern, NumberReset Reset, string NextExample);

    public record GetQuery : IRequest<Response>;
    public class GetHandler(IAppDbContext db) : IRequestHandler<GetQuery, Response>
    {
        public async Task<Response> Handle(GetQuery r, CancellationToken ct)
        {
            var p = await SalesNumbers.PatternFor(db, ct);
            return new Response(p.Text, p.Reset, await SalesNumbers.Next(db, DateTime.Today, ct));
        }
    }

    public record PreviewQuery(string Pattern, NumberReset Reset, DateTime? Date) : IRequest<string>;
    public class PreviewHandler(IAppDbContext db) : IRequestHandler<PreviewQuery, string>
    {
        public async Task<string> Handle(PreviewQuery r, CancellationToken ct)
        {
            var p = NumberPattern.Parse(r.Pattern, r.Reset);
            var date = r.Date ?? DateTime.Today;
            var prefix = p.LiteralPrefix;
            var q = db.Invoices.AsNoTracking().Where(i => i.Type == InvoiceType.Sales);
            if (prefix.Length > 0) q = q.Where(i => i.Number.StartsWith(prefix));
            var numbers = await q.Select(i => i.Number).ToListAsync(ct);
            return p.Format(date, p.NextSequence(date, numbers));
        }
    }

    public record SaveCommand(string Pattern, NumberReset Reset) : IRequest<Unit>;
    public class SaveHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<SaveCommand, Unit>
    {
        public async Task<Unit> Handle(SaveCommand r, CancellationToken ct)
        {
            var c = await db.Companies.FirstOrDefaultAsync(x => x.Id == db.CurrentCompanyId, ct) ?? throw new DomainValidationException("Nie wybrano firmy");
            c.SetNumbering(r.Pattern, r.Reset);
            await audit.Add("Dane firmy", $"Zmieniono wzór numeracji faktur sprzedaży: {c.SalesNumberPattern} ({c.SalesNumberReset})", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
