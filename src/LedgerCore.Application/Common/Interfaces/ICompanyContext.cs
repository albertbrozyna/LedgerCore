namespace LedgerCore.Application.Common.Interfaces;

/// <summary>
/// Firma, w której aktualnie pracuje użytkownik (ustalana z nagłówka X-Company-Id po sprawdzeniu uprawnień).
/// Wszystkie dane księgowe są automatycznie filtrowane do tej firmy.
/// </summary>
public interface ICompanyContext
{
    Guid CompanyId { get; set; }
}
