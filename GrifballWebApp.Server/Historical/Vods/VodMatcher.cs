using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical.Vods;

/// <summary>What the matcher knows of a season: its name read like a title, its dates, teams and matches.</summary>
public sealed record VodCatalogSeason(int SeasonID, string Name, DateTime Start, DateTime End, SeasonRef Ref, IReadOnlyList<VodCatalogTeam> Teams, IReadOnlyList<VodCatalogMatch> Matches);

public sealed record VodCatalogTeam(int TeamID, string Name, string Key);

public sealed record VodCatalogMatch(int SeasonMatchID, int? HomeTeamID, int? AwayTeamID, DateTime? ScheduledTime, bool IsPlayoff, bool IsGrandFinal, int? Round);

/// <summary>A title's match: <see cref="VodMatchStatus.Auto"/> when confident, otherwise <see cref="VodMatchStatus.NeedsReview"/>
/// with what it found as the suggestion.</summary>
public sealed record VodMatch(VodMatchStatus Status, int? SeasonID, int? SeasonMatchID, IReadOnlyList<int> TeamIDs, string Note);

/// <summary>
/// Matches a VOD's title to a season, its two teams and their season match, aware of when it was published: a VOD is
/// never of a season that started more than a month after it, and with no season in the title it is one running when
/// it was published. Confident (linked straight away): the season is named or running, and both teams are that
/// season's by name. Anything less is a suggestion for a sysadmin.
/// </summary>
public class VodMatcher
{
    /// <summary>A VOD from before its season's start by more than this is not of it.</summary>
    public static readonly TimeSpan BeforeStart = TimeSpan.FromDays(30);
    /// <summary>A VOD published this long after its season ends can still be of it (a late upload).</summary>
    public static readonly TimeSpan AfterEnd = TimeSpan.FromDays(45);

    private readonly IReadOnlyList<VodCatalogSeason> _seasons;

    public VodMatcher(IReadOnlyList<VodCatalogSeason> seasons)
    {
        _seasons = seasons;
    }

    /// <summary>Every season with its teams and matches.</summary>
    public static async Task<VodMatcher> Load(GrifballContext context, CancellationToken ct)
    {
        var seasons = await context.Seasons.AsNoTracking().Select(s => new { s.SeasonID, s.SeasonName, s.SeasonStart, s.SeasonEnd }).ToListAsync(ct);
        var teams = (await context.Teams.AsNoTracking().Select(t => new { t.TeamID, t.SeasonID, t.TeamName }).ToListAsync(ct)).ToLookup(t => t.SeasonID);
        var matches = (await context.SeasonMatches.AsNoTracking()
            .Select(m => new
            {
                m.SeasonMatchID, m.SeasonID, m.HomeTeamID, m.AwayTeamID, m.ScheduledTime,
                Round = m.BracketMatch == null ? (int?)null : m.BracketMatch.RoundNumber,
                GrandFinal = m.BracketMatch != null && (m.BracketMatch.Bracket == Bracket.GrandFinal || m.BracketMatch.Bracket == Bracket.GrandFinalSuddenDeath),
            })
            .ToListAsync(ct)).ToLookup(m => m.SeasonID);
        return new VodMatcher(seasons.Select(s => new VodCatalogSeason(s.SeasonID, s.SeasonName, s.SeasonStart, s.SeasonEnd, VodTitleParser.ParseSeasonName(s.SeasonName, s.SeasonStart),
            teams[s.SeasonID].Select(t => new VodCatalogTeam(t.TeamID, t.TeamName, VodTitleParser.TeamKey(t.TeamName))).ToList(),
            matches[s.SeasonID].Select(m => new VodCatalogMatch(m.SeasonMatchID, m.HomeTeamID, m.AwayTeamID, m.ScheduledTime, m.Round is not null, m.GrandFinal, m.Round)).ToList())).ToList());
    }

