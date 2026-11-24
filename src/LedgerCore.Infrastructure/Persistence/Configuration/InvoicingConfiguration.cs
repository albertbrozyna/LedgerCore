using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class ContractorConfiguration : IEntityTypeConfiguration<Contractor>
    {
        public void Configure(EntityTypeBuilder<Contractor> b)
        {
            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.Nip).HasMaxLength(10);
            b.Property(c => c.Address).HasMaxLength(300);
            b.Property(c => c.Email).HasMaxLength(100);
            b.HasIndex(c => c.Nip);
        }
    }

    internal class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> b)
        {
            b.Property(i => i.Number).HasMaxLength(50).IsRequired();
            b.Property(i => i.Net).HasPrecision(18, 2);
            b.Property(i => i.Vat).HasPrecision(18, 2);
            b.Property(i => i.Gross).HasPrecision(18, 2);
            b.Property(i => i.PaidAmount).HasPrecision(18, 2);
            b.Property(i => i.Notes).HasMaxLength(500);
            b.Property(i => i.RyczaltRate).HasPrecision(5, 2);
            b.Ignore(i => i.Remaining);
            b.Ignore(i => i.IsCorrection);
            b.Property(i => i.CorrectedAmount).HasPrecision(18, 2);
            b.HasIndex(i => i.CorrectedInvoiceId);
            b.HasOne(i => i.Contractor).WithMany().HasForeignKey(i => i.ContractorId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasMany(i => i.Payments).WithOne().HasForeignKey(p => p.InvoiceId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Metadata.FindNavigation(nameof(Invoice.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            b.Metadata.FindNavigation(nameof(Invoice.Payments))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(i => new { i.CompanyId, i.Type, i.Number, i.ContractorId }).IsUnique();
            b.HasIndex(i => i.IssueDate);
        }
    }

    internal class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
    {
        public void Configure(EntityTypeBuilder<InvoiceLine> b)
        {
            b.Property(l => l.Description).HasMaxLength(300).IsRequired();
            b.Property(l => l.Net).HasPrecision(18, 2);
            b.Property(l => l.Vat).HasPrecision(18, 2);
            b.Ignore(l => l.Gross);
        }
    }

    internal class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> b) => b.Property(p => p.Amount).HasPrecision(18, 2);
    }

    internal class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
    {
        public void Configure(EntityTypeBuilder<AuditEntry> b)
        {
            b.Property(a => a.Action).HasMaxLength(60).IsRequired();
            b.Property(a => a.Details).HasMaxLength(1000);
            b.HasIndex(a => a.At);
        }
    }

    internal class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> b)
        {
            b.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            b.Property(a => a.ContentType).HasMaxLength(100);
            b.HasIndex(a => a.InvoiceId);
        }
    }

    internal class TransactionTemplateConfiguration : IEntityTypeConfiguration<TransactionTemplate>
    {
        public void Configure(EntityTypeBuilder<TransactionTemplate> b)
        {
            b.Property(t => t.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(t => new { t.CompanyId, t.Name }).IsUnique();
            b.Property(t => t.Description).HasMaxLength(500);
        }
    }

    internal class PeriodLockConfiguration : IEntityTypeConfiguration<PeriodLock>
    {
        public void Configure(EntityTypeBuilder<PeriodLock> b) => b.HasKey(p => p.Id);
    }
}
