using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<Account> Accounts { get; }
        DbSet<Transaction> Transactions { get; }
        DbSet<JournalEntry> JournalEntries { get; }
        DbSet<User> Users { get; }
        DbSet<RefreshToken> RefreshTokens { get; }
        DbSet<UserVerificationToken> UserVerificationTokens { get; }
        DbSet<Contractor> Contractors { get; }
        DbSet<Invoice> Invoices { get; }
        DbSet<InvoiceLine> InvoiceLines { get; }
        DbSet<Payment> Payments { get; }
        DbSet<AuditEntry> AuditEntries { get; }
        DbSet<PeriodLock> PeriodLocks { get; }
        DbSet<Company> Companies { get; }
        DbSet<UserCompany> UserCompanies { get; }
        DbSet<Attachment> Attachments { get; }
        DbSet<TransactionTemplate> TransactionTemplates { get; }
        DbSet<InterestRate> InterestRates { get; }
        DbSet<MailSettings> MailSettings { get; }
        DbSet<CollectionNotice> CollectionNotices { get; }
        DbSet<CollectionNoticeInvoice> CollectionNoticeInvoices { get; }
        DbSet<RecurringInvoice> RecurringInvoices { get; }
        DbSet<RecurringInvoiceLine> RecurringInvoiceLines { get; }

        /// <summary>Firma bieżącego żądania (z nagłówka X-Company-Id).</summary>
        Guid CurrentCompanyId { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
