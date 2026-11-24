using System.Linq.Expressions;
using System.Reflection;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Infrastructure.Persistence.Context;

public class LedgerDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IAppDbContext
{
    private readonly ICompanyContext _company;

    public LedgerDbContext(DbContextOptions<LedgerDbContext> options, ICompanyContext company) : base(options)
    {
        _company = company;
    }

    /// <summary>Firma bieżącego żądania – używana w filtrach zapytań (odczytywana przy każdym zapytaniu).</summary>
    public Guid CurrentCompanyId => _company.CompanyId;

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<UserVerificationToken> UserVerificationTokens { get; set; }
    public DbSet<Contractor> Contractors { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceLine> InvoiceLines { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<AuditEntry> AuditEntries { get; set; }
    public DbSet<PeriodLock> PeriodLocks { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<UserCompany> UserCompanies { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<TransactionTemplate> TransactionTemplates { get; set; }
    public DbSet<InterestRate> InterestRates { get; set; }
    public DbSet<MailSettings> MailSettings { get; set; }
    public DbSet<CollectionNotice> CollectionNotices { get; set; }
    public DbSet<CollectionNoticeInvoice> CollectionNoticeInvoices { get; set; }
    public DbSet<RecurringInvoice> RecurringInvoices { get; set; }
    public DbSet<RecurringInvoiceLine> RecurringInvoiceLines { get; set; }
    public DbSet<BankStatement> BankStatements { get; set; }
    public DbSet<BankTransaction> BankTransactions { get; set; }
    public DbSet<BankRule> BankRules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                entity.SetTableName(tableName.ToLowerInvariant());
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);

        modelBuilder.Entity<UserCompany>().HasKey(u => new { u.UserId, u.CompanyId });
        modelBuilder.Entity<Company>().Property(c => c.Name).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<Company>().Property(c => c.Nip).HasMaxLength(10);
        modelBuilder.Entity<Company>().Property(c => c.Address).HasMaxLength(300);
        modelBuilder.Entity<Company>().Property(c => c.Email).HasMaxLength(100);
        modelBuilder.Entity<Company>().Property(c => c.Phone).HasMaxLength(40);
        modelBuilder.Entity<Company>().Property(c => c.BankAccount).HasMaxLength(40);
        modelBuilder.Entity<Company>().Property(c => c.InvoiceFooter).HasMaxLength(500);
        modelBuilder.Entity<Company>().Property(c => c.SalesNumberPattern).HasMaxLength(60).HasDefaultValue(NumberPattern.Default);

        // Izolacja danych firm: każde zapytanie o dane księgowe jest automatycznie ograniczone do bieżącej firmy.
        foreach (var type in modelBuilder.Model.GetEntityTypes().Select(t => t.ClrType).Where(t => typeof(ICompanyOwned).IsAssignableFrom(t)).ToList())
        {
            typeof(LedgerDbContext).GetMethod(nameof(ApplyCompanyFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(type).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyCompanyFilter<T>(ModelBuilder modelBuilder) where T : class, ICompanyOwned
    {
        Expression<Func<T, bool>> filter = e => e.CompanyId == CurrentCompanyId;
        modelBuilder.Entity<T>().HasQueryFilter(filter);
        modelBuilder.Entity<T>().HasIndex(nameof(ICompanyOwned.CompanyId));
    }

    /// <summary>Nowe rekordy zawsze trafiają do firmy bieżącego żądania – nie da się zapisać danych „obok” firmy.</summary>
    private void StampCompany()
    {
        foreach (var entry in ChangeTracker.Entries<ICompanyOwned>().Where(e => e.State == EntityState.Added))
        {
            if (_company.CompanyId == Guid.Empty)
                throw new InvalidOperationException("Nie wybrano firmy – nie można zapisać danych księgowych.");
            entry.Property(nameof(ICompanyOwned.CompanyId)).CurrentValue = _company.CompanyId;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampCompany();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampCompany();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
