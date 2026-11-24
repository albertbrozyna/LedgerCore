using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Registers;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Invoicing;

/// <summary>Kody kont wykorzystywane przy automatycznym księgowaniu faktur (z domyślnego planu kont).</summary>
public static class SystemAccounts
{
    public const string Receivables = "201";
    public const string Payables = "202";
    public const string Vat = "220";
    public const string DefaultRevenue = "700";
    public const string DefaultExpense = "402";

    public static async Task<Account> ByCode(IAppDbContext db, string code, CancellationToken ct) =>
        await db.Accounts.FirstOrDefaultAsync(a => a.Code == code, ct)
        ?? throw new DomainValidationException($"Brak konta {code} w planie kont – dodaj je, aby księgować faktury automatycznie");
}

public static class CreateInvoice
{
    public record LineDto(string Description, decimal Net, VatRate VatRate);

    public record Command(InvoiceType Type, string? Number, Guid ContractorId, DateTime IssueDate, DateTime? SaleDate,
        DateTime DueDate, Guid? ResultAccountId, string? Notes, List<LineDto> Lines,
        KpirKind? Kind = null, decimal? RyczaltRate = null) : IRequest<Result>;

    public record Result(Guid InvoiceId, string Number, Guid TransactionId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.ContractorId).NotEmpty().WithMessage("Wybierz kontrahenta");
            RuleFor(x => x.IssueDate).Must(d => d.Year is >= 2000 and <= 2100).WithMessage("Nieprawidłowa data wystawienia");
            RuleFor(x => x.Number).MaximumLength(50);
            RuleFor(x => x.Notes).MaximumLength(500);
            RuleFor(x => x.Lines).NotNull().Must(l => l != null && l.Count > 0).WithMessage("Faktura wymaga co najmniej jednej pozycji");
            RuleForEach(x => x.Lines).ChildRules(l =>
            {
                l.RuleFor(x => x.Description).NotEmpty().WithMessage("Opis pozycji jest wymagany").MaximumLength(300);
                l.RuleFor(x => x.Net).GreaterThan(0).WithMessage("Kwota netto musi być większa od zera");
                l.RuleFor(x => x.VatRate).IsInEnum();
            });
        }
    }

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            var contractor = await db.Contractors.FirstOrDefaultAsync(c => c.Id == r.ContractorId, ct)
                             ?? throw new DomainValidationException("Nie znaleziono kontrahenta");
            if (!contractor.IsActive) throw new DomainValidationException("Kontrahent jest nieaktywny");
            await PeriodGuard.EnsureOpen(db, r.IssueDate, ct);
            var mode = await CompanyMode.Get(db, ct);
            if (mode != AccountingMode.FullBooks) return await RegisterOnly(r, mode, ct);

            var sales = r.Type == InvoiceType.Sales;
            Account result;
            if (r.ResultAccountId.HasValue)
            {
                result = await db.Accounts.FirstOrDefaultAsync(a => a.Id == r.ResultAccountId, ct)
                         ?? throw new DomainValidationException("Nie znaleziono konta przychodu/kosztu");
            }
            else result = await SystemAccounts.ByCode(db, sales ? SystemAccounts.DefaultRevenue : SystemAccounts.DefaultExpense, ct);
            if (!result.IsActive) throw new DomainValidationException($"Konto {result.Code} jest nieaktywne");
            if (sales && result.Type != AccountType.Income) throw new DomainValidationException("Faktura sprzedaży musi być księgowana na konto przychodów");
            if (!sales && result.Type is not (AccountType.Expense or AccountType.Asset))
                throw new DomainValidationException("Faktura zakupu musi być księgowana na konto kosztów (lub aktywów)");

            string number;
            if (sales) number = await NextSalesNumber(db, r.IssueDate, ct);
            else
            {
                number = r.Number?.Trim() ?? "";
                if (number.Length == 0) throw new DomainValidationException("Numer faktury zakupu jest wymagany");
                if (await db.Invoices.AnyAsync(i => i.Type == InvoiceType.Purchase && i.ContractorId == r.ContractorId && i.Number == number, ct))
                    throw new DomainValidationException($"Faktura {number} tego kontrahenta jest już zarejestrowana");
            }

            var invoice = new Invoice(r.Type, number, r.ContractorId, r.IssueDate, r.SaleDate ?? r.IssueDate, r.DueDate,
                result.Id, r.Notes, me.User?.Id, r.Lines.Select(l => (l.Description, l.Net, l.VatRate)));

            // Automatyczne księgowanie
            var ctrl = await SystemAccounts.ByCode(db, sales ? SystemAccounts.Receivables : SystemAccounts.Payables, ct);
            var vatAcc = invoice.Vat > 0 ? await SystemAccounts.ByCode(db, SystemAccounts.Vat, ct) : null;
            var txNumber = await TransactionNumbers.Next(db, invoice.IssueDate, ct);
            var label = sales ? "Faktura sprzedaży" : "Faktura zakupu";
            var tx = new Transaction($"{label} {invoice.Number} – {contractor.Name}", invoice.IssueDate, txNumber, me.User?.Id);
            if (sales)
            {
                tx.AddEntry(ctrl.Id, invoice.Gross, DebitCredit.Debit);
                tx.AddEntry(result.Id, invoice.Net, DebitCredit.Credit);
                if (vatAcc != null) tx.AddEntry(vatAcc.Id, invoice.Vat, DebitCredit.Credit);
            }
            else
            {
                tx.AddEntry(result.Id, invoice.Net, DebitCredit.Debit);
                if (vatAcc != null) tx.AddEntry(vatAcc.Id, invoice.Vat, DebitCredit.Debit);
                tx.AddEntry(ctrl.Id, invoice.Gross, DebitCredit.Credit);
            }
            tx.Validate();
            invoice.LinkTransaction(tx.Id);

            db.Transactions.Add(tx);
            db.Invoices.Add(invoice);
            await audit.Add("Faktura", $"Zarejestrowano fakturę {invoice.Number} ({(sales ? "sprzedaży" : "zakupu")}), brutto {invoice.Gross:0.00}, kontrahent {contractor.Name}", ct);
            await db.SaveChangesAsync(ct);
            return new Result(invoice.Id, invoice.Number, tx.Id);
        }

        /// <summary>KPiR / ryczałt: faktura trafia do ewidencji bez księgowania na konta (ewidencja jest widokiem nad fakturami).</summary>
        async Task<Result> RegisterOnly(Command r, AccountingMode mode, CancellationToken ct)
        {
            var sales = r.Type == InvoiceType.Sales;
            var (kind, rate) = RegisterRules.Classify(mode, r.Type, r.Kind, r.RyczaltRate);
            string number;
            if (sales) number = await NextSalesNumber(db, r.IssueDate, ct);
            else
            {
                number = r.Number?.Trim() ?? "";
                if (number.Length == 0) throw new DomainValidationException("Numer faktury zakupu jest wymagany");
                if (await db.Invoices.AnyAsync(i => i.Type == InvoiceType.Purchase && i.ContractorId == r.ContractorId && i.Number == number, ct))
                    throw new DomainValidationException($"Faktura {number} tego kontrahenta jest już zarejestrowana");
            }
            var invoice = new Invoice(r.Type, number, r.ContractorId, r.IssueDate, r.SaleDate ?? r.IssueDate, r.DueDate,
                Guid.Empty, r.Notes, me.User?.Id, r.Lines.Select(l => (l.Description, l.Net, l.VatRate)));
            invoice.SetTaxClassification(kind, rate);
            db.Invoices.Add(invoice);
            await audit.Add("Faktura", $"Zarejestrowano fakturę {invoice.Number} ({(sales ? "sprzedaży" : "zakupu")}) w ewidencji {(mode == AccountingMode.Kpir ? "KPiR" : "ryczałtu")}, netto {invoice.Net:0.00}", ct);
            await db.SaveChangesAsync(ct);
            return new Result(invoice.Id, invoice.Number, Guid.Empty);
        }

        static async Task<string> NextSalesNumber(IAppDbContext db, DateTime date, CancellationToken ct)
        {
            var prefix = $"FV/{date.Year}/";
            var nums = await db.Invoices.AsNoTracking().Where(i => i.Type == InvoiceType.Sales && i.Number.StartsWith(prefix))
                .Select(i => i.Number).ToListAsync(ct);
            var max = nums.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
            return $"{prefix}{max + 1:0000}";
        }
    }
}

