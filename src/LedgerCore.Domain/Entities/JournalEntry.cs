using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using System.ComponentModel.DataAnnotations.Schema;

namespace LedgerCore.Domain.Entities;

public class JournalEntry : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }

    // RELACJE
    public Guid TransactionId { get; private set; } // Rodzic (Transakcja)
    [ForeignKey("TransactionId")]
    public Transaction Transaction { get; private set; } // Nawigacja do transakcji
    public Guid AccountId { get; private set; }     // Konto
    public Account? Account { get; private set; }   // Nawigacja do konta

    // DANE FINANSOWE
    public decimal Amount { get; private set; }
    /// <summary>Kwota w groszach (całkowita) – pozwala sumować w SQL bez utraty dokładności (SQLite nie sumuje decimal).</summary>
    public long AmountCents { get; private set; }
    /// <summary>Data księgowania (kopia z nagłówka) – pozwala filtrować i sumować zapisy bez łączenia tabel.</summary>
    public DateTime Date { get; private set; }
    /// <summary>Zapis należy do księgowania zamykającego rok (ZR/…) – pomijany w raportach wyników.</summary>
    public bool IsClosing { get; private set; }
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
        AmountCents = decimal.ToInt64(decimal.Round(amount * 100m, 0));
        Side = side;
        CreatedAt = DateTime.UtcNow;
    }

    // Metoda pomocnicza: przypisanie do transakcji
    // (Zadziała tylko raz, gdy dodajemy wpis do transakcji)
    internal void AssignToTransaction(Guid transactionId, DateTime date, bool isClosing)
    {
        TransactionId = transactionId;
        Date = date.Date;
        IsClosing = isClosing;
    }
}