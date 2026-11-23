using LedgerCore.Domain.Enums;

namespace LedgerCore.Domain.Entities;

public class Account : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty; // np. "100-01"
    public AccountType Type { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Account(string name, string code, AccountType type)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nazwa konta jest wymagana");
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Numer konta jest wymagany");

        Id = Guid.NewGuid();
        Name = name.Trim();
        Code = code.Trim();
        Type = type;
        IsActive = true;
    }

    // Potrzebny dla EF Core
    private Account() { }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nazwa konta jest wymagana");
        Name = name.Trim();
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>Konta aktywów i kosztów mają saldo po stronie Winien, pozostałe po stronie Ma.</summary>
    public bool IsDebitNormal => Type is AccountType.Asset or AccountType.Expense;
}
