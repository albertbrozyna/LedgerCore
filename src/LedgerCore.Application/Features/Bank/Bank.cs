using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Bank;

public record BankRowDto(Guid Id, DateTime Date, decimal Amount, string Title, string Counterparty, string CounterpartyAccount,
    BankTransactionStatus Status, bool IsDuplicate, Guid? InvoiceId);

static class BankSupport
{
    public static async Task<Account> BankAccount(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var a = await db.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainValidationException("Nie znaleziono konta bankowego");
        if (a.Type != AccountType.Asset || !a.IsActive) throw new DomainValidationException("Konto bankowe musi być aktywnym kontem aktywów (kasa/bank)");
        return a;
    }

    /// <summary>Skrót operacji: rachunek firmy + data + kwota + znormalizowany tytuł i rachunek kontrahenta + kolejny egzemplarz w pliku.</summary>
    public static string Hash(Guid accountId, ParsedRow r, int occurrence)
    {
        var title = string.Concat(r.Title.ToLowerInvariant().Where(char.IsLetterOrDigit));
        var key = $"{accountId:N}|{r.Date:yyyy-MM-dd}|{r.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{title}|{r.CounterpartyAccount}|{occurrence}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    /// <summary>Księguje operację według reguły: wpływ Wn bank / Ma konto reguły, wypłata Wn konto reguły / Ma bank. Bez faktury.</summary>
    public static async Task PostByRule(IAppDbContext db, IUserDetails me, AuditLog audit, BankTransaction bt, BankRule rule, Account bank, CancellationToken ct)
    {
        var target = await db.Accounts.FirstOrDefaultAsync(a => a.Id == rule.TargetAccountId, ct)
                     ?? throw new DomainValidationException($"Konto reguły „{rule.Name}” już nie istnieje");
        if (!target.IsActive) throw new DomainValidationException($"Konto {target.Code} z reguły „{rule.Name}” jest nieaktywne");
        if (target.Id == bank.Id) throw new DomainValidationException("Konto reguły nie może być kontem bankowym operacji");
        await PeriodGuard.EnsureOpen(db, bt.Date, ct);
        var abs = Math.Abs(bt.Amount);
        var desc = rule.Description ?? $"{rule.Name}: {bt.Title}";
        var tx = new Transaction(desc.Length > 500 ? desc[..500] : desc, bt.Date, await TransactionNumbers.Next(db, bt.Date, ct), me.User?.Id);
        if (bt.Amount > 0) { tx.AddEntry(bank.Id, abs, DebitCredit.Debit); tx.AddEntry(target.Id, abs, DebitCredit.Credit); }
        else { tx.AddEntry(target.Id, abs, DebitCredit.Debit); tx.AddEntry(bank.Id, abs, DebitCredit.Credit); }
        tx.Validate();
        bt.MarkPostedByRule(tx.Id, rule.Id);
        db.Transactions.Add(tx);
        await audit.Add("Wyciąg bankowy", $"Zaksięgowano operację {bt.Date:yyyy-MM-dd} {bt.Amount:0.00} „{bt.Title}” wg reguły „{rule.Name}” ({tx.Number})", ct);
        await db.SaveChangesAsync(ct);
    }
}

public static class ImportStatement
{
    public record Command(Guid AccountId, BankFormat Format, CsvProfile Profile, string Content, string? FileName, string? OwnAccount, bool ApplyRules)
        : IRequest<Result>;

