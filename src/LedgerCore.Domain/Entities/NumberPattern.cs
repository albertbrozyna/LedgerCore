using System.Text;
using System.Text.RegularExpressions;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Domain.Entities;

/// <summary>Kiedy numeracja faktur sprzedaży zaczyna się od nowa.</summary>
public enum NumberReset { Yearly, Monthly, Never }

/// <summary>
/// Wzór numeru faktury sprzedaży, np. <c>FV/{RRRR}/{NNNN}</c> lub <c>{NNN}/{MM}/{RR}</c>.
/// Znaczniki: <c>{RRRR}</c> rok 4-cyfrowy, <c>{RR}</c> rok 2-cyfrowy, <c>{MM}</c> miesiąc, <c>{N…}</c> numer kolejny
/// (liczba liter N = minimalna liczba cyfr, 1–8). Pozostałe znaki (litery, cyfry, spacja i / - . _ #) są stałe.
/// </summary>
public sealed class NumberPattern
{
    public const string Default = "FV/{RRRR}/{NNNN}";
    public const int MaxLength = 40;

    abstract record Part;
    sealed record Literal(string Text) : Part;
    sealed record Year4 : Part;
    sealed record Year2 : Part;
    sealed record Month : Part;
    sealed record Seq(int Width) : Part;

    readonly List<Part> _parts;
    public string Text { get; }
    public NumberReset Reset { get; }

    NumberPattern(string text, NumberReset reset, List<Part> parts) { Text = text; Reset = reset; _parts = parts; }

    static readonly Regex Token = new(@"\{([^{}]*)\}", RegexOptions.Compiled);
    static readonly Regex AllowedLiteral = new(@"^[\p{L}\p{N} /\-._#]*$", RegexOptions.Compiled);

    public static NumberPattern Parse(string? text, NumberReset reset)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0) throw new DomainValidationException("Wzór numeru faktury jest wymagany");
        if (!Enum.IsDefined(reset)) throw new DomainValidationException("Nieprawidłowy sposób resetowania numeracji");

        var parts = new List<Part>();
        var pos = 0;
        foreach (Match m in Token.Matches(text))
        {
            AddLiteral(parts, text[pos..m.Index]);
            var name = m.Groups[1].Value;
            Part p = name switch
            {
                "RRRR" => new Year4(),
                "RR" => new Year2(),
                "MM" => new Month(),
                _ when name.Length is >= 1 and <= 8 && name.All(c => c == 'N') => new Seq(name.Length),
                _ => throw new DomainValidationException($"Nieznany znacznik {{{name}}} we wzorze numeru. Dozwolone: {{RRRR}}, {{RR}}, {{MM}} oraz {{N}}, {{NN}}, {{NNN}}… (do 8 liter N)")
            };
            parts.Add(p);
            pos = m.Index + m.Length;
        }
        AddLiteral(parts, text[pos..]);

        var seqCount = parts.OfType<Seq>().Count();
        if (seqCount != 1) throw new DomainValidationException("Wzór numeru musi zawierać dokładnie jeden znacznik numeru kolejnego, np. {NNNN}");
        if (parts.OfType<Year4>().Count() + parts.OfType<Year2>().Count() > 1) throw new DomainValidationException("Rok może wystąpić we wzorze numeru tylko raz ({RRRR} albo {RR})");
        if (parts.OfType<Month>().Count() > 1) throw new DomainValidationException("Miesiąc {MM} może wystąpić we wzorze numeru tylko raz");

        var hasYear = parts.Any(p => p is Year4 or Year2);
        var hasMonth = parts.Any(p => p is Month);
        if (reset == NumberReset.Yearly && !hasYear)
            throw new DomainValidationException("Przy numeracji zerowanej co rok wzór musi zawierać rok ({RRRR} lub {RR}) – inaczej numery by się powtarzały");
        if (reset == NumberReset.Monthly && (!hasYear || !hasMonth))
            throw new DomainValidationException("Przy numeracji zerowanej co miesiąc wzór musi zawierać rok i miesiąc ({RRRR}/{RR} oraz {MM}) – inaczej numery by się powtarzały");

        var pattern = new NumberPattern(text, reset, parts);
        if (pattern.Format(new DateTime(2000, 1, 1), 1).Length > MaxLength)
            throw new DomainValidationException($"Numer faktury utworzony z tego wzoru byłby dłuższy niż {MaxLength} znaków");
        return pattern;
    }

    static void AddLiteral(List<Part> parts, string text)
    {
        if (text.Length == 0) return;
        if (text.Contains('{') || text.Contains('}')) throw new DomainValidationException("Nawiasy klamrowe we wzorze numeru muszą otaczać znacznik, np. {RRRR}");
        if (!AllowedLiteral.IsMatch(text)) throw new DomainValidationException("Wzór numeru może zawierać litery, cyfry, spację oraz znaki / - . _ #");
        parts.Add(new Literal(text));
    }

    public string Format(DateTime date, int sequence)
    {
        var sb = new StringBuilder();
        foreach (var p in _parts)
            sb.Append(p switch
            {
                Literal l => l.Text,
                Year4 => date.Year.ToString("0000"),
                Year2 => (date.Year % 100).ToString("00"),
                Month => date.Month.ToString("00"),
                Seq s => sequence.ToString().PadLeft(s.Width, '0'),
                _ => ""
            });
        return sb.ToString();
    }

    /// <summary>Stały początek numeru (do wstępnego zawężenia zapytania), pusty gdy wzór zaczyna się od znacznika.</summary>
    public string LiteralPrefix => _parts.Count > 0 && _parts[0] is Literal l ? l.Text : "";

    /// <summary>
    /// Wyrażenie rozpoznające numery z okresu numeracji daty <paramref name="date"/> (grupa „n” = numer kolejny).
    /// Rok/miesiąc, po których numeracja się zeruje, są dopasowywane do daty; pozostałe znaczniki daty – dowolnie.
    /// </summary>
    public Regex MatcherFor(DateTime date)
    {
        var fixYear = Reset is NumberReset.Yearly or NumberReset.Monthly;
        var fixMonth = Reset == NumberReset.Monthly;
        var sb = new StringBuilder("^");
        foreach (var p in _parts)
            sb.Append(p switch
            {
                Literal l => Regex.Escape(l.Text),
                Year4 => fixYear ? date.Year.ToString("0000") : @"\d{4}",
                Year2 => fixYear ? (date.Year % 100).ToString("00") : @"\d{2}",
                Month => fixMonth ? date.Month.ToString("00") : @"\d{2}",
                Seq => @"(?<n>\d+)",
                _ => ""
            });
        return new Regex(sb.Append('$').ToString(), RegexOptions.CultureInvariant);
    }

    /// <summary>Następny numer kolejny w okresie daty, mając listę dotychczasowych numerów faktur sprzedaży.</summary>
    public int NextSequence(DateTime date, IEnumerable<string> existingNumbers)
    {
        var rx = MatcherFor(date);
        var max = 0;
        foreach (var n in existingNumbers)
        {
            var m = rx.Match(n);
            if (m.Success && int.TryParse(m.Groups["n"].Value, out var v) && v > max) max = v;
        }
        return max + 1;
    }
}
