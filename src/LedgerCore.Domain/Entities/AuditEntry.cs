namespace LedgerCore.Domain.Entities;

public class AuditEntry : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public DateTime At { get; private set; }
    public Guid? UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;

    private AuditEntry() { }

    public AuditEntry(Guid? userId, string userName, string action, string details)
    {
        Id = Guid.NewGuid(); At = DateTime.UtcNow; UserId = userId; UserName = userName; Action = action; Details = details;
    }
}

/// <summary>Blokada okresu: nie można księgować z datą nie późniejszą niż LockedThrough. Jeden wiersz na firmę.</summary>
public class PeriodLock : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public int Id { get; private set; }
    public DateTime? LockedThrough { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Set(DateTime? lockedThrough) { LockedThrough = lockedThrough?.Date; UpdatedAt = DateTime.UtcNow; }
}
