using CSharpFunctionalExtensions;
using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure.Persistence.Configuration
{
    internal class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            // Mówimy bazie: Jedna Transakcja ma wiele wpisów (Entries)
            builder.HasMany(t => t.Entries)
                  .WithOne(j => j.Transaction)
                  .HasForeignKey(j => j.TransactionId)
                  .IsRequired();

            // Mówimy bazie: "Hej, lista 'Entries' w klasie Transaction jest tylko do odczytu.
            // Żeby ją zapisać, musisz dobrać się do prywatnego pola '_entries'."
            builder.Metadata.FindNavigation(nameof(Transaction.Entries))!
                  .SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
