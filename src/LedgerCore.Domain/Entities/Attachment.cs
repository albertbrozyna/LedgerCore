namespace LedgerCore.Domain.Entities;

/// <summary>Załącznik (skan/PDF) do faktury. Plik leży na dysku w folderze danych, w bazie są tylko metadane.</summary>
public class Attachment : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public Guid? UploadedByUserId { get; private set; }

    private Attachment() { }

    public Attachment(Guid invoiceId, string fileName, string contentType, long size, Guid? userId)
    {
        Id = Guid.NewGuid(); InvoiceId = invoiceId; FileName = fileName; ContentType = contentType;
        Size = size; UploadedAt = DateTime.UtcNow; UploadedByUserId = userId;
    }
}

/// <summary>Szablon księgowania: powtarzalna operacja (np. czynsz). Linie przechowywane jako JSON.</summary>
public class TransactionTemplate : ICompanyOwned
{
    /// <summary>Firma (klient biura), do której należy rekord. Ustawiane automatycznie przy zapisie.</summary>
    public Guid CompanyId { get; private set; }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string LinesJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; }

    private TransactionTemplate() { }

    public TransactionTemplate(string name, string description, string linesJson)
    {
        Id = Guid.NewGuid(); Name = name.Trim(); Description = description.Trim(); LinesJson = linesJson; CreatedAt = DateTime.UtcNow;
    }
}
