using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LedgerCore.Domain.Exceptions;

namespace LedgerCore.Application.Features.Bank;

public enum BankFormat { Csv, Mt940, Elixir }
public enum CsvProfile { Auto, Mbank, Pko, Ing }

public record ParsedRow(DateTime Date, decimal Amount, string Title, string Counterparty, string CounterpartyAccount);

/// <summary>Wynik parsowania pliku wyciągu. Salda są wypełnione tylko dla formatów, które je zawierają (MT940).</summary>
public record ParsedStatement(List<ParsedRow> Rows, int Skipped, decimal? Opening, decimal? Closing, DateTime? ClosingDate, string? AccountNumber);

/// <summary>Wspólny interfejs parserów wyciągów – nowy format = nowa klasa i wpis w <see cref="BankParsers.For"/>.</summary>
public interface IBankParser
{
    ParsedStatement Parse(string text);
}

public static class BankParsers
{
    public static IBankParser For(BankFormat format, CsvProfile profile, string? ownAccount) => format switch
    {
        BankFormat.Csv => new CsvParser(profile),
        BankFormat.Mt940 => new Mt940Parser(),
        BankFormat.Elixir => new ElixirParser(ownAccount),
        _ => throw new DomainValidationException("Nieznany format wyciągu")
    };

    internal static string Clean(string s) => Regex.Replace(s.Replace(' ', ' '), @"\s+", " ").Trim();

    /// <summary>Same cyfry numeru rachunku; prefiks „PL” i spacje są ignorowane.</summary>
    public static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Daty: 2026-11-05, 05.11.2026, 05-11-2026, 05/11/2026 oraz 20261105.</summary>
    public static DateTime? ParseDate(string s)
    {
        var t = s.Trim();
        var m = Regex.Match(t, @"^(\d{4})-(\d{2})-(\d{2})");
        if (!m.Success) m = Regex.Match(t, @"^(\d{4})(\d{2})(\d{2})$");
        if (m.Success) return Make(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        m = Regex.Match(t, @"^(\d{2})[.\-/](\d{2})[.\-/](\d{4})");
        return m.Success ? Make(m.Groups[3].Value, m.Groups[2].Value, m.Groups[1].Value) : null;
    }

    static DateTime? Make(string y, string mo, string d) =>
        DateTime.TryParseExact($"{y}-{mo}-{d}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;

    /// <summary>Kwota ze znakiem: „-1 234,56”, „1234.50”, „+500,00 PLN”, „(12,00)”. Zwraca null, gdy to nie jest kwota.</summary>
    public static decimal? ParseAmount(string s)
    {
        var t = Regex.Replace(s, @"[\s ]", "");
        t = Regex.Replace(t, "PLN|zł", "", RegexOptions.IgnoreCase);
        if (t.Length == 0) return null;
        var neg = t.StartsWith('-') || (t.StartsWith('(') && t.EndsWith(')'));
        t = t.TrimStart('-', '+', '(').TrimEnd(')');
        if (t.Length == 0 || !t.All(c => char.IsDigit(c) || c is ',' or '.')) return null;
        string intPart, frac;
        var commas = t.Count(c => c == ',');
        var dots = t.Count(c => c == '.');
        char? dec = null;
        if (commas > 0 && dots > 0) dec = t.LastIndexOf(',') > t.LastIndexOf('.') ? ',' : '.';   // ostatni znak to część dziesiętna
        else if (commas == 1) dec = ',';
        else if (dots == 1 && !Regex.IsMatch(t, @"^\d{1,3}\.\d{3}$")) dec = '.';               // „1.234” to tysiące, „12.50” – ułamek
        if (dec is { } d)
        {
            var at = t.LastIndexOf(d);
            intPart = t[..at].Replace(",", "").Replace(".", ""); frac = t[(at + 1)..];
        }
        else { intPart = t.Replace(",", "").Replace(".", ""); frac = ""; }
        if (intPart.Length == 0) intPart = "0";
        if (frac.Length > 2 || !intPart.All(char.IsDigit) || !frac.All(char.IsDigit)) return null;
        var v = decimal.Parse($"{intPart}.{(frac.Length == 0 ? "0" : frac)}", CultureInfo.InvariantCulture);
        return neg ? -v : v;
    }

    internal static List<string> SplitCsvLine(string line, char delim)
    {
        var res = new List<string>();
        var cur = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cur.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == delim) { res.Add(cur.ToString().Trim()); cur.Clear(); }
            else cur.Append(ch);
        }
        res.Add(cur.ToString().Trim());
        return res;
    }
}

/// <summary>
/// CSV: separator ; , lub tabulator wykrywany automatycznie, nagłówek wyszukiwany w pierwszych 40 liniach (eksporty banków mają wstęp
/// z danymi rachunku). Bez nagłówka obowiązuje układ: data, kwota, tytuł, kontrahent. Profile banków zawężają rozpoznawanie kolumn
/// do nagłówków używanych w danym banku – bez nich działa rozpoznawanie ogólne.
/// </summary>
public class CsvParser(CsvProfile profile) : IBankParser
{
    record Profile(string[] Date, string[] Amount, string[] Title, string[] Who, string[] Account);

