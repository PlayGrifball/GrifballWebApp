using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Application.Historical.Vods;

public enum SeasonTerm
{
    Winter,
    Spring,
    Summer,
    Fall,
}

/// <summary>What a title (or a season's name) says about the season: "SL24", "Spring League 2026 (Minor)",
/// "Late League S7". All null: it names none.</summary>
public sealed record SeasonRef
{
    public SeasonTerm? Term { get; init; }
    public int? Year { get; init; }
    public int? LateLeagueSeason { get; init; }
    /// <summary>Division words: major, minor, pro, legacy, amateur, rookie, classic.</summary>
    public IReadOnlySet<string> Qualifiers { get; init; } = new HashSet<string>();

    public bool NamesASeason => Term is not null || LateLeagueSeason is not null;
}

/// <summary>What a VOD's title says: its season, week, stage and the two teams.</summary>
public sealed record VodTitleInfo
{
    public required SeasonRef Season { get; init; }
    public int? Week { get; init; }
    /// <summary>Grand final, final, championship.</summary>
    public bool IsFinal { get; init; }
    /// <summary>Playoffs, semifinal, 3rd place match... (a final is one too).</summary>
    public bool IsPlayoff { get; init; }
    public string? TeamA { get; init; }
    public string? TeamB { get; init; }

    public bool NamesTeams => TeamA is not null && TeamB is not null;
}

/// <summary>
/// Reads the league's VOD titles: "SL24 Week 3 - Team A vs Team B", "AGLA Summer Minor Championship | BO7 | Low IQ vs
/// Reach for the Balls", "AGLA: Zero Logic (Rebs) 0:0 Like Clockwork (YLuxe)", "Late League S7 Grand Final". Season names
/// go through the same reading (<see cref="ParseSeasonName"/>) so a title and a season compare like for like.
/// </summary>
public static partial class VodTitleParser
{
    private static readonly string[] QualifierWords = ["major", "minor", "pro", "legacy", "amateur", "rookie", "classic"];

    public static VodTitleInfo Parse(string title)
    {
        var text = Clean(title);
        var (teamA, teamB) = Teams(text);
        var semi = SemiPattern().IsMatch(text);
        var final = !semi && FinalPattern().IsMatch(text);
        return new VodTitleInfo
        {
            Season = Season(text),
            Week = WeekPattern().Match(text) is { Success: true } w ? int.Parse(w.Groups[1].Value, CultureInfo.InvariantCulture) : null,
            IsFinal = final,
            IsPlayoff = final || semi || PlayoffPattern().IsMatch(text),
            TeamA = teamA,
            TeamB = teamB,
        };
    }

    /// <summary>A season's name read like a title; a name without a year takes its start's.</summary>
    public static SeasonRef ParseSeasonName(string name, DateTime start)
    {
        var parsed = Season(Clean(name));
        return parsed.Term is not null && parsed.Year is null ? parsed with { Year = AnyYearPattern().Match(name) is { Success: true } y ? int.Parse(y.Value, CultureInfo.InvariantCulture) : start.Year } : parsed;
    }

    private static SeasonRef Season(string text)
    {
        SeasonTerm? term = null;
        int? year = null;
        if (AbbreviationPattern().Match(text) is { Success: true } abbr)
        {
            term = abbr.Groups["abbr"].Value.ToUpperInvariant() switch
            {
                "WL" => SeasonTerm.Winter,
                "SL" => SeasonTerm.Spring,
                "SML" or "SUL" => SeasonTerm.Summer,
                _ => SeasonTerm.Fall,
            };
            year = 2000 + int.Parse(abbr.Groups["yy"].Value, CultureInfo.InvariantCulture);
        }
        else if (TermPattern().Match(text) is { Success: true } full)
        {
            term = full.Groups["term"].Value.ToLowerInvariant() switch
            {
                "winter" => SeasonTerm.Winter,
                "spring" => SeasonTerm.Spring,
                "summer" => SeasonTerm.Summer,
                _ => SeasonTerm.Fall,
            };
            if (full.Groups["year"].Success)
            {
                var y = int.Parse(full.Groups["year"].Value, CultureInfo.InvariantCulture);
                year = y < 100 ? 2000 + y : y;
            }
        }
        int? late = LatePattern().Match(text) is { Success: true } l ? int.Parse(l.Groups["n"].Value, CultureInfo.InvariantCulture) : null;
        var lower = text.ToLowerInvariant();
        var qualifiers = QualifierWords.Where(q => Regex.IsMatch(lower, $@"\b{q}\b")).ToHashSet();
        return new SeasonRef { Term = late is null ? term : null, Year = late is null ? year : null, LateLeagueSeason = late, Qualifiers = qualifiers };
    }

