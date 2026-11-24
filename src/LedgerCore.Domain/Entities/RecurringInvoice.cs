using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

public enum RecurrenceFrequency { Monthly, Quarterly, Yearly }

public class RecurringInvoiceLine : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid RecurringInvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Net { get; private set; }
    public VatRate VatRate { get; private set; }

    private RecurringInvoiceLine() { }

    internal RecurringInvoiceLine(Guid recurringId, string description, decimal net, VatRate rate)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainValidationException("Opis pozycji jest wymagany");
        if (net <= 0) throw new DomainValidationException("Kwota netto pozycji musi być większa od zera");
        if (decimal.Round(net, 2) != net) throw new DomainValidationException("Kwoty mogą mieć maksymalnie 2 miejsca po przecinku");
        Id = Guid.NewGuid();
        RecurringInvoiceId = recurringId;
        Description = description.Trim();
        Net = net;
        VatRate = rate;
    }
}

/// <summary>
/// Szablon faktury sprzedaży wystawianej cyklicznie (abonament). <see cref="NextDate"/> to data wystawienia następnej faktury;
/// po wygenerowaniu przesuwa się o okres. Każda wygenerowana faktura pamięta szablon i datę okresu (unikalnie), więc nie powstanie duplikat.
/// </summary>
public class RecurringInvoice : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid ContractorId { get; private set; }
    public Contractor? Contractor { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }
    /// <summary>Dzień miesiąca wystawienia (1–31; w krótszych miesiącach – ostatni dzień miesiąca).</summary>
    public int DayOfMonth { get; private set; }
    public int DueDays { get; private set; }
    public DateTime NextDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? ResultAccountId { get; private set; }
    public KpirKind? KpirKind { get; private set; }
    public decimal? RyczaltRate { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<RecurringInvoiceLine> _lines = new();
    public IReadOnlyCollection<RecurringInvoiceLine> Lines => _lines.AsReadOnly();

    private RecurringInvoice() { }

    public RecurringInvoice(string name, Guid contractorId, RecurrenceFrequency frequency, int dayOfMonth, int dueDays, DateTime firstDate,
        DateTime? endDate, Guid? resultAccountId, KpirKind? kpirKind, decimal? ryczaltRate, string? notes,
        IEnumerable<(string Description, decimal Net, VatRate Rate)> lines)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Update(name, contractorId, frequency, dayOfMonth, dueDays, firstDate, endDate, resultAccountId, kpirKind, ryczaltRate, notes, lines);
    }

    public void Update(string name, Guid contractorId, RecurrenceFrequency frequency, int dayOfMonth, int dueDays, DateTime nextDate,
        DateTime? endDate, Guid? resultAccountId, KpirKind? kpirKind, decimal? ryczaltRate, string? notes,
        IEnumerable<(string Description, decimal Net, VatRate Rate)> lines)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainValidationException("Nazwa szablonu jest wymagana");
        if (!Enum.IsDefined(frequency)) throw new DomainValidationException("Nieprawidłowa częstotliwość");
        if (dayOfMonth is < 1 or > 31) throw new DomainValidationException("Dzień wystawienia musi być z zakresu 1–31");
        if (dueDays is < 0 or > 365) throw new DomainValidationException("Termin płatności musi mieścić się w zakresie 0–365 dni");
        if (nextDate.Year is < 2000 or > 2100) throw new DomainValidationException("Nieprawidłowa data następnej faktury");
        if (endDate is { } e && e.Date < nextDate.Date) throw new DomainValidationException("Data zakończenia nie może być wcześniejsza niż data następnej faktury");
        var list = lines.ToList();
        if (list.Count == 0) throw new DomainValidationException("Szablon wymaga co najmniej jednej pozycji");

        Name = name.Trim(); ContractorId = contractorId; Frequency = frequency; DayOfMonth = dayOfMonth; DueDays = dueDays;
        NextDate = nextDate.Date; EndDate = endDate?.Date; ResultAccountId = resultAccountId; KpirKind = kpirKind; RyczaltRate = ryczaltRate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _lines.Clear();
        foreach (var l in list) _lines.Add(new RecurringInvoiceLine(Id, l.Description, l.Net, l.Rate));
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>Czy szablon ma jeszcze coś do wygenerowania na dzień <paramref name="asOf"/>.</summary>
    public bool IsDue(DateTime asOf) => IsActive && NextDate <= asOf.Date && (EndDate == null || NextDate <= EndDate);

    public int MonthsPerPeriod => Frequency switch { RecurrenceFrequency.Monthly => 1, RecurrenceFrequency.Quarterly => 3, _ => 12 };

    /// <summary>Przesuwa następną datę o jeden okres (z zachowaniem dnia miesiąca, w krótszych miesiącach – ostatni dzień).</summary>
    public void Advance()
    {
        var first = new DateTime(NextDate.Year, NextDate.Month, 1).AddMonths(MonthsPerPeriod);
        NextDate = new DateTime(first.Year, first.Month, Math.Min(DayOfMonth, DateTime.DaysInMonth(first.Year, first.Month)));
        if (EndDate != null && NextDate > EndDate) IsActive = false;
    }
}
