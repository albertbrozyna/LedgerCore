using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

public class Transaction
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }
    public DateTime Date { get; private set; }

    // Lista zapisów (np. Winien 100, Ma 100)
    private readonly List<JournalEntry> _entries = new();

    // Udostępniamy listę tylko do odczytu, żeby nikt jej nie popsuł z zewnątrz
    public IReadOnlyCollection<JournalEntry> Entries => _entries.AsReadOnly();


    private Transaction() { }


    public Transaction(string description, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Opis jest wymagany");

        Id = Guid.NewGuid();
        Description = description;
        Date = date;
    }

    public void AddEntry(Guid accountId, decimal amount,DebitCredit side)
    {
        var entry = new JournalEntry(accountId, amount,side);

        entry.AssignToTransaction(this.Id);

        _entries.Add(entry);
    }

    // Walidacja - czy suma Winien równa się sumie Ma?
    public void Validate()
    {

        var debtSum =  _entries.Where(x => x.Side == DebitCredit.Debit)
            .Sum(x => x.Amount);

        var creditSum = _entries.Where(x => x.Side == DebitCredit.Credit)
            .Sum(x => x.Amount);

    
        if (creditSum != debtSum)
        {
            throw new UnbalancedTransactionException(debtSum,creditSum);
        }
    }

}