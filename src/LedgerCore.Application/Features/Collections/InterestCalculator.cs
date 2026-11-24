using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Application.Features.Collections;

/// <summary>
/// Odsetki za opóźnienie liczone dziennie: kapitał × stopa roczna / 100 / 365 za każdy dzień zwłoki.
/// Zwłoka trwa od dnia następnego po terminie płatności do dnia obliczenia włącznie (wpłata w danym dniu zatrzymuje odsetki
/// dopiero od dnia następnego, czyli dzień wpłaty jest jeszcze oprocentowany). Kapitał zmniejszają wpłaty (płatności częściowe),
/// a stopa zmienia się wg tabeli stóp z datami obowiązywania. Wynik zaokrąglany raz, na końcu, do groszy (od 0,5 w górę).
/// Źródło wzoru: art. 481 Kodeksu cywilnego (odsetki w skali roku); stopy nie są zaszyte w kodzie – pochodzą z tabeli edytowanej przez administratora.
/// </summary>
public static class InterestCalculator
{
    public record Result(decimal Interest, int Days, string RateInfo);

    public static Result Compute(decimal amountDue, IEnumerable<(DateTime Date, decimal Amount)> payments, DateTime dueDate,
        DateTime asOf, IReadOnlyList<(DateTime From, decimal Percent)> rates)
    {
        var start = dueDate.Date.AddDays(1);
        var end = asOf.Date;
        if (end < start) return new Result(0m, 0, "");

        var sorted = rates.OrderBy(r => r.From).ToList();
        if (sorted.Count == 0 || sorted[0].From.Date > start)
            throw new DomainValidationException($"Brak stopy odsetek obowiązującej od {start:yyyy-MM-dd} – uzupełnij tabelę stóp odsetek w Administracji");
        var pays = payments.Select(p => (Date: p.Date.Date, p.Amount)).OrderBy(p => p.Date).ToList();

        decimal sum = 0m;
        var days = 0;
        var used = new List<decimal>();
        var rateIdx = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var principal = amountDue - pays.Where(p => p.Date < d).Sum(p => p.Amount);
            if (principal <= 0) break;
            while (rateIdx + 1 < sorted.Count && sorted[rateIdx + 1].From.Date <= d) rateIdx++;
            var pct = sorted[rateIdx].Percent;
            if (used.Count == 0 || used[^1] != pct) used.Add(pct);
            sum += principal * pct / 100m / 365m;
            days++;
        }
        var info = string.Join(" → ", used.Select(p => p.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pl-PL")) + "%"));
        return new Result(decimal.Round(sum, 2, MidpointRounding.AwayFromZero), days, info);
    }
}
