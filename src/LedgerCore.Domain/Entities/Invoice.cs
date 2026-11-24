using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

public class InvoiceLine : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Net { get; private set; }
    public VatRate VatRate { get; private set; }
    public decimal Vat { get; private set; }
    public decimal Gross => Net + Vat;

    private InvoiceLine() { }

    internal InvoiceLine(Guid invoiceId, string description, decimal net, VatRate rate, bool allowNegative = false)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainValidationException("Opis pozycji jest wymagany");
        if (allowNegative ? net == 0 : net <= 0) throw new DomainValidationException(allowNegative ? "Kwota netto pozycji korekty nie może być zerowa" : "Kwota netto pozycji musi być większa od zera");
        if (decimal.Round(net, 2) != net) throw new DomainValidationException("Kwoty mogą mieć maksymalnie 2 miejsca po przecinku");
        Id = Guid.NewGuid();
        InvoiceId = invoiceId;
        Description = description.Trim();
        Net = net;
        VatRate = rate;
        Vat = decimal.Round(net * rate.Percent() / 100m, 2, MidpointRounding.AwayFromZero);
    }
}

public class Payment : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public DateTime Date { get; private set; }
    public decimal Amount { get; private set; }
    public Guid AccountId { get; private set; }      // konto kasy/banku
    public Guid TransactionId { get; private set; }  // księgowanie płatności
    public DateTime CreatedAt { get; private set; }

    private Payment() { }

    internal Payment(Guid invoiceId, DateTime date, decimal amount, Guid accountId, Guid transactionId)
    {
        Id = Guid.NewGuid();
        InvoiceId = invoiceId; Date = date.Date; Amount = amount; AccountId = accountId;
        TransactionId = transactionId; CreatedAt = DateTime.UtcNow;
    }
}

public class Invoice : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public InvoiceType Type { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid ContractorId { get; private set; }
    public Contractor? Contractor { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime SaleDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public decimal Net { get; private set; }
    public decimal Vat { get; private set; }
    public decimal Gross { get; private set; }
    public decimal PaidAmount { get; private set; }
    public Guid? TransactionId { get; private set; }
    /// <summary>Konto przychodu (sprzedaż) lub kosztu (zakup), na które zaksięgowano netto.</summary>
    public Guid ResultAccountId { get; private set; }
    public bool IsCanceled { get; private set; }
    /// <summary>Dla faktury korygującej: id faktury korygowanej.</summary>
    public Guid? CorrectedInvoiceId { get; private set; }
    /// <summary>Dla faktury korygowanej: suma brutto zastosowanych korekt (może być ujemna).</summary>
    public decimal CorrectedAmount { get; private set; }
    public bool IsCorrection => CorrectedInvoiceId != null;
    public string? Notes { get; private set; }
    /// <summary>Klasyfikacja w KPiR (tylko firmy prowadzące KPiR; w pełnej księgowości null).</summary>
    public KpirKind? KpirKind { get; private set; }
    /// <summary>Stawka ryczałtu w % (tylko sprzedaż w firmach na ryczałcie).</summary>
    public decimal? RyczaltRate { get; private set; }
    /// <summary>Szablon faktury cyklicznej, z którego wygenerowano fakturę (null dla zwykłych faktur).</summary>
    public Guid? RecurringInvoiceId { get; private set; }
    /// <summary>Data okresu szablonu, za który wygenerowano fakturę – razem z szablonem unikalna (ochrona przed duplikatami).</summary>
    public DateTime? RecurringPeriod { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private readonly List<InvoiceLine> _lines = new();
    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();
    private readonly List<Payment> _payments = new();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    /// <summary>Do zapłaty po uwzględnieniu korekt. Korekta sama nie podlega płatności – rozlicza się ją na fakturze korygowanej.</summary>
    public decimal Remaining => IsCorrection ? 0 : Gross + CorrectedAmount - PaidAmount;

    private Invoice() { }

    public Invoice(InvoiceType type, string number, Guid contractorId, DateTime issueDate, DateTime saleDate, DateTime dueDate,
        Guid resultAccountId, string? notes, Guid? userId, IEnumerable<(string Description, decimal Net, VatRate Rate)> lines, Guid? correctedInvoiceId = null)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new DomainValidationException("Numer faktury jest wymagany");
        if (dueDate.Date < issueDate.Date) throw new DomainValidationException("Termin płatności nie może być wcześniejszy niż data wystawienia");
        Id = Guid.NewGuid();
        Type = type; Number = number.Trim(); ContractorId = contractorId;
        IssueDate = issueDate.Date; SaleDate = saleDate.Date; DueDate = dueDate.Date;
        ResultAccountId = resultAccountId; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAt = DateTime.UtcNow; CreatedByUserId = userId; CorrectedInvoiceId = correctedInvoiceId;
        foreach (var l in lines) _lines.Add(new InvoiceLine(Id, l.Description, l.Net, l.Rate, correctedInvoiceId != null));
        if (_lines.Count == 0) throw new DomainValidationException("Faktura wymaga co najmniej jednej pozycji");
        Net = _lines.Sum(l => l.Net); Vat = _lines.Sum(l => l.Vat); Gross = Net + Vat;
    }

    public void MarkRecurring(Guid recurringInvoiceId, DateTime period)
    {
        RecurringInvoiceId = recurringInvoiceId;
        RecurringPeriod = period.Date;
    }

    public void LinkTransaction(Guid transactionId) => TransactionId = transactionId;

    public void SetTaxClassification(KpirKind? kind, decimal? ryczaltRate)
    {
        KpirKind = kind;
        RyczaltRate = ryczaltRate;
    }

    public Payment AddPayment(DateTime date, decimal amount, Guid accountId, Guid transactionId)
    {
        if (IsCanceled) throw new DomainValidationException("Faktura została anulowana");
        if (IsCorrection) throw new DomainValidationException("Korekta nie podlega płatności – rozlicza się ją na fakturze korygowanej");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new DomainValidationException("Nieprawidłowa kwota płatności");
        if (amount > Remaining) throw new DomainValidationException($"Kwota płatności przekracza pozostałe do zapłaty ({Remaining:0.00})");
        var p = new Payment(Id, date, amount, accountId, transactionId);
        _payments.Add(p);
        PaidAmount += amount;
        return p;
    }

    public void Cancel()
    {
        if (IsCanceled) throw new DomainValidationException("Faktura jest już anulowana");
        if (PaidAmount > 0) throw new DomainValidationException("Nie można anulować faktury z zarejestrowanymi płatnościami");
        IsCanceled = true;
    }

    /// <summary>Stosuje korektę (kwota brutto z znakiem). Faktura po korekcie nie może być niższa niż już zapłacona kwota.</summary>
    public void ApplyCorrection(decimal gross)
    {
        if (IsCorrection) throw new DomainValidationException("Nie można korygować faktury korygującej");
        if (IsCanceled) throw new DomainValidationException("Faktura została anulowana");
        if (Gross + CorrectedAmount + gross < PaidAmount)
            throw new DomainValidationException("Korekta zmniejsza fakturę poniżej kwoty już zapłaconej – nadpłata wymaga zwrotu, który należy rozliczyć poza korektą");
        CorrectedAmount += gross;
    }

    /// <summary>Cofa wpływ korekty (przy jej anulowaniu).</summary>
    public void RevertCorrection(decimal gross)
    {
        if (Gross + CorrectedAmount - gross < PaidAmount)
            throw new DomainValidationException("Nie można anulować korekty – faktura byłaby niższa niż już zapłacona kwota");
        CorrectedAmount -= gross;
    }
}
