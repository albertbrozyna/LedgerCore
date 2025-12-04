using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Infrastructure.Persistence;

// 1. Dziedziczymy po DbContext (klasie od Microsoftu)
public class LedgerDbContext : DbContext
{
    // 2. Konstruktor - przyjmuje opcje (np. "połącz z Postgres") i przekazuje je wyżej
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options)
    {
    }

    // 3. Definicja Tabel
    // "DbSet" oznacza: "To będzie tabela w bazie danych"
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }

    // 4. Konfiguracja "pod maską"
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Konfiguracja dla Transakcji
        modelBuilder.Entity<Transaction>(entity =>
        {
            // Mówimy bazie: Jedna Transakcja ma wiele wpisów (Entries)
            entity.HasMany(t => t.Entries)
                  .WithOne()
                  .HasForeignKey(e => e.TransactionId);

            // 🔥 BARDZO WAŻNE:
            // Mówimy bazie: "Hej, lista 'Entries' w klasie Transaction jest tylko do odczytu.
            // Żeby ją zapisać, musisz dobrać się do prywatnego pola '_entries'."
            entity.Metadata.FindNavigation(nameof(Transaction.Entries))!
                  .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        // Konfiguracja dla kwot pieniężnych (wymagane w Postgres dla typu decimal)
        modelBuilder.Entity<JournalEntry>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2); // 18 cyfr, 2 po przecinku

        base.OnModelCreating(modelBuilder);
    }
}