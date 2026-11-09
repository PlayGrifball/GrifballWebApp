using GrifballWebApp.Database.Queries;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.SeasonMatchPage;

/// <summary>
/// The Xbox accounts on a season match's two rosters (players with one): the one way they are read, for the possible
/// matches, a pull, scoring the series, the box score and the match details page. These are today's rosters, not the
/// rosters on the day a game was played: a player moved off a team since then counts for the team they are on now.
/// </summary>
public sealed record SeriesRosters(IReadOnlySet<long> Home, IReadOnlySet<long> Away)
{
    /// <summary>No rosters: a season match whose teams are not decided yet.</summary>
    public static readonly SeriesRosters None = new(new HashSet<long>(), new HashSet<long>());

    /// <summary>Both teams' rosters, in one query.</summary>
    public static async Task<SeriesRosters> LoadAsync(GrifballContext context, int? homeTeamID, int? awayTeamID, CancellationToken ct = default)
    {
        if (homeTeamID is null && awayTeamID is null)
            return None;
        var players = await context.Teams.Rosters()
            .Where(tp => (tp.TeamID == homeTeamID || tp.TeamID == awayTeamID) && tp.User.XboxUserID != null)
            .Select(tp => new { tp.TeamID, XboxUserID = tp.User.XboxUserID!.Value })
            .ToListAsync(ct);
        return new SeriesRosters(
            players.Where(p => p.TeamID == homeTeamID).Select(p => p.XboxUserID).ToHashSet(),
            players.Where(p => p.TeamID == awayTeamID).Select(p => p.XboxUserID).ToHashSet());
    }

    /// <summary>The season match's rosters; <see cref="None"/> for a match that isn't there.</summary>
    public static async Task<SeriesRosters> LoadAsync(GrifballContext context, int seasonMatchID, CancellationToken ct = default)
    {
        var teams = await context.SeasonMatches.Where(sm => sm.SeasonMatchID == seasonMatchID)
            .Select(sm => new { sm.HomeTeamID, sm.AwayTeamID })
            .FirstOrDefaultAsync(ct);
        return teams is null ? None : await LoadAsync(context, teams.HomeTeamID, teams.AwayTeamID, ct);
    }

    /// <summary>How far a team of a game leans to the home roster: its players on the home roster less those on the away one.</summary>
    public int Lean(IEnumerable<long> xboxUserIDs) => xboxUserIDs.Sum(id => (Home.Contains(id) ? 1 : 0) - (Away.Contains(id) ? 1 : 0));
}

/// <summary>
/// Which team of a Waypoint game was the season match's home side and which its away side: the one rule the box score,
/// the match details page and scoring the series decide it by.
/// </summary>
public static class SeriesSides
{
    /// <summary>
    /// A game of two teams: home is the one leaning more to the home roster (<see cref="SeriesRosters.Lean"/>), away the
    /// other. So a player traded since the game counts against the side they left, and a ringer on neither roster for
    /// neither. Null when that can't decide: not two teams, or both lean alike (nobody rostered, or an even split). What
    /// then is up to the caller: scoring refuses the game, the match page leaves the sides unnamed.
    /// </summary>
    public static (T Home, T Away)? Of<T>(IReadOnlyList<T> teams, Func<T, int> lean)
    {
        if (teams.Count != 2)
            return null;
        var (first, second) = (lean(teams[0]), lean(teams[1]));
        if (first == second)
            return null;
        return first > second ? (teams[0], teams[1]) : (teams[1], teams[0]);
    }
}
