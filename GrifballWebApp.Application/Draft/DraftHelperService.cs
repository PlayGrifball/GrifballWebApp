using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Draft;

/// <summary>One of a season's weekly timeslots a player ticked when they signed up.</summary>
public sealed record DraftTimeslot(int AvailabilityOptionID, DayOfWeek DayOfWeek, TimeOnly Time);

/// <summary>A player's career across every format: seasons, games, wins, goals and kills a game, and titles.</summary>
public sealed record DraftCareer(int Seasons, int Games, decimal? WinPercent, decimal? GoalsPerGame, decimal? KillsPerGame, int Titles);

/// <summary>A player still to be drafted, with what a captain weighs; <see cref="Career"/> is null for one who never played.</summary>
public sealed record DraftPoolPlayer(int UserID, string Name, string? Region, bool WillCaptain, IReadOnlyList<DraftTimeslot> Timeslots, DraftCareer? Career);

/// <summary>A suggested pick: its score (0 to 1, higher better) and why, in words.</summary>
public sealed record DraftSuggestion(int UserID, string Name, double Score, IReadOnlyList<string> Reasons);

/// <summary>The picks suggested for a team, best first.</summary>
public sealed record DraftSuggestions(int TeamID, string TeamName, string CaptainName, IReadOnlyList<DraftSuggestion> Picks);

/// <summary>
/// The draft helper: the player pool with what a captain weighs (careers, signup timeslots, region), and the picks it suggests
/// for a team, by a fixed score anyone can check (no AI): how the player has played against the rest of the pool
/// (<see cref="PerformanceWeight"/>), how many of the team's short timeslots they fill (<see cref="FitWeight"/>), and whether
/// they share the captain's region (<see cref="RegionWeight"/>).
/// </summary>
public sealed class DraftHelperService(IApplicationDbContext context, LeaderboardsService leaderboards)
{
    public const double PerformanceWeight = 0.5, FitWeight = 0.35, RegionWeight = 0.15;

    /// <summary>The games a career needs for its stats to count; fewer is an unknown (the middle of the pool).</summary>
    public const int MinGames = 5;

    /// <summary>How many picks <see cref="SuggestAsync"/> gives unless asked for another number.</summary>
    public const int DefaultSuggestions = 5;

    private const string UnknownName = "Unknown Spartan";

    /// <summary><paramref name="seasonID"/>'s players still to be drafted (signed up, on no team of the season), by name.</summary>
    public async Task<IReadOnlyList<DraftPoolPlayer>> PoolAsync(int seasonID, CancellationToken ct = default)
    {
        var rostered = context.Teams.Rosters().Where(p => p.Team.SeasonID == seasonID).Select(p => p.UserID);
        var signups = await context.SeasonSignups.AsNoTracking()
            .Where(s => s.SeasonID == seasonID && !rostered.Contains(s.UserID))
            .Select(s => new
            {
                s.UserID,
                s.WillCaptain,
                Region = s.User.Region != null ? s.User.Region.RegionName : null,
            })
            .ToListAsync(ct);
        var users = signups.Select(s => s.UserID).ToList();
        var names = await Names(users, ct);
        var slots = (await new TeamAvailabilityService(context).SeasonSlotsAsync(seasonID, ct)).ToDictionary(s => s.AvailabilityOptionID);
        var ticked = (await context.Set<SignupAvailability>().AsNoTracking()
            .Where(a => a.SeasonSignup.SeasonID == seasonID && users.Contains(a.SeasonSignup.UserID))
            .Select(a => new { a.SeasonSignup.UserID, a.AvailabilityOptionID })
            .ToListAsync(ct))
            .Where(a => slots.ContainsKey(a.AvailabilityOptionID))
            .ToLookup(a => a.UserID, a => slots[a.AvailabilityOptionID]);
        var careers = await leaderboards.CareersAsync(ct);

        return signups
            .Select(s => new DraftPoolPlayer(s.UserID, names.GetValueOrDefault(s.UserID) ?? UnknownName, s.Region, s.WillCaptain,
                [.. ticked[s.UserID].OrderBy(t => t.DayOfWeek).ThenBy(t => t.Time).Select(t => new DraftTimeslot(t.AvailabilityOptionID, t.DayOfWeek, t.Time))],
                careers.TryGetValue(s.UserID, out var career) && career.Totals.GamesPlayed > 0 ? Career(career) : null))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The picks suggested for <paramref name="teamID"/> (the team on deck when not given), best first, at most
    /// <paramref name="count"/>; null when there is no such team in the season, or none on deck.
    /// </summary>
    public async Task<DraftSuggestions?> SuggestAsync(int seasonID, int? teamID = null, int count = DefaultSuggestions, CancellationToken ct = default)
    {
        var teams = context.Teams.AsNoTracking();
        var team = await (teamID is { } id ? teams.Where(t => t.TeamID == id && t.SeasonID == seasonID) : teams.PickOrder(seasonID))
            .Select(t => new
            {
                t.TeamID,
                t.TeamName,
                t.Captain.UserID,
                CaptainRegion = t.Captain.User.Region != null ? t.Captain.User.Region.RegionName : null,
            })
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;

        var pool = await PoolAsync(seasonID, ct);
        var rules = await new SeasonSchedulingRulesService(context).ForAsync(seasonID, ct);
        var slots = (await new TeamAvailabilityService(context).ForTeamAsync(team.TeamID, ct))?.Slots ?? [];
        var performance = Performance(pool);
        var picks = pool
            .Select(p => Score(p, performance[p.UserID], Fit(p, slots, rules.MinPlayersAvailable), Region(p, team.CaptainRegion)))
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(count, 1))
            .ToList();
        var captain = (await Names([team.UserID], ct)).GetValueOrDefault(team.UserID) ?? UnknownName;
        return new DraftSuggestions(team.TeamID, team.TeamName, captain, picks);
    }

