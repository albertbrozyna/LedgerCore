using LedgerCore.Domain.Enums;

namespace LedgerCore.Domain.Entities; 

public class Account
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; } // np. "100-01"
    public AccountType Type { get; private set; }

    public Account(string name, string code, AccountType type)
    {
        Id = Guid.NewGuid();
        Name = name;
        Code = code;
        Type = type;
    }

    // Ten prywatny konstruktor jest potrzebny dla bazy danych później
    private Account() { }
}