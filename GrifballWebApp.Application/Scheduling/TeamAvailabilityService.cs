using GrifballWebApp.Application.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Scheduling;

/// <summary>One of a season's timeslots for a team: whether it's the team's, and how many of its roster can play it.</summary>
public sealed record TeamSlot(int AvailabilityOptionID, DayOfWeek DayOfWeek, TimeOnly Time, bool Available, int PlayersAvailable);

/// <summary>One of a season's weekly timeslots.</summary>
internal sealed record SeasonSlot(int AvailabilityOptionID, DayOfWeek DayOfWeek, TimeOnly Time);

/// <summary>
/// A team's timeslots, each of its season's: its captain's own (<see cref="SetByCaptain"/>), else its roster's (a slot is
/// the team's when enough of its players can play it: the season's rule).
/// </summary>
public sealed record TeamSlots(int TeamID, bool SetByCaptain, int RosterSize, IReadOnlyList<TeamSlot> Slots)
{
    /// <summary>The caller may change them: the team's captain, or staff (<see cref="TeamAvailabilityService.MayChangeAsync"/>).</summary>
    public bool CanChange { get; init; }
}

/// <summary>
/// The teams' availability, which scheduling checks a match's time against: worked out from each team's roster, the
/// timeslots its players ticked when they signed up (a slot is the team's when at least the season's
/// <see cref="Domain.League.SeasonSchedulingRules.MinPlayersAvailable"/> of them can play it), unless its captain set the
/// team's own (<see cref="SetAsync"/>), which they can reset back to the roster's (<see cref="ResetAsync"/>).
/// </summary>
public sealed class TeamAvailabilityService(IApplicationDbContext context)
{
    public const string NoSuchTeam = "There's no such team.";
    public const string NotTheCaptain = "Only the team's captain (or staff) can change its availability.";
    public const string NotTheSeasonsSlot = "Choose from the season's own timeslots.";
    public const string NoSlots = "Choose at least one timeslot, or reset to the roster's.";

    /// <summary>Each team of <paramref name="seasonID"/>'s timeslots, by its ID.</summary>
    public async Task<IReadOnlyDictionary<int, TeamSlots>> ForSeasonAsync(int seasonID, CancellationToken ct = default)
        => await ForSeasonAsync(seasonID, await new SeasonSchedulingRulesService(context).ForAsync(seasonID, ct), await SeasonSlotsAsync(seasonID, ct), ct);

    /// <summary><paramref name="seasonID"/>'s weekly timeslots, by day and time.</summary>
    internal Task<List<SeasonSlot>> SeasonSlotsAsync(int seasonID, CancellationToken ct)
        => context.Availability.AsNoTracking()
            .Where(o => o.SeasonAvailability.Any(s => s.SeasonID == seasonID))
            .OrderBy(o => o.DayOfWeek).ThenBy(o => o.Time)
            .Select(o => new SeasonSlot(o.AvailabilityOptionID, o.DayOfWeek, o.Time))
            .ToListAsync(ct);

    /// <summary>The same, with the season's <paramref name="rules"/> and <paramref name="slots"/> loaded already (SchedulingService.LoadAsync's).</summary>
    internal async Task<IReadOnlyDictionary<int, TeamSlots>> ForSeasonAsync(int seasonID, Domain.League.SeasonSchedulingRules rules,
        IReadOnlyList<SeasonSlot> slots, CancellationToken ct)
    {
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.SeasonID == seasonID)
            .Select(t => new
            {
                t.TeamID,
                Players = t.TeamPlayers.Select(p => p.UserID).ToList(),
                Own = t.TeamAvailability.Select(a => a.AvailabilityOptionID).ToList(),
            })
            .ToListAsync(ct);
        var players = teams.SelectMany(t => t.Players).Distinct().ToList();
        // Who can play each slot: the timeslots each rostered player ticked when they signed up for the season
        var ticked = (await context.Set<Domain.League.SignupAvailability>().AsNoTracking()
            .Where(a => a.SeasonSignup.SeasonID == seasonID && players.Contains(a.SeasonSignup.UserID))
            .Select(a => new { a.SeasonSignup.UserID, a.AvailabilityOptionID })
            .ToListAsync(ct))
            .ToLookup(a => a.AvailabilityOptionID, a => a.UserID);

