using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class InterestRateConfiguration : IEntityTypeConfiguration<InterestRate>
    {
        public void Configure(EntityTypeBuilder<InterestRate> b)
        {
            b.Property(x => x.AnnualPercent).HasPrecision(5, 2);
            b.Property(x => x.Note).HasMaxLength(200);
            b.HasIndex(x => x.ValidFrom).IsUnique();
        }
    }

    internal class MailSettingsConfiguration : IEntityTypeConfiguration<MailSettings>
    {
        public void Configure(EntityTypeBuilder<MailSettings> b)
        {
            b.Property(x => x.Host).HasMaxLength(200);
            b.Property(x => x.UserName).HasMaxLength(200);
            b.Property(x => x.ProtectedPassword).HasMaxLength(2000);
            b.Property(x => x.FromAddress).HasMaxLength(200);
            b.Property(x => x.FromName).HasMaxLength(200);
            b.Property(x => x.ReminderSubject).HasMaxLength(200);
            b.Property(x => x.ReminderBody).HasMaxLength(4000);
            b.Property(x => x.InterestSubject).HasMaxLength(200);
            b.Property(x => x.InterestBody).HasMaxLength(4000);
            b.HasIndex(x => x.CompanyId).IsUnique();
        }
    }

    internal class CollectionNoticeConfiguration : IEntityTypeConfiguration<CollectionNotice>
    {
        public void Configure(EntityTypeBuilder<CollectionNotice> b)
        {
            b.Property(x => x.Principal).HasPrecision(18, 2);
            b.Property(x => x.Interest).HasPrecision(18, 2);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.SentTo).HasMaxLength(200);
            b.Property(x => x.Subject).HasMaxLength(300);
            b.Property(x => x.Body).HasMaxLength(8000);
            b.Property(x => x.Error).HasMaxLength(500);
            b.HasIndex(x => new { x.CompanyId, x.ContractorId });
            b.HasMany(x => x.Invoices).WithOne().HasForeignKey(i => i.NoticeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Metadata.FindNavigation(nameof(CollectionNotice.Invoices))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal class CollectionNoticeInvoiceConfiguration : IEntityTypeConfiguration<CollectionNoticeInvoice>
    {
        public void Configure(EntityTypeBuilder<CollectionNoticeInvoice> b) => b.HasIndex(x => new { x.CompanyId, x.InvoiceId });
    }
}
