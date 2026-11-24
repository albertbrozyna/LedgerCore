using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Registers;

/// <summary>
/// Reguły ewidencji podatkowych (KPiR, ryczałt) – oparte o rozporządzenie Ministra Finansów w sprawie prowadzenia podatkowej
/// księgi przychodów i rozchodów oraz ustawę o zryczałtowanym podatku dochodowym (stawki). Przyjęte uproszczenia:
/// kwoty to wartości NETTO faktur (czynny podatnik VAT; dla stawek zwolnionych netto = brutto), data zdarzenia to data
/// sprzedaży (sprzedaż) lub data wystawienia (zakup i korekty), korekty wchodzą ze znakiem w dacie wystawienia korekty,
/// faktury anulowane są pomijane. Wynagrodzenia (kol. 12) i pozostałe zapisy bez faktur wymagają ręcznych wpisów (poza zakresem).
/// </summary>
public static class RegisterRules
{
    /// <summary>Dozwolone stawki ryczałtu ewidencjonowanego (%).</summary>
    public static readonly decimal[] RyczaltRates = [2m, 3m, 5.5m, 8.5m, 10m, 12m, 12.5m, 14m, 15m, 17m, 20m];

    public static (KpirKind? Kind, decimal? Rate) Classify(AccountingMode mode, InvoiceType type, KpirKind? kind, decimal? rate)
    {
        var sales = type == InvoiceType.Sales;
        switch (mode)
        {
            case AccountingMode.Kpir:
                var k = kind ?? (sales ? KpirKind.Revenue : KpirKind.OtherExpense);
                if (sales && !k.IsRevenue()) throw new DomainValidationException("Faktura sprzedaży może być wpisana do KPiR tylko jako przychód (kolumna 7 lub 8)");
                if (!sales && k.IsRevenue()) throw new DomainValidationException("Faktura zakupu może być wpisana do KPiR tylko jako wydatek (kolumna 10, 11 lub 13)");
                return (k, null);
            case AccountingMode.Ryczalt:
                if (!sales) return (null, null); // zakupy nie wchodzą do ewidencji przychodów ryczałtu (tylko rejestry VAT)
                if (rate is null) throw new DomainValidationException("Podaj stawkę ryczałtu dla faktury sprzedaży");
                if (!RyczaltRates.Contains(rate.Value))
                    throw new DomainValidationException($"Nieprawidłowa stawka ryczałtu {rate}% (dozwolone: {string.Join(", ", RyczaltRates)})");
                return (null, rate);
            default:
                return (null, null);
        }
    }

    /// <summary>Data zdarzenia gospodarczego: sprzedaż – data sprzedaży, zakup i korekty – data wystawienia.</summary>
    public static DateTime EventDate(Invoice i) => i.Type == InvoiceType.Sales && !i.IsCorrection ? i.SaleDate : i.IssueDate;

    static string Describe(Invoice i)
    {
        var text = string.Join("; ", i.Lines.Select(l => l.Description));
        return text.Length > 120 ? text[..117] + "…" : text;
    }

    internal static string Describe2(Invoice i) => Describe(i);

    /// <summary>Podatek od przychodu wg stawki, zaokrąglony do pełnych złotych (grosze od 50 w górę).</summary>
    public static decimal RyczaltTax(decimal revenue, decimal rate) =>
        Math.Max(0, Math.Round(revenue * rate / 100m, 0, MidpointRounding.AwayFromZero));

    /// <summary>Zaliczka wg stawki liniowej 19% – tylko informacyjnie (forma opodatkowania podatnika nie jest przechowywana).</summary>
    public static decimal LinearAdvance(decimal income) => Math.Max(0, Math.Round(income * 0.19m, 0, MidpointRounding.AwayFromZero));
}

/// <summary>Podatkowa księga przychodów i rozchodów jako widok nad fakturami (bez kopiowania danych).</summary>
public static class KpirRegister
{
    public record Query(DateTime From, DateTime To) : IRequest<Response>;

    public record Totals(decimal Revenue, decimal OtherRevenue, decimal TotalRevenue, decimal GoodsPurchase, decimal PurchaseCosts,
        decimal Wages, decimal OtherExpense, decimal TotalExpense, decimal Research);

    public record Row(int Lp, Guid InvoiceId, DateTime Date, string Document, string Contractor, string? Address, string Description,
        decimal Revenue, decimal OtherRevenue, decimal TotalRevenue, decimal GoodsPurchase, decimal PurchaseCosts, decimal Wages,
        decimal OtherExpense, decimal TotalExpense, decimal Research, string? Remarks);

    /// <summary>Podsumowanie miesiąca: obroty miesiąca i narastająco od początku roku (wraz z dochodem i informacyjną zaliczką).</summary>
    public record MonthSummary(string Month, Totals Period, Totals YearToDate, decimal Income, decimal AdvanceInfo);

    public record Response(List<Row> Rows, Totals Period, List<MonthSummary> Months, Totals YearToDate, decimal Income, decimal AdvanceInfo);

