using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.HasMany(t => t.Entries)
                  .WithOne(j => j.Transaction)
                  .HasForeignKey(j => j.TransactionId)
                  .IsRequired();

            // Lista Entries jest tylko do odczytu - EF zapisuje przez prywatne pole _entries
            builder.Metadata.FindNavigation(nameof(Transaction.Entries))!
                  .SetPropertyAccessMode(PropertyAccessMode.Field);

            builder.Property(t => t.Number).HasMaxLength(30).IsRequired();
            builder.HasIndex(t => new { t.CompanyId, t.Number }).IsUnique();
            builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
            builder.HasIndex(t => t.Date);
            builder.HasIndex(t => new { t.CompanyId, t.Date });
            builder.HasIndex(t => t.ReversesTransactionId);
        }
    }
}
