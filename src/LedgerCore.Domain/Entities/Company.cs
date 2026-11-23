namespace LedgerCore.Domain.Entities;

/// <summary>Znacznik rekordów należących do konkretnej firmy (klienta biura). Dane różnych firm są od siebie odizolowane.</summary>
public interface ICompanyOwned
{
    Guid CompanyId { get; }
}

/// <summary>Firma (klient biura). Jedna instalacja może obsługiwać wiele firm; dane firmy są też danymi wystawcy na fakturach.</summary>
public class Company
{
    /// <summary>Identyfikator firmy tworzonej przy migracji istniejących danych z wersji jednofirmowej.</summary>
    public static readonly Guid LegacyId = new("11111111-1111-1111-1111-111111111111");

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Nip { get; private set; }
    public string? Address { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? BankAccount { get; private set; }
    public string? InvoiceFooter { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private Company() { }

    public Company(string name)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Update(name, null, null, null, null, null, null);
    }

    public void Update(string name, string? nip, string? address, string? email, string? phone, string? bankAccount, string? footer)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nazwa firmy jest wymagana");
        string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        Name = name.Trim();
        Nip = Contractor.NormalizeNip(nip);
        Address = N(address); Email = N(email); Phone = N(phone); BankAccount = N(bankAccount); InvoiceFooter = N(footer);
    }

    public void SetActive(bool active) => IsActive = active;
}

/// <summary>Dostęp użytkownika do firmy. Administrator ma dostęp do wszystkich firm bez przypisań.</summary>
public class UserCompany
{
    public Guid UserId { get; private set; }
    public Guid CompanyId { get; private set; }

    private UserCompany() { }
    public UserCompany(Guid userId, Guid companyId) { UserId = userId; CompanyId = companyId; }
}
