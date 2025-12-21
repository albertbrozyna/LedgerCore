using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using LedgerCore.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
namespace LedgerCore.Infrastructure.Persistence;

// 1. Dziedziczymy po DbContext (klasie od Microsoftu)
public class LedgerDbContext : IdentityDbContext<User,IdentityRole<Guid>,Guid>,IAppDbContext
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

    public DbSet<User> Users { get; set; }

    // 4. Konfiguracja "pod maską"
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                entity.SetTableName(tableName.ToLowerInvariant());
            }
        }

        // Konfiguracja dla Transakcji
        modelBuilder.Entity<Transaction>(entity =>
        {
            // Mówimy bazie: Jedna Transakcja ma wiele wpisów (Entries)
            entity.HasMany(t => t.Entries)
                  .WithOne(j => j.Transaction)
                  .HasForeignKey(j => j.TransactionId)
                  .IsRequired();

            // Mówimy bazie: "Hej, lista 'Entries' w klasie Transaction jest tylko do odczytu.
            // Żeby ją zapisać, musisz dobrać się do prywatnego pola '_entries'."
            entity.Metadata.FindNavigation(nameof(Transaction.Entries))!
                  .SetPropertyAccessMode(PropertyAccessMode.Field);

            

        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Zamienia np. AspNetUsers na aspnetusers
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                entity.SetTableName(tableName.ToLowerInvariant());
            }
        }

        // Konfiguracja dla kwot pieniężnych (wymagane w Postgres dla typu decimal)
        modelBuilder.Entity<JournalEntry>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2); // 18 cyfr, 2 po przecinku

       

    }
}