using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

/// <summary>Rodzaj dokumentu windykacyjnego.</summary>
public enum NoticeKind { Reminder, InterestNote }

/// <summary>Sposób doręczenia: wydruk (rejestrujemy wystawienie) lub e-mail wysłany z programu.</summary>
public enum NoticeChannel { Print, Email }

/// <summary>Issued – wystawiony do wydruku, Sent – e-mail wysłany, Failed – wysyłka nieudana (błąd w <see cref="CollectionNotice.Error"/>).</summary>
public enum NoticeStatus { Issued, Sent, Failed }

/// <summary>
/// Stopa odsetek (w skali roku, %) obowiązująca od podanej daty. Tabela stóp jest wspólna dla całej instalacji
/// (stopy ustawowe nie zależą od firmy) i edytowana przez administratora – stopy zmieniają się w czasie, więc nie ma ich w kodzie.
/// </summary>
public class InterestRate
{
    public Guid Id { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public decimal AnnualPercent { get; private set; }
    public string? Note { get; private set; }

    private InterestRate() { }

    public InterestRate(DateTime validFrom, decimal annualPercent, string? note)
    {
        if (annualPercent < 0 || annualPercent > 100) throw new DomainValidationException("Stopa odsetek musi mieścić się w przedziale 0–100%");
        if (decimal.Round(annualPercent, 2) != annualPercent) throw new DomainValidationException("Stopa odsetek może mieć najwyżej 2 miejsca po przecinku");
        Id = Guid.NewGuid();
        ValidFrom = validFrom.Date;
        AnnualPercent = annualPercent;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>Ustawienia poczty wychodzącej firmy (SMTP) i szablony wiadomości. Hasło jest przechowywane wyłącznie zaszyfrowane.</summary>
public class MailSettings : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 587;
    /// <summary>true = STARTTLS/SSL, false = bez szyfrowania (tylko serwery lokalne).</summary>
    public bool UseTls { get; private set; } = true;
    public string? UserName { get; private set; }
    /// <summary>Hasło zaszyfrowane (DataProtection) – nigdy zwracane przez API.</summary>
    public string? ProtectedPassword { get; private set; }
    public string FromAddress { get; private set; } = string.Empty;
    public string? FromName { get; private set; }
    public string? ReminderSubject { get; private set; }
    public string? ReminderBody { get; private set; }
    public string? InterestSubject { get; private set; }
    public string? InterestBody { get; private set; }

    private MailSettings() { }
    public MailSettings(Guid id) { Id = id; }

    public void Update(string host, int port, bool useTls, string? userName, string fromAddress, string? fromName,
        string? reminderSubject, string? reminderBody, string? interestSubject, string? interestBody)
    {
        string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        Host = host.Trim(); Port = port; UseTls = useTls; UserName = N(userName); FromAddress = fromAddress.Trim(); FromName = N(fromName);
        ReminderSubject = N(reminderSubject); ReminderBody = N(reminderBody);
        InterestSubject = N(interestSubject); InterestBody = N(interestBody);
    }

    /// <summary>Ustawia zaszyfrowane hasło; null usuwa je.</summary>
    public void SetProtectedPassword(string? protectedPassword) => ProtectedPassword = protectedPassword;
}

/// <summary>Wystawiony dokument windykacyjny (wezwanie do zapłaty / nota odsetkowa) z migawką treści – dokument da się odtworzyć po zmianie danych.</summary>
public class CollectionNotice : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid ContractorId { get; private set; }
    public NoticeKind Kind { get; private set; }
    public NoticeChannel Channel { get; private set; }
    public NoticeStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CalculationDate { get; private set; }
    public DateTime PaymentDeadline { get; private set; }
    public decimal Principal { get; private set; }
    public decimal Interest { get; private set; }
    public string? SentTo { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    /// <summary>Migawka pozycji (JSON): numer faktury, termin, kapitał, dni, odsetki.</summary>
    public string LinesJson { get; private set; } = "[]";
    public string? Error { get; private set; }

    private readonly List<CollectionNoticeInvoice> _invoices = new();
    public IReadOnlyCollection<CollectionNoticeInvoice> Invoices => _invoices.AsReadOnly();

    private CollectionNotice() { }

    public CollectionNotice(Guid contractorId, NoticeKind kind, NoticeChannel channel, string createdBy, DateTime calculationDate,
        DateTime paymentDeadline, decimal principal, decimal interest, string subject, string body, string linesJson, IEnumerable<Guid> invoiceIds)
    {
        Id = Guid.NewGuid(); ContractorId = contractorId; Kind = kind; Channel = channel; CreatedAt = DateTime.UtcNow; CreatedBy = createdBy;
        CalculationDate = calculationDate.Date; PaymentDeadline = paymentDeadline.Date; Principal = principal; Interest = interest;
        Subject = subject; Body = body; LinesJson = linesJson; Status = NoticeStatus.Issued;
        foreach (var i in invoiceIds.Distinct()) _invoices.Add(new CollectionNoticeInvoice(Id, i));
    }

    public void MarkSent(string to) { Status = NoticeStatus.Sent; SentTo = to; Error = null; }
    public void MarkFailed(string to, string error) { Status = NoticeStatus.Failed; SentTo = to; Error = error.Length > 500 ? error[..500] : error; }
}

public class CollectionNoticeInvoice : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid NoticeId { get; private set; }
    public Guid InvoiceId { get; private set; }

    private CollectionNoticeInvoice() { }
    internal CollectionNoticeInvoice(Guid noticeId, Guid invoiceId) { Id = Guid.NewGuid(); NoticeId = noticeId; InvoiceId = invoiceId; }
}