    static Profile ProfileFor(CsvProfile p) => p switch
    {
        // nagłówki oparte na typowych eksportach historii rachunku; w razie innych nazw wybierz profil „Automatyczny”
        CsvProfile.Mbank => new(["data operacji", "data"], ["kwota"], ["opis operacji", "tytu"], ["nadawca", "odbiorca", "kontrahent"], ["rachunek"]),
        CsvProfile.Pko => new(["data operacji", "data"], ["kwota"], ["opis transakcji", "opis", "tytu"], ["nazwa", "kontrahent"], ["rachunek"]),
        CsvProfile.Ing => new(["data transakcji", "data"], ["kwota transakcji", "kwota"], ["tytu"], ["dane kontrahenta", "kontrahent"], ["nr rachunku", "rachunek"]),
        _ => new(["data operacji", "data transakcji", "data", "date"], ["kwota", "amount", "warto"], ["tytu", "opis", "title", "descr"],
            ["kontrahent", "nadawca", "odbiorca", "counterpart", "nazwa"], ["rachunek", "iban", "account"])
    };

    static int Find(List<string> header, string[] words)
    {
        foreach (var w in words)
        {
            var i = header.FindIndex(h => h.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) return i;
        }
        return -1;
    }

    public ParsedStatement Parse(string text)
    {
        var lines = text.TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0) return new([], 0, null, null, null, null);
        var prof = ProfileFor(profile);

        static char Delim(string line) => new[] { ';', '\t', ',' }.OrderByDescending(d => line.Split(d).Length).First();
        int headerAt = -1; char delim = Delim(lines[0]); List<string> header = [];
        for (var i = 0; i < Math.Min(lines.Count, 40); i++)
        {
            var d = Delim(lines[i]);
            var cols = BankParsers.SplitCsvLine(lines[i], d).Select(c => c.TrimStart('#').Trim().ToLowerInvariant()).ToList();
            if (Find(cols, prof.Date) >= 0 && Find(cols, prof.Amount) >= 0) { headerAt = i; delim = d; header = cols; break; }
        }
        int date = 0, amount = 1, title = 2, who = 3, acc = -1;
        if (headerAt >= 0)
        {
            date = Find(header, prof.Date); amount = Find(header, prof.Amount);
            title = Find(header, prof.Title); who = Find(header, prof.Who); acc = Find(header, prof.Account);
            if (who == title) who = -1;
        }

        var rows = new List<ParsedRow>();
        var skipped = 0;
        foreach (var line in lines.Skip(headerAt >= 0 ? headerAt + 1 : 0))
        {
            var c = BankParsers.SplitCsvLine(line, delim);
            string At(int i) => i >= 0 && i < c.Count ? c[i] : "";
            var d = BankParsers.ParseDate(At(date));
            var a = BankParsers.ParseAmount(At(amount));
            if (d is null || a is null || a == 0) { skipped++; continue; }
            rows.Add(new(d.Value, a.Value, BankParsers.Clean(At(title)), BankParsers.Clean(At(who)), BankParsers.Digits(At(acc))));
        }
        return new(rows, skipped, null, null, null, null);
    }
}

/// <summary>MT940 (SWIFT): :60F: saldo początkowe, :61: operacja, :86: opis (także strukturalny polski z podpolami ~20…~38), :62F: saldo końcowe.</summary>
public class Mt940Parser : IBankParser
{
    static readonly Regex Line61 = new(@"^(\d{6})(\d{4})?(RC|RD|C|D)[A-Z]?(\d+(?:,\d*)?)N[A-Z0-9]{3}", RegexOptions.Compiled);
    static readonly Regex Balance = new(@"^([CD])(\d{6})([A-Z]{3})(\d+(?:,\d*)?)$", RegexOptions.Compiled);

    static DateTime? Yymmdd(string s)
    {
        if (s.Length != 6 || !int.TryParse(s[..2], out var y) || !int.TryParse(s[2..4], out var m) || !int.TryParse(s[4..], out var d)) return null;
        y += y >= 70 ? 1900 : 2000;
        try { return new DateTime(y, m, d); } catch (ArgumentOutOfRangeException) { return null; }
    }

