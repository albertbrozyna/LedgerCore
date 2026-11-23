using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Administration;

public static class GetCompany
{
    public record Query : IRequest<Response>;
    public record Response(string Name, string? Nip, string? Address, string? Email, string? Phone, string? BankAccount, string? InvoiceFooter);

    public class Handler(IAppDbContext db, ICompanyContext company) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query r, CancellationToken ct)
        {
            var c = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == company.CompanyId, ct);
            return c is null ? new Response("", null, null, null, null, null, null)
                : new Response(c.Name, c.Nip, c.Address, c.Email, c.Phone, c.BankAccount, c.InvoiceFooter);
        }
    }
}

public static class SaveCompany
{
    public record Command(string Name, string? Nip, string? Address, string? Email, string? Phone, string? BankAccount, string? InvoiceFooter)
        : IRequest<Unit>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa firmy jest wymagana").MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(300);
            RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Nieprawidłowy e-mail");
            RuleFor(x => x.BankAccount).MaximumLength(40);
            RuleFor(x => x.InvoiceFooter).MaximumLength(500);
        }
    }

    public class Handler(IAppDbContext db, AuditLog audit, ICompanyContext company) : IRequestHandler<Command, Unit>
    {
        public async Task<Unit> Handle(Command r, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(r.Nip) && !Contractor.IsValidNip(r.Nip))
                throw new DomainValidationException("Nieprawidłowy NIP firmy (zła suma kontrolna)");
            var c = await db.Companies.FirstOrDefaultAsync(x => x.Id == company.CompanyId, ct) ?? throw new DomainValidationException("Nie wybrano firmy");
            c.Update(r.Name, r.Nip, r.Address, r.Email, r.Phone, r.BankAccount, r.InvoiceFooter);
            await audit.Add("Dane firmy", $"Zmieniono dane firmy: {c.Name}", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

/// <summary>
/// Zamknięcie roku obrotowego: konta przychodów i kosztów są zerowane księgowaniem „ZR/rok/0001” z 31 grudnia,
/// a wynik trafia na konto 860 „Wynik finansowy” (tworzone, jeśli go brak). Następnie okres jest blokowany do 31 grudnia.
/// Salda kont bilansowych przechodzą na nowy rok automatycznie (księga jest ciągła).
/// </summary>
public static class CloseYear
{
    public const string ResultAccountCode = "860";
    public static string Number(int year) => $"ZR/{year}/0001";

    public record Command(int Year) : IRequest<Response>;
    public record Response(string Number, decimal Result);

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command r, CancellationToken ct)
        {
            if (r.Year < 2000 || r.Year > DateTime.Today.Year) throw new DomainValidationException("Nieprawidłowy rok");
            var end = new DateTime(r.Year, 12, 31);
            var number = Number(r.Year);
            if (await db.Transactions.AnyAsync(t => t.Number == number, ct))
                throw new DomainValidationException($"Rok {r.Year} został już zamknięty");
            await PeriodGuard.EnsureOpen(db, end, ct);

            // otwarte (niezapłacone) faktury nie blokują zamknięcia – rozrachunki są kontami bilansowymi i przechodzą dalej
            var accounts = await db.Accounts.Where(a => a.Type == AccountType.Income || a.Type == AccountType.Expense).ToListAsync(ct);
            var entries = (await LedgerQueries.LoadEntries(db, new DateTime(r.Year, 1, 1), end, null, ct)).ToLookup(e => e.AccountId);

            var tx = new Transaction($"Zamknięcie roku obrotowego {r.Year} – przeniesienie wyniku finansowego", end, number, me.User?.Id);
            decimal result = 0;
            foreach (var a in accounts.OrderBy(a => a.Code, StringComparer.Ordinal))
            {
                var net = LedgerQueries.Net(entries[a.Id]); // Winien − Ma
                if (net == 0) continue;
                // wyzerowanie salda: strona przeciwna do salda
                if (net > 0) tx.AddEntry(a.Id, net, DebitCredit.Credit); else tx.AddEntry(a.Id, -net, DebitCredit.Debit);
                result += -net; // przychody (Ma) zwiększają wynik, koszty (Wn) zmniejszają
            }
            if (!tx.Entries.Any()) throw new DomainValidationException($"Brak obrotów na kontach wynikowych w roku {r.Year} – nie ma czego zamykać");

            var resultAcc = await db.Accounts.FirstOrDefaultAsync(a => a.Code == ResultAccountCode, ct);
            if (resultAcc == null)
            {
                resultAcc = new Account("Wynik finansowy", ResultAccountCode, AccountType.Equity);
                db.Accounts.Add(resultAcc);
            }
            if (result > 0) tx.AddEntry(resultAcc.Id, result, DebitCredit.Credit);
            else if (result < 0) tx.AddEntry(resultAcc.Id, -result, DebitCredit.Debit);
            tx.Validate();

            db.Transactions.Add(tx);
            var row = await db.PeriodLocks.FirstOrDefaultAsync(ct);
            if (row == null) { row = new PeriodLock(); db.PeriodLocks.Add(row); }
            if (row.LockedThrough == null || row.LockedThrough < end) row.Set(end);
            await audit.Add("Zamknięcie roku", $"Zamknięto rok obrotowy {r.Year}, wynik finansowy {result:0.00}", ct);
            await db.SaveChangesAsync(ct);
            return new Response(number, result);
        }
    }
}