    static Totals Sum(IEnumerable<Row> rows)
    {
        var r = rows.ToList();
        return new Totals(r.Sum(x => x.Revenue), r.Sum(x => x.OtherRevenue), r.Sum(x => x.TotalRevenue), r.Sum(x => x.GoodsPurchase),
            r.Sum(x => x.PurchaseCosts), r.Sum(x => x.Wages), r.Sum(x => x.OtherExpense), r.Sum(x => x.TotalExpense), r.Sum(x => x.Research));
    }

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query q, CancellationToken ct)
        {
            var from = q.From.Date; var to = q.To.Date;
            if (to < from) throw new DomainValidationException("Data końcowa nie może być wcześniejsza niż początkowa");
            if (from.Year != to.Year) throw new DomainValidationException("Ewidencja KPiR dotyczy jednego roku podatkowego – wybierz zakres w obrębie roku");

            var yearStart = new DateTime(from.Year, 1, 1);
            var invoices = (await db.Invoices.AsNoTracking().Include(i => i.Contractor).Include(i => i.Lines)
                    .Where(i => !i.IsCanceled && i.KpirKind != null).ToListAsync(ct))
                .Where(i => RegisterRules.EventDate(i) >= yearStart && RegisterRules.EventDate(i) <= to)
                .OrderBy(RegisterRules.EventDate).ThenBy(i => i.IssueDate).ThenBy(i => i.CreatedAt).ThenBy(i => i.Number).ToList();

            var all = invoices.Select((i, idx) =>
            {
                var k = i.KpirKind!.Value;
                var net = i.Net;
                var rev = k == KpirKind.Revenue ? net : 0m;
                var oth = k == KpirKind.OtherRevenue ? net : 0m;
                var goods = k == KpirKind.GoodsPurchase ? net : 0m;
                var side = k == KpirKind.PurchaseCosts ? net : 0m;
                var other = k is KpirKind.OtherExpense or KpirKind.Research ? net : 0m;
                var remarks = i.IsCorrection ? "korekta" : null;
                return new Row(idx + 1, i.Id, RegisterRules.EventDate(i), i.Number, i.Contractor!.Name, i.Contractor.Address, RegisterRules.Describe2(i),
                    rev, oth, rev + oth, goods, side, 0m, other, goods + side + other, k == KpirKind.Research ? net : 0m, remarks);
            }).ToList();

            var rows = all.Where(r => r.Date >= from).ToList();
            var months = new List<MonthSummary>();
            for (var m = new DateTime(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
            {
                var monthRows = all.Where(r => r.Date.Month == m.Month && r.Date >= from && r.Date <= to).ToList();
                var ytd = Sum(all.Where(r => r.Date <= new DateTime(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month))));
                var inc = ytd.TotalRevenue - ytd.TotalExpense;
                months.Add(new MonthSummary(m.ToString("yyyy-MM"), Sum(monthRows), ytd, inc, RegisterRules.LinearAdvance(inc)));
            }
            var total = Sum(all.Where(r => r.Date <= to));
            var income = total.TotalRevenue - total.TotalExpense;
            return new Response(rows, Sum(rows), months, total, income, RegisterRules.LinearAdvance(income));
        }
    }
}

/// <summary>Ewidencja przychodów ryczałtu ewidencjonowanego: sprzedaż ze stawką ryczałtu; podatek (informacyjnie) liczony od sum wg stawek.</summary>
public static class RyczaltRegister
{
    public record Query(DateTime From, DateTime To) : IRequest<Response>;

    public record Row(int Lp, Guid InvoiceId, DateTime Date, string Document, string Contractor, string Description, decimal Rate, decimal Revenue);
    public record RateTotal(decimal Rate, decimal Revenue, decimal Tax);
    public record MonthSummary(string Month, decimal Revenue, decimal YearToDateRevenue, decimal YearToDateTax);
    public record Response(List<Row> Rows, decimal TotalRevenue, List<RateTotal> ByRate, List<RateTotal> YearToDateByRate, List<MonthSummary> Months, decimal YearToDateTax);

    static List<RateTotal> ByRate(IEnumerable<Row> rows) =>
        rows.GroupBy(r => r.Rate).OrderBy(g => g.Key).Select(g => new RateTotal(g.Key, g.Sum(r => r.Revenue), RegisterRules.RyczaltTax(g.Sum(r => r.Revenue), g.Key))).ToList();

    public class Handler(IAppDbContext db) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query q, CancellationToken ct)
        {
            var from = q.From.Date; var to = q.To.Date;
            if (to < from) throw new DomainValidationException("Data końcowa nie może być wcześniejsza niż początkowa");
            if (from.Year != to.Year) throw new DomainValidationException("Ewidencja ryczałtu dotyczy jednego roku podatkowego – wybierz zakres w obrębie roku");

            var yearStart = new DateTime(from.Year, 1, 1);
            var invoices = (await db.Invoices.AsNoTracking().Include(i => i.Contractor).Include(i => i.Lines)
                    .Where(i => !i.IsCanceled && i.Type == InvoiceType.Sales && i.RyczaltRate != null).ToListAsync(ct))
                .Where(i => RegisterRules.EventDate(i) >= yearStart && RegisterRules.EventDate(i) <= to)
                .OrderBy(RegisterRules.EventDate).ThenBy(i => i.IssueDate).ThenBy(i => i.CreatedAt).ThenBy(i => i.Number).ToList();

            var all = invoices.Select((i, idx) => new Row(idx + 1, i.Id, RegisterRules.EventDate(i), i.IsCorrection ? $"{i.Number} (korekta)" : i.Number,
                i.Contractor!.Name, RegisterRules.Describe2(i), i.RyczaltRate!.Value, i.Net)).ToList();

            var rows = all.Where(r => r.Date >= from).ToList();
            var months = new List<MonthSummary>();
            for (var m = new DateTime(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
            {
                var end = new DateTime(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month));
                var ytd = all.Where(r => r.Date <= end).ToList();
                months.Add(new MonthSummary(m.ToString("yyyy-MM"), rows.Where(r => r.Date.Month == m.Month && r.Date <= to).Sum(r => r.Revenue),
                    ytd.Sum(r => r.Revenue), ByRate(ytd).Sum(t => t.Tax)));
            }
            var ytdAll = all.Where(r => r.Date <= to).ToList();
            var ytdRates = ByRate(ytdAll);
            return new Response(rows, rows.Sum(r => r.Revenue), ByRate(rows), ytdRates, months, ytdRates.Sum(t => t.Tax));
        }
    }
}
