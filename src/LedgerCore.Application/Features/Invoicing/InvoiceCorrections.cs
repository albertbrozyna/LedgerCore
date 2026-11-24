using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Invoicing;

/// <summary>
/// Faktura korygująca. Pozycje to RÓŻNICE względem faktury korygowanej (ujemne = zmniejszenie, dodatnie = zwiększenie).
/// Korekta jest księgowana ze znakiem (zmniejszenie sprzedaży to storno przychodu, VAT należnego i należności),
/// zmienia kwotę do zapłaty na fakturze korygowanej i wchodzi do rejestru VAT w miesiącu wystawienia korekty.
/// </summary>
public static class CreateCorrection
{
    public record LineDto(string Description, decimal Net, VatRate VatRate);

    public record Command(Guid OriginalId, string? Number, DateTime IssueDate, string? Reason, List<LineDto> Lines) : IRequest<CreateInvoice.Result>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OriginalId).NotEmpty();
            RuleFor(x => x.IssueDate).Must(d => d.Year is >= 2000 and <= 2100).WithMessage("Nieprawidłowa data wystawienia");
            RuleFor(x => x.Number).MaximumLength(50);
            RuleFor(x => x.Reason).MaximumLength(450);
            RuleFor(x => x.Lines).NotNull().Must(l => l != null && l.Count > 0).WithMessage("Korekta wymaga co najmniej jednej pozycji");
            RuleForEach(x => x.Lines).ChildRules(l =>
            {
                l.RuleFor(x => x.Description).NotEmpty().WithMessage("Opis pozycji jest wymagany").MaximumLength(300);
                l.RuleFor(x => x.Net).NotEqual(0).WithMessage("Kwota różnicy netto nie może być zerowa");
                l.RuleFor(x => x.VatRate).IsInEnum();
            });
        }
    }

    /// <summary>Dodaje zapis po stronie normalnej dla dodatniej kwoty, a po przeciwnej dla ujemnej (kwoty zerowe pomija).</summary>
    internal static void AddSigned(Transaction tx, Guid accountId, decimal amount, DebitCredit normalSide)
    {
        if (amount == 0) return;
        var side = amount > 0 ? normalSide : normalSide == DebitCredit.Debit ? DebitCredit.Credit : DebitCredit.Debit;
        tx.AddEntry(accountId, Math.Abs(amount), side);
    }

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, CreateInvoice.Result>
    {
        public async Task<CreateInvoice.Result> Handle(Command r, CancellationToken ct)
        {
            var original = await db.Invoices.Include(i => i.Contractor).FirstOrDefaultAsync(i => i.Id == r.OriginalId, ct)
                           ?? throw new DomainValidationException("Nie znaleziono faktury korygowanej");
            if (original.IsCorrection) throw new DomainValidationException("Nie można korygować faktury korygującej – skoryguj fakturę pierwotną");
            if (original.IsCanceled) throw new DomainValidationException("Faktura została anulowana");
            await PeriodGuard.EnsureOpen(db, r.IssueDate, ct);

            var sales = original.Type == InvoiceType.Sales;
            string number;
            if (sales) number = await NextNumber(ct, r.IssueDate);
            else
            {
                number = r.Number?.Trim() ?? "";
                if (number.Length == 0) throw new DomainValidationException("Numer faktury korygującej dostawcy jest wymagany");
                if (await db.Invoices.AnyAsync(i => i.Type == InvoiceType.Purchase && i.ContractorId == original.ContractorId && i.Number == number, ct))
                    throw new DomainValidationException($"Dokument {number} tego kontrahenta jest już zarejestrowany");
            }

            var notes = $"Korekta faktury {original.Number}" + (string.IsNullOrWhiteSpace(r.Reason) ? "" : $". Przyczyna: {r.Reason.Trim()}");
            var correction = new Invoice(original.Type, number, original.ContractorId, r.IssueDate, r.IssueDate, r.IssueDate,
                original.ResultAccountId, notes, me.User?.Id, r.Lines.Select(l => (l.Description, l.Net, l.VatRate)), original.Id);
            if (correction.Gross == 0 && correction.Net == 0)
                throw new DomainValidationException("Korekta nie zmienia kwot faktury");

            original.ApplyCorrection(correction.Gross); // pilnuje, by nie zejść poniżej zapłaconej kwoty

            if (await CompanyMode.Get(db, ct) != AccountingMode.FullBooks)
            {
                // KPiR / ryczałt: korekta trafia do ewidencji ze znakiem (w dacie wystawienia korekty), bez księgowania na konta
                correction.SetTaxClassification(original.KpirKind, original.RyczaltRate);
                db.Invoices.Add(correction);
                await audit.Add("Korekta", $"Wystawiono korektę {correction.Number} do faktury {original.Number}, różnica brutto {correction.Gross:0.00}", ct);
                await db.SaveChangesAsync(ct);
                return new CreateInvoice.Result(correction.Id, correction.Number, Guid.Empty);
            }

            var control = await SystemAccounts.ByCode(db, sales ? SystemAccounts.Receivables : SystemAccounts.Payables, ct);
            var vatAcc = correction.Vat != 0 ? await SystemAccounts.ByCode(db, SystemAccounts.Vat, ct) : null;
            var tx = new Transaction($"Korekta {(sales ? "faktury sprzedaży" : "faktury zakupu")} {original.Number} ({correction.Number}) – {original.Contractor!.Name}",
                correction.IssueDate, await TransactionNumbers.Next(db, correction.IssueDate, ct), me.User?.Id);
            if (sales)
            {
                AddSigned(tx, control.Id, correction.Gross, DebitCredit.Debit);
                AddSigned(tx, original.ResultAccountId, correction.Net, DebitCredit.Credit);
                if (vatAcc != null) AddSigned(tx, vatAcc.Id, correction.Vat, DebitCredit.Credit);
            }
            else
            {
                AddSigned(tx, original.ResultAccountId, correction.Net, DebitCredit.Debit);
                if (vatAcc != null) AddSigned(tx, vatAcc.Id, correction.Vat, DebitCredit.Debit);
                AddSigned(tx, control.Id, correction.Gross, DebitCredit.Credit);
            }
            tx.Validate();
            correction.LinkTransaction(tx.Id);

            db.Transactions.Add(tx);
            db.Invoices.Add(correction);
            await audit.Add("Korekta", $"Wystawiono korektę {correction.Number} do faktury {original.Number}, różnica brutto {correction.Gross:0.00}", ct);
            await db.SaveChangesAsync(ct);
            return new CreateInvoice.Result(correction.Id, correction.Number, tx.Id);
        }

        async Task<string> NextNumber(CancellationToken ct, DateTime date)
        {
            var prefix = $"KFV/{date.Year}/";
            var nums = await db.Invoices.AsNoTracking().Where(i => i.Type == InvoiceType.Sales && i.Number.StartsWith(prefix)).Select(i => i.Number).ToListAsync(ct);
            var max = nums.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
            return $"{prefix}{max + 1:0000}";
        }
    }
}
