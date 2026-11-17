using System.Text.RegularExpressions;

namespace GrifballWebApp.Application.Historical;

/// <summary>Gamertag and key helpers shared by the import.</summary>
public static partial class HistoricalKeys
{
    /// <summary>The prefix of a placeholder (dummy) user's UserName: <see cref="global::GrifballWebApp.Domain.Players.User.ImportPlaceholderPrefix"/>.</summary>
    public const string DummyUserNamePrefix = global::GrifballWebApp.Domain.Players.User.ImportPlaceholderPrefix;

    /// <summary>Checks whether a name is an excluded non-team label (header repeats or substitution pools like 'Subs' or 'Team').</summary>
    public static bool IsExcludedTeamName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;
        var clean = CleanGamertag(name);
        return clean.Equals("Team", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Teams", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Sub", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Subs", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Team Name", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Team Names", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Substitute", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Substitutes", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Sub Pool", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Subs Pool", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Free Agent", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Free Agents", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("(Deleted Team)", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Deleted Team", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Trimmed, inner whitespace collapsed to one space: how gamertags are stored on historical rows (the past
    /// gamertags' own rule, <see cref="global::GrifballWebApp.Domain.Players.XboxGamertagHistory.Clean"/>).</summary>
    public static string CleanGamertag(string gamertag) => global::GrifballWebApp.Domain.Players.XboxGamertagHistory.Clean(gamertag);

    /// <summary><see cref="CleanGamertag"/>, upper-cased: what gamertags are compared by
    /// (<see cref="global::GrifballWebApp.Domain.Players.XboxGamertagHistory.Normalize"/>).</summary>
    public static string Normalize(string gamertag) => global::GrifballWebApp.Domain.Players.XboxGamertagHistory.Normalize(gamertag);

    public static string DummyUserName(string gamertag) => DummyUserNamePrefix + CleanGamertag(gamertag);

    /// <summary>"Name#1234" is "Name": the suffix Xbox gives a modern gamertag someone else already has. Cleaned.</summary>
    public static string WithoutSuffix(string gamertag)
    {
        var clean = CleanGamertag(gamertag);
        var match = Suffix().Match(clean);
        return match.Success && match.Index > 0 ? clean[..match.Index].TrimEnd() : clean;
    }

    /// <summary>What two gamertags are compared by when looking for duplicates: <see cref="WithoutSuffix"/>, upper-cased,
    /// letters and digits only ("Mr_Epic Beast" and "Mr Epic Beast" are one).</summary>
    public static string LooseKey(string gamertag)
        => new string(WithoutSuffix(gamertag).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>The shortest name <see cref="StripFraming"/> leaves once the decoration is off; a shorter one keeps it.</summary>
    public const int MinFramedKeyLength = 4;

    /// <summary>
    /// <see cref="LooseKey"/> without decoration: outer symbols ("__Name__", "~Name~") and letter framing whose two
    /// ends mirror each other exactly ("xX Name Xx", "xXXanderXx" is Xander, "ii name ii", "l Name l", "0 Name 0").
    /// One framing letter on each side needs a separator ("x Name x", not "Xerox"); real names whose ends only look
    /// alike keep every letter ("Xander", "Illusion", "Lloyd Hall", "Llama Drill"). Falls back to the plain key when
    /// less than <see cref="MinFramedKeyLength"/> letters would be left.
    /// </summary>
    public static string StripFraming(string gamertag)
    {
        var key = LooseKey(StripMirroredLetters(WithoutSuffix(gamertag).Trim(OuterSymbols).Trim()));
        return key.Length >= MinFramedKeyLength ? key : LooseKey(gamertag);
    }

    private static readonly char[] OuterSymbols = ['_', '-', '~', '*', '.', '='];

    private static bool IsFramingSeparator(char c) => char.IsWhiteSpace(c) || OuterSymbols.Contains(c);

    private static int FramingCategory(char c) => c switch
    {
        'x' or 'X' => 1,
        'i' or 'I' => 2,
        'l' or 'L' => 3,
        'o' or 'O' or '0' => 4,
        _ => 0
    };

    /// <summary>Drops framing letters of one kind from both ends, as many as the shorter end has, when the two ends
    /// mirror each other character for character (case included: "Ll…ll" is no frame).</summary>
    private static string StripMirroredLetters(string text)
    {
        var category = text.Length == 0 ? 0 : FramingCategory(text[0]);
        if (category == 0)
            return text;
        var lead = 0;
        while (lead < text.Length && FramingCategory(text[lead]) == category)
            lead++;
        var trail = 0;
        while (trail < text.Length - lead && FramingCategory(text[^(trail + 1)]) == category)
            trail++;
        // Only as many as the shorter end: in "xXXanderXx" the third X is the name's own letter
        var width = Math.Min(lead, trail);
        if (width == 0)
            return text;
        for (var i = 0; i < width; i++)
        {
            if (text[i] != text[^(i + 1)])
                return text;
        }
        if (width == 1 && !(IsFramingSeparator(text[1]) && IsFramingSeparator(text[^2])))
            return text;
        return text[width..^width];
    }

    /// <summary>Lower case, runs of anything but letters and digits made one hyphen: "Summer League 2021 (Pro)" is
    /// summer-league-2021-pro.</summary>
    public static string Slug(string text) => NonAlphanumeric().Replace(text.Trim().ToLowerInvariant(), "-").Trim('-');

    /// <summary>A historical season's key: the workbook and sheet its stats come from, "2018-sml18-fantasy-rey/Total Stats".
    /// A season name corrected in the archive keeps it, so a re-import finds the same season.</summary>
    public static string SeasonSourceId(string sourceWorkbook, string sourceSheet) => $"{sourceWorkbook.Trim()}/{sourceSheet.Trim()}";

    /// <summary>The key of a season whose entry has no workbook or sheet: "{league slug}/{season name slug}".</summary>
    public static string FallbackSeasonSourceId(string leagueSlug, string seasonName) => $"{leagueSlug}/{Slug(seasonName)}";

    /// <summary>The first year (1990-2099) in a season's name.</summary>
    public static int? Year(string seasonName)
    {
        var match = YearPattern().Match(seasonName);
        return match.Success ? int.Parse(match.Value) : null;
    }

    /// <summary>A match ID, or a Waypoint match URL ending in one.</summary>
    public static Guid? MatchId(string idOrUrl)
    {
        var last = idOrUrl.Trim().TrimEnd('/').Split('/', '?', '#').Reverse()
            .FirstOrDefault(part => Guid.TryParse(part, out _));
        return last is null ? null : Guid.Parse(last);
    }

    /// <summary>A season's format: the archive's when it gives one, otherwise Respawn's (a bracket only) Tournament; every other
    /// season is a league season.</summary>
    public static GrifballWebApp.Domain.League.SeasonFormat Format(string? archiveFormat, string leagueSlug, string? sourceWorkbook, string seasonName)
    {
        if (Enum.TryParse<GrifballWebApp.Domain.League.SeasonFormat>(archiveFormat?.Trim(), ignoreCase: true, out var format) && Enum.IsDefined(format))
            return format;
        if (Slug(sourceWorkbook ?? "").Contains("respawn") || Slug(seasonName).StartsWith("respawn"))
            return GrifballWebApp.Domain.League.SeasonFormat.Tournament;
        return GrifballWebApp.Domain.League.SeasonFormat.LeagueSeason;
    }

    /// <summary>The league (by name or slug) or format the archive files Late League under.</summary>
    public const string LateLeague = "late-league";

    /// <summary>
    /// Late League: matchmaking (random, skill-matched sides), not a season, so imported as a matchmaking season. The archive
    /// says so by the league ("Late League") or the format ("LateLeague"); a season named "Late League ..." is one too.
    /// </summary>
    public static bool IsLateLeague(string? league, string? format, string? seasonName)
        => Slug(league ?? "") == LateLeague || Slug(format ?? "").Replace("-", "") == "lateleague" || Slug(seasonName ?? "").StartsWith(LateLeague);

    /// <summary>The GameVersion slug for a game as the champions record writes it ("MCC Halo Reach: Dash", "Infinite").</summary>
    public static string? GameVersionSlug(string? game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return null;
        var name = game.Trim();
        if (name.StartsWith("MCC ", StringComparison.OrdinalIgnoreCase))
            name = name[4..];
        return Slug(name) switch
        {
            // Result lists name a game short: H3, H4, H5, Reach
            "halo-3" or "halo3" or "h3" => "halo3",
            "halo-reach" or "reach" or "hr" => "reach",
            "halo-reach-dash" => "reach-dash",
            "halo-4" or "halo4" or "h4" => "halo4",
            "halo-5" or "halo5" or "h5" => "halo5",
            "infinite" or "halo-infinite" => "infinite",
            _ => null,
        };
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"(19|20)\d{2}")]
    private static partial Regex YearPattern();

    [GeneratedRegex(@"\s*#\d{1,6}$")]
    private static partial Regex Suffix();
}
