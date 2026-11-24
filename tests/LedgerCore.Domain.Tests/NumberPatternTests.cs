using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Tests;

public class NumberPatternTests
{
    static NumberPattern P(string text, NumberReset reset = NumberReset.Yearly) => NumberPattern.Parse(text, reset);

    [Theory]
    [InlineData("FV/{RRRR}/{NNNN}", 2026, 3, 7, "FV/2026/0007")]
    [InlineData("{NNN}/{MM}/{RR}", 2026, 3, 7, "007/03/26")]
    [InlineData("F-{RRRR}{MM}-{N}", 2025, 12, 12, "F-202512-12")]
    [InlineData("{NN}", 2025, 1, 123, "123")]
    public void Formats_numbers(string pattern, int year, int month, int seq, string expected)
    {
        var reset = pattern.Contains("{MM}") && pattern.Contains("{RRRR}") ? NumberReset.Monthly : pattern.Contains("{RR") ? NumberReset.Yearly : NumberReset.Never;
        Assert.Equal(expected, P(pattern, reset).Format(new DateTime(year, month, 1), seq));
    }

    [Theory]
    [InlineData("", "jest wymagany")]
    [InlineData("FV/{RRRR}/", "dokładnie jeden znacznik numeru")]
    [InlineData("{NNNN}/{N}/{RRRR}", "dokładnie jeden znacznik numeru")]
    [InlineData("FV/{RRRR}/{XYZ}/{NNNN}", "Nieznany znacznik {XYZ}")]
    [InlineData("FV/{RRRR}/{NNNNNNNNN}", "Nieznany znacznik")]
    [InlineData("FV/{RRRR}/{RR}/{NNNN}", "tylko raz")]
    [InlineData("FV/{RRRR}/{MM}/{MM}/{NNNN}", "tylko raz")]
    [InlineData("FV/{RRRR/{NNNN}", "Nawiasy klamrowe")]
    [InlineData("FV\\{RRRR}/{NNNN}", "litery, cyfry")]
    [InlineData("FV/{NNNN}", "zawierać rok")]
    public void Rejects_invalid_patterns(string pattern, string messagePart)
    {
        var e = Assert.Throws<DomainValidationException>(() => P(pattern));
        Assert.Contains(messagePart, e.Message);
    }

    [Fact]
    public void Monthly_reset_requires_year_and_month()
    {
        Assert.Throws<DomainValidationException>(() => P("FV/{RRRR}/{NNNN}", NumberReset.Monthly));
        Assert.Throws<DomainValidationException>(() => P("FV/{MM}/{NNNN}", NumberReset.Monthly));
        P("FV/{RRRR}/{MM}/{NNNN}", NumberReset.Monthly);
    }

    [Fact]
    public void Too_long_pattern_is_rejected()
    {
        Assert.Throws<DomainValidationException>(() => P(new string('A', 38) + "{RRRR}/{NNNN}"));
    }

    [Fact]
    public void Yearly_sequence_restarts_in_new_year()
    {
        var p = P("FV/{RRRR}/{NNNN}");
        var existing = new[] { "FV/2025/0001", "FV/2025/0002", "FV/2025/0003", "KFV/2025/0009", "FV/2026/0001" };
        Assert.Equal(4, p.NextSequence(new DateTime(2025, 12, 31), existing));
        Assert.Equal(2, p.NextSequence(new DateTime(2026, 1, 1), existing));
        Assert.Equal(1, p.NextSequence(new DateTime(2027, 1, 1), existing));
    }

    [Fact]
    public void Monthly_sequence_restarts_each_month()
    {
        var p = P("{NNN}/{MM}/{RR}", NumberReset.Monthly);
        var existing = new[] { "001/01/26", "002/01/26", "001/02/26" };
        Assert.Equal(3, p.NextSequence(new DateTime(2026, 1, 20), existing));
        Assert.Equal(2, p.NextSequence(new DateTime(2026, 2, 3), existing));
        Assert.Equal(1, p.NextSequence(new DateTime(2026, 3, 1), existing));
        Assert.Equal(1, p.NextSequence(new DateTime(2027, 1, 1), existing)); // ten sam miesiąc w innym roku zaczyna od 1
    }

    [Fact]
    public void Never_reset_continues_across_years()
    {
        var p = P("FV/{RRRR}/{NNNN}", NumberReset.Never);
        Assert.Equal(6, p.NextSequence(new DateTime(2027, 1, 1), ["FV/2025/0004", "FV/2026/0005"]));
    }

    [Fact]
    public void Number_grows_beyond_padding_width()
    {
        var p = P("FV/{RRRR}/{NN}");
        Assert.Equal("FV/2026/100", p.Format(new DateTime(2026, 1, 1), 100));
        Assert.Equal(101, p.NextSequence(new DateTime(2026, 5, 1), ["FV/2026/100"]));
    }

    [Fact]
    public void Literal_characters_are_not_regex()
    {
        var p = P("A.B/{RRRR}/{NNN}");
        Assert.Equal(1, p.NextSequence(new DateTime(2026, 1, 1), ["AxB/2026/009"]));
        Assert.Equal(10, p.NextSequence(new DateTime(2026, 1, 1), ["A.B/2026/009"]));
    }

    [Fact]
    public void Company_numbering_defaults_to_current_format_and_validates_changes()
    {
        var c = new Company("X");
        Assert.Equal("FV/{RRRR}/{NNNN}", c.SalesNumberPattern);
        Assert.Equal(NumberReset.Yearly, c.SalesNumberReset);
        Assert.Throws<DomainValidationException>(() => c.SetNumbering("bez numeru", NumberReset.Never));
        Assert.Equal("FV/{RRRR}/{NNNN}", c.SalesNumberPattern); // nieprawidłowy wzór niczego nie zmienia
        c.SetNumbering("{NNN}/{MM}/{RR}", NumberReset.Monthly);
        Assert.Equal("{NNN}/{MM}/{RR}", c.SalesNumberPattern);
    }
}
