namespace LedgerCore.Domain.Entities;

public class Contractor : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Nip { get; private set; }
    public string? Address { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private Contractor() { }

    public Contractor(string name, string? nip, string? address, string? email)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Update(name, nip, address, email, true);
    }

    public void Update(string name, string? nip, string? address, string? email, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nazwa kontrahenta jest wymagana");
        Name = name.Trim();
        Nip = NormalizeNip(nip);
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        IsActive = isActive;
    }

    public static string? NormalizeNip(string? nip)
    {
        if (string.IsNullOrWhiteSpace(nip)) return null;
        return new string(nip.Where(char.IsDigit).ToArray());
    }

    /// <summary>Walidacja sumy kontrolnej polskiego NIP (10 cyfr, wagi 6,5,7,2,3,4,5,6,7).</summary>
    public static bool IsValidNip(string? nip)
    {
        var n = NormalizeNip(nip);
        if (n is not { Length: 10 }) return false;
        int[] w = [6, 5, 7, 2, 3, 4, 5, 6, 7];
        var sum = 0;
        for (var i = 0; i < 9; i++) sum += (n[i] - '0') * w[i];
        var check = sum % 11;
        return check != 10 && check == n[9] - '0';
    }
}
