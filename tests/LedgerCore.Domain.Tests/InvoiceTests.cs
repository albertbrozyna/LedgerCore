using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Tests;

public class InvoiceTests
{
    static readonly DateTime Day = new(2026, 5, 10);

    static Invoice Make(params (string, decimal, VatRate)[] lines) =>
        new(InvoiceType.Sales, "FV/2026/0001", Guid.NewGuid(), Day, Day, Day.AddDays(14), Guid.NewGuid(), null, null,
            lines.Length == 0 ? [("Usługa", 100m, VatRate.R23)] : lines);

    static Invoice MakeCorrection(Invoice original, params (string, decimal, VatRate)[] lines) =>
        new(InvoiceType.Sales, "KFV/2026/0001", original.ContractorId, Day, Day, Day, original.ResultAccountId, null, null, lines, original.Id);

    [Theory]
    [InlineData(VatRate.R23, 23)]
    [InlineData(VatRate.R8, 8)]
    [InlineData(VatRate.R5, 5)]
    [InlineData(VatRate.R0, 0)]
    [InlineData(VatRate.Exempt, 0)]
    public void VatRate_Percent(VatRate rate, int expected) => Assert.Equal(expected, rate.Percent());

    [Fact]
    public void Totals_SumNetVatAndGross()
    {
        var inv = Make(("A", 100m, VatRate.R23), ("B", 200m, VatRate.R8), ("C", 50m, VatRate.R0));
        Assert.Equal(350m, inv.Net);
        Assert.Equal(23m + 16m, inv.Vat);
        Assert.Equal(389m, inv.Gross);
        Assert.Equal(inv.Gross, inv.Remaining);
    }

    [Theory]
    [InlineData("0.01", "0.00")]   // 0,0023 -> 0,00
    [InlineData("0.02", "0.00")]   // 0,0046 -> 0,00
    [InlineData("0.03", "0.01")]   // 0,0069 -> 0,01
    [InlineData("10.99", "2.53")]  // 2,5277 -> 2,53
    [InlineData("1.22", "0.28")]   // 0,2806 -> 0,28
    public void VatPerLine_IsRoundedToTwoDecimals(string net, string expectedVat)
    {
        var inv = Make(("A", decimal.Parse(net, System.Globalization.CultureInfo.InvariantCulture), VatRate.R23));
        Assert.Equal(decimal.Parse(expectedVat, System.Globalization.CultureInfo.InvariantCulture), inv.Vat);
    }

    [Fact]
    public void Vat_IsRoundedHalfAwayFromZero()
    {
        // 0,50 * 5% = 0,025 -> 0,03 (a nie 0,02 jak przy zaokrągleniu bankierskim)
        Assert.Equal(0.03m, Make(("A", 0.50m, VatRate.R5)).Vat);
    }

