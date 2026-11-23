using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
            builder.Property(a => a.Code).HasMaxLength(30).IsRequired();
            builder.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();
        }
    }
}
