using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Common.Ledger;

/// <summary>Tryb ewidencji bieżącej firmy: pełna księgowość księguje na konta, KPiR i ryczałt – tylko ewidencja nad fakturami.</summary>
public static class CompanyMode
{
    public static async Task<AccountingMode> Get(IAppDbContext db, CancellationToken ct) =>
        await db.Companies.AsNoTracking().Where(c => c.Id == db.CurrentCompanyId).Select(c => c.AccountingMode).FirstOrDefaultAsync(ct);

    /// <summary>Operacje na kontach (księgowania, storno, zamknięcie roku) są dostępne tylko w pełnej księgowości.</summary>
    public static async Task RequireFullBooks(IAppDbContext db, CancellationToken ct)
    {
        var mode = await Get(db, ct);
        if (mode != AccountingMode.FullBooks)
            throw new DomainValidationException(
                $"Ta firma prowadzi {(mode == AccountingMode.Kpir ? "KPiR" : "ewidencję ryczałtu")} – księgowania na kontach są dostępne tylko w pełnej księgowości");
    }
}