        return teams.ToDictionary(t => t.TeamID, t =>
        {
            var own = t.Own.ToHashSet();
            var slotsOfTeam = slots.Select(s =>
            {
                var playersAvailable = ticked[s.AvailabilityOptionID].Count(t.Players.Contains);
                var available = own.Count > 0 ? own.Contains(s.AvailabilityOptionID) : playersAvailable >= rules.MinPlayersAvailable;
                return new TeamSlot(s.AvailabilityOptionID, s.DayOfWeek, s.Time, available, playersAvailable);
            }).ToList();
            return new TeamSlots(t.TeamID, own.Count > 0, t.Players.Count, slotsOfTeam);
        });
    }

    /// <summary><paramref name="teamID"/>'s timeslots; null when there is no such team.</summary>
    public async Task<TeamSlots?> ForTeamAsync(int teamID, CancellationToken ct = default)
    {
        var seasonID = await context.Teams.AsNoTracking().Where(t => t.TeamID == teamID).Select(t => (int?)t.SeasonID).SingleOrDefaultAsync(ct);
        return seasonID is { } season ? (await ForSeasonAsync(season, ct))[teamID] : null;
    }

    /// <summary>Whether <paramref name="userID"/> may change <paramref name="teamID"/>'s timeslots: its captain, or <paramref name="staff"/>.</summary>
    public async Task<bool> MayChangeAsync(int teamID, int userID, bool staff, CancellationToken ct = default)
        => staff || await context.Teams.AsNoTracking().Where(t => t.TeamID == teamID).AnyAsync(Domain.League.Team.CaptainedBy(userID), ct);

    /// <summary>
    /// <paramref name="userID"/> (the team's captain, or <paramref name="staff"/>) set the team's own timeslots, from its season's,
    /// in place of its roster's. Why not (nothing changed), or null when it was set.
    /// </summary>
    public async Task<string?> SetAsync(int teamID, IReadOnlyCollection<int> availabilityOptionIDs, int userID, bool staff, CancellationToken ct = default)
    {
        var team = await context.Teams.Include(t => t.TeamPlayers).Include(t => t.TeamAvailability).AsSplitQuery().SingleOrDefaultAsync(t => t.TeamID == teamID, ct);
        if (team is null)
            return NoSuchTeam;
        if (!staff && !team.IsCaptainedBy(userID))
            return NotTheCaptain;
        if (availabilityOptionIDs.Count == 0)
            return NoSlots;
        var seasons = await context.Set<Domain.League.SeasonAvailability>()
            .Where(s => s.SeasonID == team.SeasonID)
            .Select(s => s.AvailabilityOptionID)
            .ToListAsync(ct);
        if (availabilityOptionIDs.Any(o => !seasons.Contains(o)))
            return NotTheSeasonsSlot;
        team.SetAvailability(availabilityOptionIDs);
        await context.SaveChangesAsync(ct);
        return null;
    }

    /// <summary><paramref name="userID"/> (the captain, or <paramref name="staff"/>) reset the team's timeslots to its roster's; why not, or null.</summary>
    public async Task<string?> ResetAsync(int teamID, int userID, bool staff, CancellationToken ct = default)
    {
        var team = await context.Teams.Include(t => t.TeamPlayers).Include(t => t.TeamAvailability).AsSplitQuery().SingleOrDefaultAsync(t => t.TeamID == teamID, ct);
        if (team is null)
            return NoSuchTeam;
        if (!staff && !team.IsCaptainedBy(userID))
            return NotTheCaptain;
        team.ClearAvailability();
        await context.SaveChangesAsync(ct);
        return null;
    }
}