    static decimal? Amount(string s) => decimal.TryParse(s.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : null;

    public ParsedStatement Parse(string text)
    {
        // pola zaczynają się od „:TAG:” na początku linii, kolejne linie to ciąg dalszy
        var fields = new List<(string Tag, string Value)>();
        foreach (var raw in text.TrimStart('﻿').Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var m = Regex.Match(line, @"^:(\d{2}[A-Z]?):(.*)$");
            if (m.Success) fields.Add((m.Groups[1].Value, m.Groups[2].Value));
            else if (fields.Count > 0 && line.Length > 0 && !line.StartsWith('-') && !line.StartsWith('{') && !line.StartsWith('}'))
                fields[^1] = (fields[^1].Tag, fields[^1].Value + "\n" + line);
        }

        var rows = new List<ParsedRow>();
        string? account = null;
        decimal? opening = null, closing = null;
        DateTime? closingDate = null;
        var skipped = 0;
        ParsedRow? last = null; int lastIndex = -1;

        foreach (var (tag, value) in fields)
        {
            switch (tag)
            {
                case "25":
                    var v = value.Trim();
                    account = v.Contains('/') ? v[(v.IndexOf('/') + 1)..] : v;
                    break;
                case "60F" or "60M":
                    var b = Balance.Match(value.Trim());
                    if (b.Success && opening is null)
                    {
                        opening = Amount(b.Groups[4].Value) * (b.Groups[1].Value == "D" ? -1 : 1);
                    }
                    break;
                case "62F" or "62M":
                    var e = Balance.Match(value.Trim());
                    if (e.Success && (tag == "62F" || closing is null))
                    {
                        closing = Amount(e.Groups[4].Value) * (e.Groups[1].Value == "D" ? -1 : 1);
                        closingDate = Yymmdd(e.Groups[2].Value);
                    }
                    break;
                case "61":
                    var first = value.Split('\n')[0].Trim();
                    var m = Line61.Match(first);
                    var date = m.Success ? Yymmdd(m.Groups[1].Value) : null;
                    var amt = m.Success ? Amount(m.Groups[4].Value) : null;
                    if (!m.Success || date is null || amt is null || amt == 0) { skipped++; last = null; lastIndex = -1; break; }
                    var mark = m.Groups[3].Value;
                    var credit = mark is "C" or "RD"; // RD = storno obciążenia (wpływ), RC = storno uznania (wypływ)
                    last = new ParsedRow(date.Value, credit ? amt.Value : -amt.Value, "", "", "");
                    rows.Add(last); lastIndex = rows.Count - 1;
                    break;
                case "86" when last != null:
                    var (title, who, acc) = Info(value);
                    rows[lastIndex] = last with { Title = title, Counterparty = who, CounterpartyAccount = acc };
                    last = null;
                    break;
            }
        }
        return new(rows, skipped, opening, closing, closingDate, account);
    }

    /// <summary>Pole :86: – strukturalne („020~00PRZELEW~20…~32NAZWA~38IBAN”) lub dowolny tekst.</summary>
    static (string Title, string Who, string Account) Info(string value)
    {
        var flat = value.Replace("\r", "").Replace("\n", "");
        if (!flat.Contains('~')) return (BankParsers.Clean(value.Replace("\n", " ")), "", "");
        var parts = flat.Split('~').Skip(1).Where(p => p.Length >= 2).ToDictionary(p => p[..2], p => p[2..], StringComparer.Ordinal);
        string Get(params string[] keys) => string.Concat(keys.Select(k => parts.TryGetValue(k, out var x) ? x : ""));
        var title = BankParsers.Clean(string.Concat(Get("20", "21", "22", "23", "24", "25", "26", "27", "28", "29"), Get("60", "61", "62", "63")));
        if (title.Length == 0) title = BankParsers.Clean(Get("00"));
        var acc = BankParsers.Digits(Get("38"));
        if (acc.Length == 0) acc = BankParsers.Digits(Get("30", "31"));
        return (title, BankParsers.Clean(Get("32") + " " + Get("33")), acc);
    }
}

/// <summary>
/// Elixir-0: wiersze CSV z polami w cudzysłowach (typ, data RRRRMMDD, kwota w groszach, bank zleceniodawcy, 0, rachunek zleceniodawcy,
/// rachunek odbiorcy, nazwa zleceniodawcy, nazwa odbiorcy, bank odbiorcy, tytuł…). Plik nie mówi, czy to wpływ, czy wypływ –
/// kierunek wynika z porównania rachunku z numerem rachunku firmy.
/// </summary>
public class ElixirParser(string? ownAccount) : IBankParser
{
    public ParsedStatement Parse(string text)
    {
        var own = BankParsers.Digits(ownAccount);
        if (own.Length < 26) throw new DomainValidationException("Dla formatu Elixir-0 podaj numer rachunku firmy (26 cyfr) – na jego podstawie program rozpoznaje wpływy i wypłaty");
        own = own[^26..];
        var rows = new List<ParsedRow>();
        var skipped = 0;
        foreach (var raw in text.TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0))
        {
            var f = BankParsers.SplitCsvLine(raw, ',');
            string At(int i) => i < f.Count ? f[i] : "";
            var date = BankParsers.ParseDate(At(1));
            var grosze = long.TryParse(At(2), out var g) ? g : 0;
            var from = BankParsers.Digits(At(5)); var to = BankParsers.Digits(At(6));
            if (At(0) != "110" || date is null || grosze <= 0 || from.Length < 26 || to.Length < 26) { skipped++; continue; }
            from = from[^26..]; to = to[^26..];
            var amount = grosze / 100m;
            var title = BankParsers.Clean(At(10).Replace('|', ' '));
            if (from == own) rows.Add(new(date.Value, -amount, title, BankParsers.Clean(At(8).Replace('|', ' ')), to));
            else if (to == own) rows.Add(new(date.Value, amount, title, BankParsers.Clean(At(7).Replace('|', ' ')), from));
            else skipped++;
        }
        return new(rows, skipped, null, null, null, own);
    }
}
