using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;

namespace LedgerCore.Domain.Tests;

public class ContractorTests
{
    [Theory]
    [InlineData("5260250995")]       // poprawny NIP
    [InlineData("526-025-09-95")]    // z myślnikami
    [InlineData("526 025 09 95")]    // ze spacjami
    [InlineData("1234563218")]
    public void IsValidNip_AcceptsCorrectNip(string nip) => Assert.True(Contractor.IsValidNip(nip));

    [Theory]
    [InlineData("5260250996")]       // zła suma kontrolna
    [InlineData("526025099")]        // za krótki
    [InlineData("52602509955")]      // za długi
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abcdefghij")]
    [InlineData("1234567890")]       // zła cyfra kontrolna (poprawna: 7)
    public void IsValidNip_RejectsIncorrectNip(string? nip) => Assert.False(Contractor.IsValidNip(nip));

    [Theory]
    [InlineData("526-025-09-95", "5260250995")]
    [InlineData(" 5260250995 ", "5260250995")]
    [InlineData("PL5260250995", "5260250995")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeNip_KeepsOnlyDigits(string? input, string? expected) => Assert.Equal(expected, Contractor.NormalizeNip(input));

    [Fact]
    public void Constructor_NormalizesFieldsAndRequiresName()
    {
        var c = new Contractor("  ACME  ", "526-025-09-95", " ", " a@b.pl ");
        Assert.Equal("ACME", c.Name);
        Assert.Equal("5260250995", c.Nip);
        Assert.Null(c.Address);
        Assert.Equal("a@b.pl", c.Email);
        Assert.True(c.IsActive);
        Assert.Throws<ArgumentException>(() => new Contractor(" ", null, null, null));
    }
}

public class AccountTests
{
    [Theory]
    [InlineData(AccountType.Asset, true)]
    [InlineData(AccountType.Expense, true)]
    [InlineData(AccountType.Liability, false)]
    [InlineData(AccountType.Equity, false)]
    [InlineData(AccountType.Income, false)]
    public void IsDebitNormal_DependsOnType(AccountType type, bool expected) =>
        Assert.Equal(expected, new Account("Konto", "100", type).IsDebitNormal);

    [Fact]
    public void Constructor_RequiresNameAndCode()
    {
        Assert.Throws<ArgumentException>(() => new Account(" ", "100", AccountType.Asset));
        Assert.Throws<ArgumentException>(() => new Account("Kasa", " ", AccountType.Asset));
    }

    [Fact]
    public void Constructor_TrimsAndActivates()
    {
        var a = new Account("  Kasa ", " 100 ", AccountType.Asset);
        Assert.Equal("Kasa", a.Name);
        Assert.Equal("100", a.Code);
        Assert.True(a.IsActive);
    }

    [Fact]
    public void RenameAndSetActive()
    {
        var a = new Account("Kasa", "100", AccountType.Asset);
        a.Rename(" Kasa główna ");
        a.SetActive(false);
        Assert.Equal("Kasa główna", a.Name);
        Assert.False(a.IsActive);
        Assert.Throws<ArgumentException>(() => a.Rename(" "));
    }
}
