using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

public class Transaction : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    /// <summary>Kolejny numer dokumentu, np. DK/2026/0001</summary>
    public string Number { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    /// <summary>Ustawione tylko dla storna – id księgowania, które to storno odwraca.</summary>
    public Guid? ReversesTransactionId { get; private set; }

    private readonly List<JournalEntry> _entries = new();
    public IReadOnlyCollection<JournalEntry> Entries => _entries.AsReadOnly();

    private Transaction() { }

    public Transaction(string description, DateTime date, string number, Guid? createdByUserId = null, Guid? reversesTransactionId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Opis jest wymagany");

        Id = Guid.NewGuid();
        Number = number;
        Description = description.Trim();
        Date = date.Date;
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
        ReversesTransactionId = reversesTransactionId;
    }

    public void AddEntry(Guid accountId, decimal amount, DebitCredit side)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new DomainValidationException("Kwoty mogą mieć maksymalnie 2 miejsca po przecinku");

        var entry = new JournalEntry(accountId, amount, side);
        entry.AssignToTransaction(Id);
        _entries.Add(entry);
    }

    public decimal TotalDebit => _entries.Where(x => x.Side == DebitCredit.Debit).Sum(x => x.Amount);
    public decimal TotalCredit => _entries.Where(x => x.Side == DebitCredit.Credit).Sum(x => x.Amount);

    /// <summary>Zasada podwójnego zapisu: min. 2 zapisy i suma Winien == suma Ma.</summary>
    public void Validate()
    {
        if (_entries.Count < 2)
            throw new DomainValidationException("Księgowanie wymaga co najmniej dwóch zapisów (Winien i Ma)");

        if (TotalDebit != TotalCredit)
            throw new UnbalancedTransactionException(TotalDebit, TotalCredit);
    }

    /// <summary>Tworzy storno – zapisy z odwróconymi stronami.</summary>
    public Transaction CreateReversal(string number, DateTime date, Guid? userId)
    {
        var reversal = new Transaction($"Storno {Number}: {Description}", date, number, userId, Id);
        foreach (var e in _entries)
            reversal.AddEntry(e.AccountId, e.Amount, e.Side == DebitCredit.Debit ? DebitCredit.Credit : DebitCredit.Debit);
        reversal.Validate();
        return reversal;
    }
}