    /// <summary>The name the app shows for each of <paramref name="users"/> (<see cref="User.Named"/>: never their login name).</summary>
    private async Task<Dictionary<int, string?>> Names(IReadOnlyCollection<int> users, CancellationToken ct)
        => await context.Users.AsNoTracking().Where(u => users.Contains(u.Id)).Select(User.Named).ToDictionaryAsync(n => n.UserID, n => n.Name, ct);

    private static DraftCareer Career(PlayerCareerSummary career)
    {
        var t = career.Totals;
        return new DraftCareer(t.SeasonsCount, t.GamesPlayed, t.WinPercent, t.GoalsPerGame, StatMath.PerGame(t.Kills, t.GamesWithStats), career.Titles);
    }

    /// <summary>
    /// Each player's performance (0 to 1) and its reason: the mean of where their win %, goals and kills a game place among the
    /// pool's players with <see cref="MinGames"/> or more (the best 1, the worst 0); a player with fewer is the middle, 0.5.
    /// </summary>
    private static Dictionary<int, (double Value, string Reason)> Performance(IReadOnlyList<DraftPoolPlayer> pool)
    {
        var known = pool.Where(p => p.Career is { Games: >= MinGames }).ToList();
        double Place(DraftPoolPlayer player, Func<DraftCareer, decimal?> stat)
        {
            var mine = stat(player.Career!) ?? 0;
            var others = known.Where(o => o.UserID != player.UserID).Select(o => stat(o.Career!) ?? 0).ToList();
            return others.Count == 0 ? 0.5 : (others.Count(o => o < mine) + 0.5 * others.Count(o => o == mine)) / others.Count;
        }

        return pool.ToDictionary(p => p.UserID, p =>
        {
            if (p.Career is not { Games: >= MinGames } career)
                return (0.5, p.Career is null ? "No recorded games: an unknown" : $"Only {p.Career.Games} recorded games: an unknown");
            var value = (Place(p, c => c.WinPercent) + Place(p, c => c.GoalsPerGame) + Place(p, c => c.KillsPerGame)) / 3;
            var titles = career.Titles switch { 0 => "", 1 => ", a title", _ => $", {career.Titles} titles" };
            return (value, $"Won {career.WinPercent ?? 0:0.#}% of {career.Games} games{titles} (top {Math.Max(1, (int)Math.Round((1 - value) * 100))}% of the pool)");
        });
    }

    /// <summary>
    /// How well the player fits the team's week (0 to 1) and why: the share of the team's timeslots short of the season's players
    /// they can play, else (no slot short) of the team's own slots; a season without timeslots is no help either way (0.5).
    /// </summary>
    private static (double Value, string? Reason) Fit(DraftPoolPlayer player, IReadOnlyList<TeamSlot> slots, int minPlayers)
    {
        if (slots.Count == 0)
            return (0.5, null);
        var theirs = player.Timeslots.Select(t => t.AvailabilityOptionID).ToHashSet();
        var shortOf = slots.Where(s => s.PlayersAvailable < minPlayers).ToList();
        if (shortOf.Count > 0)
        {
            var fills = shortOf.Count(s => theirs.Contains(s.AvailabilityOptionID));
            return ((double)fills / shortOf.Count, $"Can play {fills} of the {shortOf.Count} timeslots the team is short of");
        }
        var teams = slots.Where(s => s.Available).ToList();
        if (teams.Count == 0)
            return (0.5, null);
        var plays = teams.Count(s => theirs.Contains(s.AvailabilityOptionID));
        return ((double)plays / teams.Count, $"Can play {plays} of the team's {teams.Count} timeslots");
    }

    /// <summary>Whether the player shares the captain's region (1, else 0), and why; either unknown is no help either way (0.5).</summary>
    private static (double Value, string? Reason) Region(DraftPoolPlayer player, string? captainRegion)
    {
        if (player.Region is null || captainRegion is null)
            return (0.5, null);
        return player.Region == captainRegion
            ? (1, $"Same region as the captain ({captainRegion})")
            : (0, $"Plays from {player.Region}, the captain from {captainRegion}");
    }

    private static DraftSuggestion Score(DraftPoolPlayer player, (double Value, string Reason) performance, (double Value, string? Reason) fit,
        (double Value, string? Reason) region)
    {
        var score = Math.Round(PerformanceWeight * performance.Value + FitWeight * fit.Value + RegionWeight * region.Value, 4);
        string?[] reasons = [performance.Reason, fit.Reason, region.Reason];
        return new DraftSuggestion(player.UserID, player.Name, score, [.. reasons.OfType<string>()]);
    }
}
