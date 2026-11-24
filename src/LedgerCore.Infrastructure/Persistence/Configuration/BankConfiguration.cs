using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
    {
        public void Configure(EntityTypeBuilder<BankStatement> b)
        {
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.Format).HasMaxLength(20).IsRequired();
            b.Property(x => x.ImportedBy).HasMaxLength(100);
            b.Property(x => x.OpeningBalance).HasPrecision(18, 2);
            b.Property(x => x.ClosingBalance).HasPrecision(18, 2);
            b.HasIndex(x => new { x.CompanyId, x.AccountId, x.ImportedAt });
        }
    }

    internal class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
    {
        public void Configure(EntityTypeBuilder<BankTransaction> b)
        {
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Counterparty).HasMaxLength(200).IsRequired();
            b.Property(x => x.CounterpartyAccount).HasMaxLength(40).IsRequired();
            b.Property(x => x.Hash).HasMaxLength(64).IsRequired();
            // ten sam skrót na tym samym rachunku nie może wystąpić dwa razy – ochrona przed podwójnym importem
            b.HasIndex(x => new { x.CompanyId, x.AccountId, x.Hash }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.AccountId, x.Status, x.Date });
            b.HasIndex(x => x.StatementId);
        }
    }

    internal class BankRuleConfiguration : IEntityTypeConfiguration<BankRule>
    {
        public void Configure(EntityTypeBuilder<BankRule> b)
        {
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.MatchText).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(300);
        }
    }
}
