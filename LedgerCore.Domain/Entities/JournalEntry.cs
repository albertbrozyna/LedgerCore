using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using System.ComponentModel.DataAnnotations.Schema;

namespace LedgerCore.Domain.Entities;

public class JournalEntry
{
    public Guid Id { get; private set; }

    // RELACJE
    public Guid TransactionId { get; private set; } // Rodzic (Transakcja)
    [ForeignKey("TransactionId")]
    public Transaction Transaction { get; private set; } // Nawigacja do transakcji
    public Guid AccountId { get; private set; }     // Konto
    public Account? Account { get; private set; }   // Nawigacja do konta

    // DANE FINANSOWE
    public decimal Amount { get; private set; }
    public DebitCredit Side { get; private set; }   // Winien czy Ma?

    public DateTime CreatedAt { get; private set; }

    // Konstruktor dla EF Core
    private JournalEntry() { }

    // Twój konstruktor
    public JournalEntry(Guid accountId, decimal amount, DebitCredit side)
    {
        if (amount <= 0) throw new NegativeAmountException(amount);

        Id = Guid.NewGuid();
        AccountId = accountId;
        Amount = amount;
        Side = side;
        CreatedAt = DateTime.UtcNow;
    }

    // Metoda pomocnicza: przypisanie do transakcji
    // (Zadziała tylko raz, gdy dodajemy wpis do transakcji)
    internal void AssignToTransaction(Guid transactionId)
    {
        TransactionId = transactionId;
    }
}