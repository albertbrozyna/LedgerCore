using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LedgerCore.Infrastructure.Persistence.Context;

// Używane TYLKO przez narzędzie `dotnet ef` (tworzenie migracji).
public class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LedgerDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db");
        return new LedgerDbContext(optionsBuilder.Options, new LedgerCore.Application.Common.Interfaces.CompanyContext());
    }
}