    /// <summary>The two sides of "A vs B" (or "A 2:1 B"), in the title's part that has them, without seeds, captains or
    /// scores in brackets.</summary>
    private static (string?, string?) Teams(string text)
    {
        foreach (var part in PartSeparator().Split(text))
        {
            var vs = VersusPattern().Split(part, 2);
            if (vs.Length != 2)
            {
                if (ScorePattern().Match(part) is not { Success: true } score)
                    continue;
                vs = [score.Groups[1].Value, score.Groups[2].Value];
            }
            var a = Side(vs[0]);
            var b = Side(vs[1]);
            if (a is not null && b is not null)
                return (a, b);
        }
        return (null, null);
    }

    private static string? Side(string side)
    {
        var cleaned = Bracketed().Replace(side, " ");
        cleaned = SeedPattern().Replace(cleaned, " ");
        cleaned = Spaces().Replace(cleaned, " ").Trim(' ', '.', ',', '-', '!', '?', '"', '\'');
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Curly quotes and dashes as plain ones, one space between words.</summary>
    private static string Clean(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                '‘' or '’' or '´' or '`' => '\'',
                '“' or '”' => '"',
                '–' or '—' => '-',
                _ => c,
            });
        }
        return Spaces().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>A team name's comparable form: letters and digits only, lower case, without a leading "the".</summary>
    public static string TeamKey(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[4..];
        var sb = new StringBuilder(trimmed.Length);
        // Accents apart from their letters (FormD), then only the letters kept
        foreach (var c in trimmed.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"\b(?<abbr>WL|SML|SUL|SL|FL)\s?'?(?<yy>\d{2})(?!\d)'?", RegexOptions.IgnoreCase)]
    private static partial Regex AbbreviationPattern();

    [GeneratedRegex(@"\b(?<term>winter|spring|summer|fall|autumn)\b(?:\s+league)?(?:\s+'?(?<year>(?:19|20)\d{2}|\d{2})\b)?", RegexOptions.IgnoreCase)]
    private static partial Regex TermPattern();

    [GeneratedRegex(@"\blate\s*league\s*(?:s|season\s*)(?<n>\d{1,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex LatePattern();

    [GeneratedRegex(@"(?:19|20)\d{2}")]
    private static partial Regex AnyYearPattern();

    [GeneratedRegex(@"\bw(?:ee)?k\.?\s*(\d{1,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeekPattern();

    [GeneratedRegex(@"\bsemi\s*-?\s*finals?\b|\bquarter\s*-?\s*finals?\b", RegexOptions.IgnoreCase)]
    private static partial Regex SemiPattern();

    [GeneratedRegex(@"\bgrand\s*-?\s*finals?\b|\bfinals?\b|\bchampionship\b", RegexOptions.IgnoreCase)]
    private static partial Regex FinalPattern();

    [GeneratedRegex(@"\bplayoffs?\b|\b(?:3rd|third)\s+place\b|\bbracket\b|\belimination\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlayoffPattern();

    /// <summary>"A | B", "A - B", "A: B" (not the colon of a score "0:0").</summary>
    [GeneratedRegex(@"\s+[|-]\s+|\s*\|\s*|:\s+")]
    private static partial Regex PartSeparator();

    [GeneratedRegex(@"\s+(?:vs?\.?|versus)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex VersusPattern();

    [GeneratedRegex(@"^(.+?)\s+\d{1,2}\s*[:-]\s*\d{1,2}\s+(.+)$")]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"#\d+\b|\bseed\s*\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeedPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