public static class RecordPayment
{
    public record Command(Guid InvoiceId, DateTime Date, decimal Amount, Guid AccountId) : IRequest<Unit>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Kwota płatności musi być większa od zera");
        }
    }

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command r, CancellationToken ct)
        {
            var inv = await db.Invoices.Include(i => i.Contractor).Include(i => i.Payments)
                          .FirstOrDefaultAsync(i => i.Id == r.InvoiceId, ct)
                      ?? throw new DomainValidationException("Nie znaleziono faktury");
            await PeriodGuard.EnsureOpen(db, r.Date, ct);

            if (await CompanyMode.Get(db, ct) != AccountingMode.FullBooks)
            {
                // KPiR / ryczałt: płatność jest tylko adnotacją o rozliczeniu faktury (bez konta kasy/banku i bez księgowania)
                db.Payments.Add(inv.AddPayment(r.Date, r.Amount, Guid.Empty, Guid.Empty));
                await audit.Add("Płatność", $"Zarejestrowano płatność {r.Amount:0.00} do faktury {inv.Number}", ct);
                await db.SaveChangesAsync(ct);
                return Unit.Value;
            }
            if (r.AccountId == Guid.Empty) throw new DomainValidationException("Wybierz konto kasy/banku");

            var cash = await db.Accounts.FirstOrDefaultAsync(a => a.Id == r.AccountId, ct)
                       ?? throw new DomainValidationException("Nie znaleziono konta kasy/banku");
            if (cash.Type != AccountType.Asset || !cash.IsActive)
                throw new DomainValidationException("Płatność można zaksięgować tylko na aktywnym koncie aktywów (kasa/bank)");

            var sales = inv.Type == InvoiceType.Sales;
            var ctrl = await SystemAccounts.ByCode(db, sales ? SystemAccounts.Receivables : SystemAccounts.Payables, ct);
            var txNumber = await TransactionNumbers.Next(db, r.Date, ct);
            var tx = new Transaction($"{(sales ? "Wpłata" : "Zapłata")} do faktury {inv.Number} – {inv.Contractor!.Name}", r.Date, txNumber, me.User?.Id);
            if (sales) { tx.AddEntry(cash.Id, r.Amount, DebitCredit.Debit); tx.AddEntry(ctrl.Id, r.Amount, DebitCredit.Credit); }
            else { tx.AddEntry(ctrl.Id, r.Amount, DebitCredit.Debit); tx.AddEntry(cash.Id, r.Amount, DebitCredit.Credit); }
            tx.Validate();

            var payment = inv.AddPayment(r.Date, r.Amount, cash.Id, tx.Id);
            db.Transactions.Add(tx);
            db.Payments.Add(payment);
            await audit.Add("Płatność", $"Zarejestrowano płatność {r.Amount:0.00} do faktury {inv.Number}", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

public static class CancelInvoice
{
    public record Command(Guid InvoiceId) : IRequest<Unit>;

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command r, CancellationToken ct)
        {
            var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == r.InvoiceId, ct)
                      ?? throw new DomainValidationException("Nie znaleziono faktury");
            if (!inv.IsCorrection && await db.Invoices.AnyAsync(i => i.CorrectedInvoiceId == inv.Id && !i.IsCanceled, ct))
                throw new DomainValidationException("Faktura ma korekty – anuluj najpierw korekty");
            if (inv.IsCorrection)
            {
                var corrected = await db.Invoices.FirstAsync(i => i.Id == inv.CorrectedInvoiceId, ct);
                corrected.RevertCorrection(inv.Gross); // odwraca wpływ korekty na kwotę do zapłaty
            }
            inv.Cancel(); // rzuca, jeśli są płatności lub już anulowana

            if (inv.TransactionId == null)
            {
                // KPiR / ryczałt: faktura nie ma księgowania – anulowana znika z ewidencji
                await audit.Add("Faktura", $"Anulowano fakturę {inv.Number}", ct);
                await db.SaveChangesAsync(ct);
                return Unit.Value;
            }
            var today = DateTime.Today;
            await PeriodGuard.EnsureOpen(db, today, ct);
            var original = await db.Transactions.Include(t => t.Entries).FirstAsync(t => t.Id == inv.TransactionId, ct);
            var reversal = original.CreateReversal(await TransactionNumbers.Next(db, today, ct), today, me.User?.Id);
            db.Transactions.Add(reversal);
            await audit.Add("Faktura", $"Anulowano fakturę {inv.Number} (storno {reversal.Number})", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
