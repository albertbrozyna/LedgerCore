using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;

namespace LedgerCore.Domain.Tests;

public class RecurringInvoiceTests
{
    static RecurringInvoice Make(RecurrenceFrequency f, DateTime first, int day, DateTime? end = null) =>
        new("Abonament", Guid.NewGuid(), f, day, 14, first, end, null, null, null, null, [("Usługa", 100m, VatRate.R23)]);

    [Fact]
    public void Monthly_advance_keeps_day_and_clamps_to_month_end()
    {
        var r = Make(RecurrenceFrequency.Monthly, new DateTime(2025, 1, 31), 31);
        var dates = new List<DateTime>();
        for (var i = 0; i < 4; i++) { r.Advance(); dates.Add(r.NextDate); }
        Assert.Equal([new DateTime(2025, 2, 28), new DateTime(2025, 3, 31), new DateTime(2025, 4, 30), new DateTime(2025, 5, 31)], dates);
    }

    [Fact]
    public void Leap_year_february()
    {
        var r = Make(RecurrenceFrequency.Monthly, new DateTime(2028, 1, 30), 30);
        r.Advance();
        Assert.Equal(new DateTime(2028, 2, 29), r.NextDate);
    }

    [Fact]
    public void Quarterly_and_yearly_advance_across_year_boundary()
    {
        var q = Make(RecurrenceFrequency.Quarterly, new DateTime(2025, 11, 10), 10);
        q.Advance();
        Assert.Equal(new DateTime(2026, 2, 10), q.NextDate);
        var y = Make(RecurrenceFrequency.Yearly, new DateTime(2024, 2, 29), 29);
        y.Advance();
        Assert.Equal(new DateTime(2025, 2, 28), y.NextDate);
        y.Advance();
        Assert.Equal(new DateTime(2026, 2, 28), y.NextDate);
    }

    [Fact]
    public void Template_becomes_inactive_after_end_date_and_is_due_only_until_then()
    {
        var r = Make(RecurrenceFrequency.Monthly, new DateTime(2025, 1, 15), 15, new DateTime(2025, 2, 20));
        Assert.True(r.IsDue(new DateTime(2025, 1, 15)));
        Assert.False(r.IsDue(new DateTime(2025, 1, 14)));
        r.Advance();
        Assert.True(r.IsActive);
        r.Advance();
        Assert.False(r.IsActive);
        Assert.False(r.IsDue(new DateTime(2030, 1, 1)));
    }
}