    public record Result(Guid StatementId, int Imported, int Duplicates, int Skipped, decimal? OpeningBalance, decimal? ClosingBalance, DateTime? ClosingDate,
        int RulesPosted, List<string> RuleErrors, List<BankRowDto> Rows);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AccountId).NotEmpty().WithMessage("Wybierz konto bankowe");
            RuleFor(x => x.Format).IsInEnum();
            RuleFor(x => x.Profile).IsInEnum();
            RuleFor(x => x.Content).NotEmpty().WithMessage("Plik wyciągu jest pusty").MaximumLength(5_000_000).WithMessage("Plik wyciągu jest zbyt duży (limit 5 MB)");
            RuleFor(x => x.FileName).MaximumLength(255);
        }
    }

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            await CompanyMode.RequireFullBooks(db, ct);
            var bank = await BankSupport.BankAccount(db, r.AccountId, ct);
            var parsed = BankParsers.For(r.Format, r.Profile, r.OwnAccount).Parse(r.Content);
            if (parsed.Rows.Count == 0)
                throw new DomainValidationException(r.Format == BankFormat.Csv
                    ? "Nie znaleziono żadnych operacji. Plik powinien zawierać kolumny: data, kwota, tytuł (opcjonalnie kontrahent)."
                    : "Nie znaleziono żadnych operacji w pliku – sprawdź, czy wybrano właściwy format wyciągu");

            // skróty z numerem kolejnego egzemplarza: dwie identyczne operacje tego samego dnia to dwie operacje, ale ten sam plik nie zdubluje żadnej
            var seen = new Dictionary<string, int>();
            var items = parsed.Rows.Select(row =>
            {
                var baseKey = BankSupport.Hash(r.AccountId, row, 0);
                var n = seen.TryGetValue(baseKey, out var c) ? c : 0;
                seen[baseKey] = n + 1;
                return (Row: row, Hash: BankSupport.Hash(r.AccountId, row, n));
            }).ToList();

            var hashes = items.Select(i => i.Hash).ToList();
            var existing = await db.BankTransactions.Where(b => b.AccountId == r.AccountId && hashes.Contains(b.Hash)).ToDictionaryAsync(b => b.Hash, ct);

            var name = (r.FileName ?? "").Trim();
            if (name.Length == 0) name = "wklejony tekst";
            var dup = items.Count(i => existing.ContainsKey(i.Hash));
            var stmt = new BankStatement(r.AccountId, name, r.Format.ToString(), me.User?.Id.ToString() ?? "system",
                parsed.Opening, parsed.Closing, parsed.ClosingDate, items.Count - dup, dup, parsed.Skipped);
            var added = new List<BankTransaction>();
            var rowMap = new List<(BankTransaction Op, bool Duplicate)>();
            foreach (var (row, hash) in items)
            {
                if (existing.TryGetValue(hash, out var old)) { rowMap.Add((old, true)); continue; }
                var bt = new BankTransaction(stmt.Id, r.AccountId, row.Date, row.Amount, row.Title, row.Counterparty, row.CounterpartyAccount, hash);
                added.Add(bt); rowMap.Add((bt, false));
            }

            db.BankStatements.Add(stmt);
            db.BankTransactions.AddRange(added);
            await audit.Add("Wyciąg bankowy", $"Zaimportowano wyciąg „{stmt.FileName}” ({r.Format}): nowych operacji {added.Count}, duplikatów {dup}, pominiętych wierszy {parsed.Skipped}", ct);
            await db.SaveChangesAsync(ct);

            var posted = 0;
            var errors = new List<string>();
            if (r.ApplyRules && added.Count > 0)
                (posted, errors) = await RuleEngine.Apply(db, me, audit, bank, added, ct);

            var dtos = rowMap.Select(x => new BankRowDto(x.Op.Id, x.Op.Date, x.Op.Amount, x.Op.Title, x.Op.Counterparty, x.Op.CounterpartyAccount,
                x.Op.Status, x.Duplicate, x.Op.InvoiceId)).ToList();
            return new Result(stmt.Id, added.Count, dup, parsed.Skipped, parsed.Opening, parsed.Closing, parsed.ClosingDate, posted, errors, dtos);
        }
    }
}