    public VodMatch Match(string title, DateTime publishedAt)
    {
        var info = VodTitleParser.Parse(title);
        var explicitSeason = info.Season.NamesASeason;
        var candidates = Candidates(info.Season, publishedAt);

        if (!info.NamesTeams)
        {
            // A season-only VOD (a draft, a recap) links itself only when the title names exactly one season
            if (explicitSeason && candidates.Count == 1)
                return new VodMatch(VodMatchStatus.Auto, candidates[0].SeasonID, null, [], $"Season named in the title: {candidates[0].Name}");
            return candidates.Count == 1
                ? new VodMatch(VodMatchStatus.NeedsReview, candidates[0].SeasonID, null, [], $"No teams in the title; {(explicitSeason ? "named" : "running")}: {candidates[0].Name}")
                : new VodMatch(VodMatchStatus.NeedsReview, null, null, [], candidates.Count == 0 ? "No season or teams recognised" : $"No teams in the title; {candidates.Count} seasons fit");
        }

        // Each candidate with how well the two sides match its teams (2: by name, 1: close)
        var scored = candidates.Select(season => (Season: season, A: FindTeam(season, info.TeamA!), B: FindTeam(season, info.TeamB!)))
            .Where(s => s.A is not null || s.B is not null)
            .ToList();
        var both = scored.Where(s => s.A is not null && s.B is not null && s.A.Value.Team.TeamID != s.B.Value.Team.TeamID).ToList();
        if (both.Count == 0)
        {
            var onlyOne = scored.Count == 1 ? scored[0] : default;
            if (onlyOne.Season is not null)
            {
                var found = (onlyOne.A ?? onlyOne.B)!.Value.Team;
                return new VodMatch(VodMatchStatus.NeedsReview, onlyOne.Season.SeasonID, null, [found.TeamID],
                    $"Only {found.Name} found in {onlyOne.Season.Name} (title: {info.TeamA} vs {info.TeamB})");
            }
            if (explicitSeason && candidates.Count == 1)
                return new VodMatch(VodMatchStatus.NeedsReview, candidates[0].SeasonID, null, [], $"{info.TeamA} vs {info.TeamB}: not teams of {candidates[0].Name}");
            return new VodMatch(VodMatchStatus.NeedsReview, null, null, [], scored.Count > 1
                ? $"{info.TeamA} vs {info.TeamB}: one of them in each of {scored.Count} seasons"
                : $"{info.TeamA} vs {info.TeamB}: no season has these teams");
        }

        // Same names in several seasons: the one running when it was published
        var inTime = both.Where(s => Running(s.Season, publishedAt)).ToList();
        var chosen = both.Count == 1 ? both[0] : inTime.Count == 1 ? inTime[0] : default;
        if (chosen.Season is null)
        {
            var guess = both.OrderBy(s => Distance(s.Season, publishedAt)).First();
            return new VodMatch(VodMatchStatus.NeedsReview, guess.Season.SeasonID, null, [guess.A!.Value.Team.TeamID, guess.B!.Value.Team.TeamID],
                $"{info.TeamA} vs {info.TeamB} play in {both.Count} seasons; closest in time: {guess.Season.Name}");
        }

        var teamA = chosen.A!.Value;
        var teamB = chosen.B!.Value;
        var teamIds = new[] { teamA.Team.TeamID, teamB.Team.TeamID };
        var (seasonMatch, matchSure, matchNote) = FindMatch(chosen.Season, teamA.Team.TeamID, teamB.Team.TeamID, info, publishedAt);
        var exactNames = teamA.Exact && teamB.Exact;
        var sure = exactNames && matchSure && (explicitSeason || Running(chosen.Season, publishedAt));
        var note = $"{chosen.Season.Name}: {teamA.Team.Name} vs {teamB.Team.Name}"
            + (exactNames ? "" : $" (title: {info.TeamA} vs {info.TeamB})")
            + (Running(chosen.Season, publishedAt) ? "" : "; published outside the season")
            + matchNote;
        return new VodMatch(sure ? VodMatchStatus.Auto : VodMatchStatus.NeedsReview, chosen.Season.SeasonID, seasonMatch, teamIds, note);
    }

