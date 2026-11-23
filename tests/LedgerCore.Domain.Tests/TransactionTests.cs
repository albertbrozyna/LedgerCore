using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Tests;

public class TransactionTests
{
    static readonly Guid A = Guid.NewGuid();
    static readonly Guid B = Guid.NewGuid();
    static readonly Guid C = Guid.NewGuid();

    static Transaction Balanced()
    {
        var tx = new Transaction("Zakup", new DateTime(2026, 3, 5, 14, 30, 0), "DK/2026/0001");
        tx.AddEntry(A, 123.45m, DebitCredit.Debit);
        tx.AddEntry(B, 100m, DebitCredit.Credit);
        tx.AddEntry(C, 23.45m, DebitCredit.Credit);
        return tx;
    }

    [Fact]
    public void Validate_PassesForBalancedTransaction()
    {
        var tx = Balanced();
        tx.Validate();
        Assert.Equal(123.45m, tx.TotalDebit);
        Assert.Equal(123.45m, tx.TotalCredit);
    }

    [Fact]
    public void Validate_RejectsUnbalancedTransaction()
    {
        var tx = new Transaction("Test", DateTime.Today, "DK/2026/0001");
        tx.AddEntry(A, 100m, DebitCredit.Debit);
        tx.AddEntry(B, 99.99m, DebitCredit.Credit);
        var ex = Assert.Throws<UnbalancedTransactionException>(() => tx.Validate());
        Assert.Contains("100,00", ex.Message.Replace('.', ','));
    }

    [Fact]
    public void Validate_RejectsSingleEntry()
    {
        var tx = new Transaction("Test", DateTime.Today, "DK/2026/0001");
        tx.AddEntry(A, 100m, DebitCredit.Debit);
        Assert.Throws<DomainValidationException>(() => tx.Validate());
    }

    [Fact]
    public void Validate_RejectsNoEntries()
    {
        var tx = new Transaction("Test", DateTime.Today, "DK/2026/0001");
        Assert.Throws<DomainValidationException>(() => tx.Validate());
    }

    [Fact]
    public void Validate_RejectsOnlyDebitSideEvenWithTwoEntries()
    {
        var tx = new Transaction("Test", DateTime.Today, "DK/2026/0001");
        tx.AddEntry(A, 50m, DebitCredit.Debit);
        tx.AddEntry(B, 50m, DebitCredit.Debit);
        Assert.Throws<UnbalancedTransactionException>(() => tx.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RequiresDescription(string description)
    {
        Assert.Throws<ArgumentException>(() => new Transaction(description, DateTime.Today, "DK/2026/0001"));
    }

    [Fact]
    public void Constructor_TrimsDescriptionAndStripsTimeFromDate()
    {
        var tx = new Transaction("  Opis  ", new DateTime(2026, 3, 5, 23, 59, 0), "DK/2026/0001");
        Assert.Equal("Opis", tx.Description);
        Assert.Equal(new DateTime(2026, 3, 5), tx.Date);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddEntry_RejectsNonPositiveAmount(double amount)
    {
        var tx = new Transaction("Test", DateTime.Today, "DK/2026/0001");
        Assert.Throws<NegativeAmountException>(() => tx.AddEntry(A, (decimal)amount, DebitCredit.Debit));
    }

    [Fact]
    public void AddEntry_RejectsMoreThanTwoDecimalPlaces()
    {
        var tx = new Transaction("Test", DateTime.Today, "DK/2026/0001");
        Assert.Throws<DomainValidationException>(() => tx.AddEntry(A, 10.001m, DebitCredit.Debit));
    }

    [Fact]
    public void AddEntry_AssignsTransactionIdToEntry()
    {
        var tx = Balanced();
        Assert.All(tx.Entries, e => Assert.Equal(tx.Id, e.TransactionId));
    }

    [Fact]
    public void CreateReversal_SwapsSidesAndKeepsAmountsAndAccounts()
    {
        var tx = Balanced();
        var user = Guid.NewGuid();
        var rev = tx.CreateReversal("DK/2026/0002", new DateTime(2026, 4, 1), user);

        Assert.Equal(tx.Id, rev.ReversesTransactionId);
        Assert.Equal(user, rev.CreatedByUserId);
        Assert.Equal(tx.TotalCredit, rev.TotalDebit);
        Assert.Equal(tx.TotalDebit, rev.TotalCredit);
        Assert.Equal(tx.Entries.Count, rev.Entries.Count);
        foreach (var orig in tx.Entries)
        {
            var mirrored = rev.Entries.Single(e => e.AccountId == orig.AccountId);
            Assert.Equal(orig.Amount, mirrored.Amount);
            Assert.NotEqual(orig.Side, mirrored.Side);
        }
    }

    [Fact]
    public void CreateReversal_DescribesOriginalAndIsBalanced()
    {
        var tx = Balanced();
        var rev = tx.CreateReversal("DK/2026/0002", DateTime.Today, null);
        Assert.StartsWith("Storno DK/2026/0001", rev.Description);
        Assert.Equal(rev.TotalDebit, rev.TotalCredit);
    }

    [Fact]
    public void CreateReversal_DoesNotModifyOriginal()
    {
        var tx = Balanced();
        var debitBefore = tx.TotalDebit;
        tx.CreateReversal("DK/2026/0002", DateTime.Today, null);
        Assert.Equal(debitBefore, tx.TotalDebit);
        Assert.Null(tx.ReversesTransactionId);
    }
}