static class RuleEngine
{
    public static async Task<(int Posted, List<string> Errors)> Apply(IAppDbContext db, IUserDetails me, AuditLog audit, Account bank, List<BankTransaction> ops, CancellationToken ct)
    {
        var rules = await db.BankRules.Where(x => x.IsActive).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var posted = 0;
        var errors = new List<string>();
        foreach (var bt in ops.Where(o => o.Status == BankTransactionStatus.New).OrderBy(o => o.Date))
        {
            var rule = rules.FirstOrDefault(x => x.Matches(bt.Amount, bt.Title, bt.Counterparty));
            if (rule == null) continue;
            try { await BankSupport.PostByRule(db, me, audit, bt, rule, bank, ct); posted++; }
            catch (DomainException e) { errors.Add($"{bt.Date:yyyy-MM-dd} {bt.Amount:0.00} „{bt.Title}”: {e.Message}"); }
        }
        return (posted, errors);
    }
}

public static class ApplyBankRules
{
    public record Command(Guid AccountId) : IRequest<Result>;
    public record Result(int Posted, int Matched, List<string> Errors);

    public class Handler(IAppDbContext db, IUserDetails me, AuditLog audit) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            await CompanyMode.RequireFullBooks(db, ct);
            var bank = await BankSupport.BankAccount(db, r.AccountId, ct);
            var ops = await db.BankTransactions.Where(b => b.AccountId == r.AccountId && b.Status == BankTransactionStatus.New).ToListAsync(ct);
            var (posted, errors) = await RuleEngine.Apply(db, me, audit, bank, ops, ct);
            return new Result(posted, posted + errors.Count, errors);
        }
    }
}

public static class BankTransactions
{
    public record ListQuery(Guid? AccountId, BankTransactionStatus? Status, Guid? StatementId) : IRequest<List<BankRowDto>>;
    public class ListHandler(IAppDbContext db) : IRequestHandler<ListQuery, List<BankRowDto>>
    {
        public async Task<List<BankRowDto>> Handle(ListQuery r, CancellationToken ct)
        {
            var q = db.BankTransactions.AsNoTracking().AsQueryable();
            if (r.AccountId is { } a) q = q.Where(x => x.AccountId == a);
            if (r.Status is { } s) q = q.Where(x => x.Status == s);
            if (r.StatementId is { } st) q = q.Where(x => x.StatementId == st);
            var list = await q.OrderByDescending(x => x.Date).ThenBy(x => x.Title).Take(500).ToListAsync(ct);
            return list.Select(b => new BankRowDto(b.Id, b.Date, b.Amount, b.Title, b.Counterparty, b.CounterpartyAccount, b.Status, false, b.InvoiceId)).ToList();
        }
    }

    public record PostToInvoiceCommand(Guid Id, Guid InvoiceId) : IRequest<decimal>;
    public class PostToInvoiceHandler(IAppDbContext db, ISender sender, AuditLog audit) : IRequestHandler<PostToInvoiceCommand, decimal>
    {
        public async Task<decimal> Handle(PostToInvoiceCommand r, CancellationToken ct)
        {
            await CompanyMode.RequireFullBooks(db, ct);
            var bt = await db.BankTransactions.FirstOrDefaultAsync(b => b.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono operacji z wyciągu");
            bt.EnsureNew();
            var inv = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == r.InvoiceId, ct) ?? throw new DomainValidationException("Nie znaleziono faktury");
            if ((bt.Amount > 0) != (inv.Type == InvoiceType.Sales))
                throw new DomainValidationException(bt.Amount > 0 ? "Wpływ można przypisać tylko do faktury sprzedaży" : "Wypłatę można przypisać tylko do faktury zakupu");
            var amount = Math.Min(Math.Abs(bt.Amount), inv.Remaining);
            if (amount <= 0) throw new DomainValidationException("Faktura jest już w pełni opłacona");
            await sender.Send(new RecordPayment.Command(inv.Id, bt.Date, amount, bt.AccountId), ct);
            bt.MarkPostedToInvoice(inv.Id); // jedna operacja = jedna płatność
            await audit.Add("Wyciąg bankowy", $"Operację {bt.Date:yyyy-MM-dd} {bt.Amount:0.00} przypisano do faktury {inv.Number} (płatność {amount:0.00})", ct);
            await db.SaveChangesAsync(ct);
            return amount;
        }
    }