    [Fact]
    public void Vat_IsRoundedPerLineNotOnTotal()
    {
        // trzy pozycje po 0,10 zł: VAT 23% = 0,023 -> 0,02 każda => 0,06 (od sumy 0,30 byłoby 0,07)
        var inv = Make(("A", 0.10m, VatRate.R23), ("B", 0.10m, VatRate.R23), ("C", 0.10m, VatRate.R23));
        Assert.Equal(0.06m, inv.Vat);
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneLine()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Invoice(InvoiceType.Sales, "FV/1", Guid.NewGuid(), Day, Day, Day, Guid.NewGuid(), null, null, []));
    }

    [Fact]
    public void Constructor_RequiresNumber()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Invoice(InvoiceType.Sales, " ", Guid.NewGuid(), Day, Day, Day, Guid.NewGuid(), null, null, [("A", 1m, VatRate.R23)]));
    }

    [Fact]
    public void Constructor_RejectsDueDateBeforeIssueDate()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Invoice(InvoiceType.Sales, "FV/1", Guid.NewGuid(), Day, Day, Day.AddDays(-1), Guid.NewGuid(), null, null, [("A", 1m, VatRate.R23)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Line_RejectsNonPositiveNet(int net)
    {
        Assert.Throws<DomainValidationException>(() => Make(("A", net, VatRate.R23)));
    }

    [Fact]
    public void Line_RejectsEmptyDescription()
    {
        Assert.Throws<DomainValidationException>(() => Make((" ", 10m, VatRate.R23)));
    }

    [Fact]
    public void Line_RejectsMoreThanTwoDecimalPlaces()
    {
        Assert.Throws<DomainValidationException>(() => Make(("A", 10.005m, VatRate.R23)));
    }

    [Fact]
    public void AddPayment_ReducesRemaining()
    {
        var inv = Make(); // brutto 123,00
        inv.AddPayment(Day, 23m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(23m, inv.PaidAmount);
        Assert.Equal(100m, inv.Remaining);
        inv.AddPayment(Day, 100m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0m, inv.Remaining);
        Assert.Equal(2, inv.Payments.Count);
    }

    [Fact]
    public void AddPayment_RejectsOverpayment()
    {
        var inv = Make();
        Assert.Throws<DomainValidationException>(() => inv.AddPayment(Day, 123.01m, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(0m, inv.PaidAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void AddPayment_RejectsNonPositiveAmount(int amount)
    {
        Assert.Throws<DomainValidationException>(() => Make().AddPayment(Day, amount, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void AddPayment_RejectsMoreThanTwoDecimalPlaces()
    {
        Assert.Throws<DomainValidationException>(() => Make().AddPayment(Day, 1.005m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void AddPayment_RejectedOnCanceledInvoice()
    {
        var inv = Make();
        inv.Cancel();
        Assert.Throws<DomainValidationException>(() => inv.AddPayment(Day, 10m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Cancel_RejectedWhenPaymentsExist()
    {
        var inv = Make();
        inv.AddPayment(Day, 10m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(() => inv.Cancel());
        Assert.False(inv.IsCanceled);
    }

    [Fact]
    public void Cancel_TwiceIsRejected()
    {
        var inv = Make();
        inv.Cancel();
        Assert.True(inv.IsCanceled);
        Assert.Throws<DomainValidationException>(() => inv.Cancel());
    }

    [Fact]
    public void Correction_UsesSignedLinesAndHasNoRemaining()
    {
        var original = Make(); // 100 + 23
        var corr = MakeCorrection(original, ("Rabat", -40m, VatRate.R23));
        Assert.True(corr.IsCorrection);
        Assert.Equal(-40m, corr.Net);
        Assert.Equal(-9.20m, corr.Vat);
        Assert.Equal(-49.20m, corr.Gross);
        Assert.Equal(0m, corr.Remaining);
    }

    [Fact]
    public void Correction_RejectsZeroLine()
    {
        var original = Make();
        Assert.Throws<DomainValidationException>(() => MakeCorrection(original, ("X", 0m, VatRate.R23)));
    }

    [Fact]
    public void ApplyCorrection_ChangesRemainingOfOriginal()
    {
        var inv = Make(); // 123
        inv.ApplyCorrection(-49.20m);
        Assert.Equal(73.80m, inv.Remaining);
        inv.ApplyCorrection(12.30m);
        Assert.Equal(86.10m, inv.Remaining);
    }

    [Fact]
    public void ApplyCorrection_CannotGoBelowPaidAmount()
    {
        var inv = Make();
        inv.AddPayment(Day, 100m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(() => inv.ApplyCorrection(-30m)); // 93 < 100
        Assert.Equal(0, inv.CorrectedAmount);
    }

    [Fact]
    public void ApplyCorrection_CanReduceToExactlyPaidAmount()
    {
        var inv = Make();
        inv.AddPayment(Day, 100m, Guid.NewGuid(), Guid.NewGuid());
        inv.ApplyCorrection(-23m);
        Assert.Equal(0m, inv.Remaining);
    }

    [Fact]
    public void ApplyCorrection_RejectedOnCorrectionAndCanceled()
    {
        var original = Make();
        var corr = MakeCorrection(original, ("Rabat", -10m, VatRate.R23));
        Assert.Throws<DomainValidationException>(() => corr.ApplyCorrection(-1m));

        original.Cancel();
        Assert.Throws<DomainValidationException>(() => original.ApplyCorrection(-1m));
    }

    [Fact]
    public void RevertCorrection_RestoresRemaining()
    {
        var inv = Make();
        inv.ApplyCorrection(-49.20m);
        inv.RevertCorrection(-49.20m);
        Assert.Equal(123m, inv.Remaining);
    }

    [Fact]
    public void RevertCorrection_RejectedWhenInvoiceWouldDropBelowPaid()
    {
        var inv = Make(); // 123
        inv.ApplyCorrection(50m); // 173
        inv.AddPayment(Day, 150m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(() => inv.RevertCorrection(50m)); // 123 < 150
    }

    [Fact]
    public void AddPayment_RejectedOnCorrection()
    {
        var corr = MakeCorrection(Make(), ("Dopłata", 10m, VatRate.R23));
        Assert.Throws<DomainValidationException>(() => corr.AddPayment(Day, 1m, Guid.NewGuid(), Guid.NewGuid()));
    }
}
