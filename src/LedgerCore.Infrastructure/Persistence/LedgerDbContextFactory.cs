using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LedgerCore.Infrastructure.Persistence;

// To jest instrukcja TYLKO dla terminala (dotnet ef).
// Aplikacja tego nie używa, tylko migrator.
public class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        // 1. Tworzymy budowniczego opcji
        var optionsBuilder = new DbContextOptionsBuilder<LedgerDbContext>();

        // 2. Konfigurujemy połączenie "na sztywno" TYLKO do migracji.
        // Dzięki temu nie musimy się martwić o appsettings.json czy Program.cs
        // Upewnij się, że dane (hasło/user) są takie jak w Dockerze!
        var connectionString = "Host=localhost;Database=ledger_db;Username=admin;Password=sekretnehaslo123";

        optionsBuilder.UseNpgsql(connectionString);

        // 3. Zwracamy gotowy kontekst
        return new LedgerDbContext(optionsBuilder.Options);
    }
}