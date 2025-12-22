using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
         builder
        .Property(e => e.Amount)
        .HasPrecision(18, 2); // 18 cyfr, 2 po przecinku
        }
    }
}