    /// <summary>The seasons the title can be of: the one it names (by term and year, the Late League season, narrowed by a
    /// division word), else those running when it was published. Never one that started long after it.</summary>
    private List<VodCatalogSeason> Candidates(SeasonRef title, DateTime publishedAt)
    {
        var possible = _seasons.Where(s => publishedAt >= s.Start - BeforeStart).ToList();
        if (!title.NamesASeason)
            return possible.Where(s => Running(s, publishedAt)).ToList();

        var named = possible.Where(s => title.LateLeagueSeason is { } late
            ? s.Ref.LateLeagueSeason == late
            : s.Ref.Term == title.Term && (title.Year is { } year ? s.Ref.Year == year : s.Ref.Year == publishedAt.Year || s.Ref.Year == publishedAt.Year - 1)).ToList();
        if (title.Qualifiers.Count > 0)
        {
            var qualified = named.Where(s => s.Ref.Qualifiers.Overlaps(title.Qualifiers)).ToList();
            if (qualified.Count > 0)
                named = qualified;
        }
        // "Summer" without a year: this year's or last year's, the one running first
        if (title.Year is null && title.LateLeagueSeason is null && named.Count > 1 && named.Where(s => Running(s, publishedAt)).ToList() is { Count: > 0 } running)
            named = running;
        return named;
    }

    private static bool Running(VodCatalogSeason season, DateTime at) => at >= season.Start - BeforeStart && at <= season.End + AfterEnd;

    private static TimeSpan Distance(VodCatalogSeason season, DateTime at)
        => at < season.Start ? season.Start - at : at > season.End ? at - season.End : TimeSpan.Zero;

    /// <summary>The season's team a side of the title names: by name (exact), else the only one close to it (one name
    /// inside the other, or a letter off).</summary>
    private static (VodCatalogTeam Team, bool Exact)? FindTeam(VodCatalogSeason season, string side)
    {
        var key = VodTitleParser.TeamKey(side);
        if (key.Length == 0)
            return null;
        var exact = season.Teams.Where(t => t.Key == key).ToList();
        if (exact.Count == 1)
            return (exact[0], true);
        var close = season.Teams.Where(t => Close(t.Key, key)).ToList();
        return close.Count == 1 ? (close[0], false) : null;
    }

    private static bool Close(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
            return false;
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        if (shorter.Length >= 5 && longer.Contains(shorter, StringComparison.Ordinal))
            return true;
        return shorter.Length >= 6 && longer.Length - shorter.Length <= 1 && Levenshtein(a, b) <= 1;
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[b.Length];
    }

    /// <summary>The season match of the two teams: the only one, else the one the title's stage, week or the publish date
    /// points to. Not sure when it had to guess. A season without matches (most archive seasons) links no match.</summary>
    private static (int? SeasonMatchID, bool Sure, string Note) FindMatch(VodCatalogSeason season, int teamA, int teamB, VodTitleInfo info, DateTime publishedAt)
    {
        var games = season.Matches.Where(m => (m.HomeTeamID == teamA && m.AwayTeamID == teamB) || (m.HomeTeamID == teamB && m.AwayTeamID == teamA)).ToList();
        if (games.Count == 0)
            return (null, true, "");
        if (games.Count == 1)
            return (games[0].SeasonMatchID, true, "");

        // The title's stage: the grand final, the playoffs or the regular season
        var list = games.Where(m => m.IsGrandFinal).ToList();
        if (!info.IsFinal || list.Count == 0)
        {
            list = games.Where(m => m.IsPlayoff == info.IsPlayoff).ToList();
            if (list.Count == 0)
                list = games;
        }
        if (list.Count == 1)
            return (list[0].SeasonMatchID, true, "");

        if (info.Week is { } week)
        {
            // Week n: the match scheduled closest to the season's start plus n - 1 weeks
            var target = season.Start.AddDays(7 * (week - 1));
            var byWeek = list.Where(m => m.ScheduledTime is not null).OrderBy(m => Math.Abs((m.ScheduledTime!.Value - target).TotalDays)).FirstOrDefault();
            if (byWeek is not null)
                return (byWeek.SeasonMatchID, Math.Abs((byWeek.ScheduledTime!.Value - target).TotalDays) <= 7, $"; week {week}");
        }
        // The latest one played before it was published, within a week of it
        var before = list.Where(m => m.ScheduledTime is not null && m.ScheduledTime <= publishedAt.AddDays(1)).OrderByDescending(m => m.ScheduledTime).FirstOrDefault();
        if (before is not null)
            return (before.SeasonMatchID, publishedAt - before.ScheduledTime!.Value <= TimeSpan.FromDays(7), $"; {list.Count} matches between them, the one before it was published");
        return (null, false, $"; {list.Count} matches between them");
    }
}
