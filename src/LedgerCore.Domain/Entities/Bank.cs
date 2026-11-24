using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

public enum BankTransactionStatus { New, Posted, Skipped }
public enum RuleDirection { Any, In, Out }

/// <summary>Jeden zaimportowany plik wyciągu (z saldami z pliku, jeśli format je zawiera – MT940).</summary>
public class BankStatement : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string Format { get; private set; } = string.Empty;
    public DateTime ImportedAt { get; private set; }
    public string ImportedBy { get; private set; } = string.Empty;
    public decimal? OpeningBalance { get; private set; }
    public decimal? ClosingBalance { get; private set; }
    public DateTime? ClosingDate { get; private set; }
    public int ImportedCount { get; private set; }
    public int DuplicateCount { get; private set; }
    public int SkippedCount { get; private set; }

    private BankStatement() { }

    public BankStatement(Guid accountId, string fileName, string format, string importedBy, decimal? opening, decimal? closing, DateTime? closingDate,
        int imported, int duplicates, int skipped)
    {
        Id = Guid.NewGuid();
        AccountId = accountId; FileName = fileName.Trim(); Format = format; ImportedBy = importedBy; ImportedAt = DateTime.UtcNow;
        OpeningBalance = opening; ClosingBalance = closing; ClosingDate = closingDate?.Date;
        ImportedCount = imported; DuplicateCount = duplicates; SkippedCount = skipped;
    }
}

/// <summary>Operacja z wyciągu. Skrót (<see cref="Hash"/>) jest unikalny na rachunek – ponowny import tych samych operacji ich nie dubluje.</summary>
public class BankTransaction : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid StatementId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTime Date { get; private set; }
    public decimal Amount { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Counterparty { get; private set; } = string.Empty;
    public string CounterpartyAccount { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public BankTransactionStatus Status { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public Guid? RuleId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private BankTransaction() { }

    public BankTransaction(Guid statementId, Guid accountId, DateTime date, decimal amount, string title, string counterparty, string counterpartyAccount, string hash)
    {
        if (amount == 0) throw new DomainValidationException("Kwota operacji nie może być zerowa");
        Id = Guid.NewGuid();
        StatementId = statementId; AccountId = accountId; Date = date.Date; Amount = amount;
        Title = title.Length > 500 ? title[..500] : title;
        Counterparty = counterparty.Length > 200 ? counterparty[..200] : counterparty;
        CounterpartyAccount = counterpartyAccount.Length > 40 ? counterpartyAccount[..40] : counterpartyAccount;
        Hash = hash;
    }

    public void EnsureNew()
    {
        if (Status == BankTransactionStatus.Posted) throw new DomainValidationException("Operacja jest już zaksięgowana");
        if (Status == BankTransactionStatus.Skipped) throw new DomainValidationException("Operacja jest oznaczona jako pominięta – przywróć ją, aby ją zaksięgować");
    }

    public void MarkPostedToInvoice(Guid invoiceId)
    {
        EnsureNew();
        Status = BankTransactionStatus.Posted; InvoiceId = invoiceId; ProcessedAt = DateTime.UtcNow;
    }

    public void MarkPostedByRule(Guid transactionId, Guid ruleId)
    {
        EnsureNew();
        Status = BankTransactionStatus.Posted; TransactionId = transactionId; RuleId = ruleId; ProcessedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        EnsureNew();
        Status = BankTransactionStatus.Skipped; ProcessedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        if (Status != BankTransactionStatus.Skipped) throw new DomainValidationException("Przywrócić można tylko pominiętą operację");
        Status = BankTransactionStatus.New; ProcessedAt = null;
    }
}

/// <summary>Reguła: „jeśli opis lub kontrahent zawiera tekst – zaksięguj na wskazane konto” (np. opłaty bankowe, odsetki).</summary>
public class BankRule : ICompanyOwned
{
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string MatchText { get; private set; } = string.Empty;
    public RuleDirection Direction { get; private set; }
    public Guid TargetAccountId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private BankRule() { }

    public BankRule(string name, string matchText, RuleDirection direction, Guid targetAccountId, string? description, bool isActive)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Update(name, matchText, direction, targetAccountId, description, isActive);
    }

    public void Update(string name, string matchText, RuleDirection direction, Guid targetAccountId, string? description, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainValidationException("Nazwa reguły jest wymagana");
        if (string.IsNullOrWhiteSpace(matchText) || matchText.Trim().Length < 3) throw new DomainValidationException("Tekst do dopasowania musi mieć co najmniej 3 znaki");
        if (!Enum.IsDefined(direction)) throw new DomainValidationException("Nieprawidłowy kierunek reguły");
        Name = name.Trim(); MatchText = matchText.Trim(); Direction = direction; TargetAccountId = targetAccountId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(); IsActive = isActive;
    }

    public bool Matches(decimal amount, string title, string counterparty)
    {
        if (!IsActive) return false;
        if (Direction == RuleDirection.In && amount <= 0) return false;
        if (Direction == RuleDirection.Out && amount >= 0) return false;
        return $"{title} {counterparty}".Contains(MatchText, StringComparison.OrdinalIgnoreCase);
    }
}
