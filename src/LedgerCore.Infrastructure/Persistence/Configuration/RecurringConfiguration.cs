using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class RecurringInvoiceConfiguration : IEntityTypeConfiguration<RecurringInvoice>
    {
        public void Configure(EntityTypeBuilder<RecurringInvoice> b)
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            b.Property(x => x.RyczaltRate).HasPrecision(5, 2);
            b.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.RecurringInvoiceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Metadata.FindNavigation(nameof(RecurringInvoice.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.MonthsPerPeriod);
            b.HasIndex(x => new { x.CompanyId, x.IsActive, x.NextDate });
        }
    }

    internal class RecurringInvoiceLineConfiguration : IEntityTypeConfiguration<RecurringInvoiceLine>
    {
        public void Configure(EntityTypeBuilder<RecurringInvoiceLine> b)
        {
            b.Property(x => x.Description).HasMaxLength(300).IsRequired();
            b.Property(x => x.Net).HasPrecision(18, 2);
        }
    }
}
