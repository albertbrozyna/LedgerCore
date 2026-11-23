using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.Property(e => e.Amount).HasPrecision(18, 2);
            builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(e => e.AccountId);
        }
    }
}