    public record SetSkippedCommand(Guid Id, bool Skip) : IRequest<Unit>;
    public class SetSkippedHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<SetSkippedCommand, Unit>
    {
        public async Task<Unit> Handle(SetSkippedCommand r, CancellationToken ct)
        {
            var bt = await db.BankTransactions.FirstOrDefaultAsync(b => b.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono operacji z wyciągu");
            if (r.Skip) bt.Skip(); else bt.Restore();
            await audit.Add("Wyciąg bankowy", $"{(r.Skip ? "Pominięto" : "Przywrócono")} operację {bt.Date:yyyy-MM-dd} {bt.Amount:0.00} „{bt.Title}”", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

public static class BankRules
{
    public record Item(Guid Id, string Name, string MatchText, RuleDirection Direction, Guid TargetAccountId, string TargetAccount, string? Description, bool IsActive);

    public record ListQuery : IRequest<List<Item>>;
    public class ListHandler(IAppDbContext db) : IRequestHandler<ListQuery, List<Item>>
    {
        public async Task<List<Item>> Handle(ListQuery r, CancellationToken ct)
        {
            var rules = await db.BankRules.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync(ct);
            var accounts = await db.Accounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => $"{a.Code} {a.Name}", ct);
            return rules.Select(x => new Item(x.Id, x.Name, x.MatchText, x.Direction, x.TargetAccountId,
                accounts.TryGetValue(x.TargetAccountId, out var n) ? n : "(konto usunięte)", x.Description, x.IsActive)).ToList();
        }
    }

    public record SaveCommand(Guid? Id, string Name, string MatchText, RuleDirection Direction, Guid TargetAccountId, string? Description, bool IsActive) : IRequest<Guid>;
    public class SaveValidator : AbstractValidator<SaveCommand>
    {
        public SaveValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Nazwa reguły jest wymagana").MaximumLength(100);
            RuleFor(x => x.MatchText).NotEmpty().WithMessage("Podaj tekst do dopasowania").MaximumLength(100);
            RuleFor(x => x.Description).MaximumLength(300);
            RuleFor(x => x.TargetAccountId).NotEmpty().WithMessage("Wybierz konto księgowania");
        }
    }
    public class SaveHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<SaveCommand, Guid>
    {
        public async Task<Guid> Handle(SaveCommand r, CancellationToken ct)
        {
            var acc = await db.Accounts.FirstOrDefaultAsync(a => a.Id == r.TargetAccountId, ct) ?? throw new DomainValidationException("Nie znaleziono konta księgowania");
            if (!acc.IsActive) throw new DomainValidationException($"Konto {acc.Code} jest nieaktywne");
            if (r.Id is { } id)
            {
                var rule = await db.BankRules.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainValidationException("Nie znaleziono reguły");
                rule.Update(r.Name, r.MatchText, r.Direction, r.TargetAccountId, r.Description, r.IsActive);
                await audit.Add("Reguła bankowa", $"Zmieniono regułę „{rule.Name}”", ct);
                await db.SaveChangesAsync(ct);
                return rule.Id;
            }
            var n = new BankRule(r.Name, r.MatchText, r.Direction, r.TargetAccountId, r.Description, r.IsActive);
            db.BankRules.Add(n);
            await audit.Add("Reguła bankowa", $"Dodano regułę „{n.Name}”: „{n.MatchText}” → {acc.Code}", ct);
            await db.SaveChangesAsync(ct);
            return n.Id;
        }
    }

    public record DeleteCommand(Guid Id) : IRequest<Unit>;
    public class DeleteHandler(IAppDbContext db, AuditLog audit) : IRequestHandler<DeleteCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteCommand r, CancellationToken ct)
        {
            var rule = await db.BankRules.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new DomainValidationException("Nie znaleziono reguły");
            db.BankRules.Remove(rule);
            await audit.Add("Reguła bankowa", $"Usunięto regułę „{rule.Name}”", ct);
            await db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

public static class BankReconciliation
{
    public record StatementItem(Guid Id, string FileName, string Format, DateTime ImportedAt, string ImportedBy, decimal? OpeningBalance, decimal? ClosingBalance,
        DateTime? ClosingDate, int ImportedCount, int DuplicateCount, int SkippedCount);
    public record UnpostedItem(Guid Id, DateTime Date, decimal Amount, string Title, string Counterparty);
    public record Result(Guid AccountId, DateTime AsOf, decimal LedgerBalance, decimal? StatementBalance, string StatementSource, DateTime? StatementDate,
        decimal? Difference, bool Matches, decimal UnpostedSum, decimal? DifferenceAfterUnposted, List<UnpostedItem> Unposted);

    public record StatementsQuery(Guid AccountId) : IRequest<List<StatementItem>>;
    public class StatementsHandler(IAppDbContext db) : IRequestHandler<StatementsQuery, List<StatementItem>>
    {
        public async Task<List<StatementItem>> Handle(StatementsQuery r, CancellationToken ct) =>
            (await db.BankStatements.AsNoTracking().Where(s => s.AccountId == r.AccountId).OrderByDescending(s => s.ImportedAt).Take(100).ToListAsync(ct))
            .Select(s => new StatementItem(s.Id, s.FileName, s.Format, s.ImportedAt, s.ImportedBy, s.OpeningBalance, s.ClosingBalance, s.ClosingDate,
                s.ImportedCount, s.DuplicateCount, s.SkippedCount)).ToList();
    }

    /// <summary>Saldo końcowe z wyciągu (z pliku MT940 albo wpisane ręcznie) kontra saldo konta w księdze na dzień.</summary>
    public record Query(Guid AccountId, DateTime? AsOf, decimal? ClosingBalance) : IRequest<Result>;
    public class Handler(IAppDbContext db) : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query r, CancellationToken ct)
        {
            var acc = await BankSupport.BankAccount(db, r.AccountId, ct);
            var last = await db.BankStatements.AsNoTracking().Where(s => s.AccountId == r.AccountId && s.ClosingBalance != null)
                .OrderByDescending(s => s.ClosingDate).ThenByDescending(s => s.ImportedAt).FirstOrDefaultAsync(ct);
            decimal? statement = r.ClosingBalance ?? last?.ClosingBalance;
            var source = r.ClosingBalance != null ? "wpisane ręcznie" : last != null ? $"z pliku „{last.FileName}”" : "brak";
            var asOf = (r.AsOf ?? (r.ClosingBalance == null ? last?.ClosingDate : null) ?? DateTime.Today).Date;
            var sums = await LedgerQueries.Sums(db, null, asOf, false, acc.Id, ct);
            var ledger = LedgerQueries.Of(sums, acc.Id).Net;
            var unposted = await db.BankTransactions.AsNoTracking().Where(b => b.AccountId == acc.Id && b.Status == BankTransactionStatus.New && b.Date <= asOf)
                .OrderBy(b => b.Date).ToListAsync(ct);
            var unpostedSum = unposted.Sum(b => b.Amount);
            decimal? diff = statement == null ? null : ledger - statement;
            return new Result(acc.Id, asOf, ledger, statement, source, r.ClosingBalance != null ? null : last?.ClosingDate, diff, diff == 0, unpostedSum,
                statement == null ? null : ledger + unpostedSum - statement,
                unposted.Select(b => new UnpostedItem(b.Id, b.Date, b.Amount, b.Title, b.Counterparty)).ToList());
        }
    }
}
