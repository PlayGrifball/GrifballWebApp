using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.MatchDetails;

/// <summary>The match details page: one Halo Infinite match, as pulled from Waypoint, with all its stats.</summary>
public class MatchDetailsService
{
    private readonly GrifballContext _context;

    public MatchDetailsService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The match, or null when it has not been pulled.</summary>
    public async Task<MatchDetailsDto?> GetAsync(Guid matchID, CancellationToken ct = default)
    {
        var match = await _context.Matches
            .Include(m => m.MatchTeams)
                .ThenInclude(t => t.MatchParticipants)
                    .ThenInclude(p => p.XboxUser)
                        .ThenInclude(x => x.User)
            .Include(m => m.MatchTeams)
                .ThenInclude(t => t.MatchParticipants)
                    .ThenInclude(p => p.MedalEarned)
                        .ThenInclude(e => e.Medal)
                            .ThenInclude(m => m.MedalType)
            .Include(m => m.MatchTeams)
                .ThenInclude(t => t.MatchParticipants)
                    .ThenInclude(p => p.MedalEarned)
                        .ThenInclude(e => e.Medal)
                            .ThenInclude(m => m.MedalDifficulty)
            .Include(m => m.SeriesGame)
                .ThenInclude(g => g.SeasonMatch)
                    .ThenInclude(sm => sm.Season)
            .Include(m => m.SeriesGame)
                .ThenInclude(g => g.SeasonMatch)
                    .ThenInclude(sm => sm.HomeTeam)
            .Include(m => m.SeriesGame)
                .ThenInclude(g => g.SeasonMatch)
                    .ThenInclude(sm => sm.AwayTeam)
            .Where(m => m.MatchID == matchID)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (match is null)
            return null;

        var seasonMatch = match.SeriesGame?.SeasonMatch;
        var homeRoster = await RosterAsync(seasonMatch?.HomeTeamID, ct);
        var awayRoster = await RosterAsync(seasonMatch?.AwayTeamID, ct);

        var teams = match.MatchTeams
            .OrderBy(t => t.TeamID)
            .Select(t =>
            {
                var side = SideOf(t, homeRoster, awayRoster);
                return new MatchDetailsTeamDto
                {
                    TeamID = t.TeamID,
                    SeasonTeamID = side switch { Side.Home => seasonMatch!.HomeTeamID, Side.Away => seasonMatch!.AwayTeamID, _ => null },
                    SeasonTeamName = side switch { Side.Home => seasonMatch!.HomeTeam?.TeamName, Side.Away => seasonMatch!.AwayTeam?.TeamName, _ => null },
                    Score = t.Score,
                    Outcome = t.Outcome,
                    Players = t.MatchParticipants
                        .OrderByDescending(p => p.Score)
                        .ThenByDescending(p => p.Kills)
                        .ThenBy(p => p.XboxUser.Gamertag)
                        .Select(ToPlayer)
                        .ToArray(),
                };
            })
            .ToArray();

        return new MatchDetailsDto
        {
            MatchID = match.MatchID,
            StartTime = match.StartTime,
            EndTime = match.EndTime,
            Duration = match.Duration,
            SeasonMatch = seasonMatch is null ? null : new MatchDetailsSeasonMatchDto
            {
                SeasonMatchID = seasonMatch.SeasonMatchID,
                SeasonID = seasonMatch.SeasonID,
                SeasonName = seasonMatch.Season.SeasonName,
                GameNumber = match.SeriesGame!.GameNumber,
                BestOf = seasonMatch.BestOf,
            },
            Teams = teams,
            Medals = Medals(match.MatchTeams.SelectMany(t => t.MatchParticipants).SelectMany(p => p.MedalEarned)),
        };
    }

    /// <summary>The Xbox accounts of a season team's players.</summary>
    private async Task<HashSet<long>> RosterAsync(int? teamID, CancellationToken ct)
    {
        if (teamID is null)
            return [];
        var ids = await _context.TeamPlayers
            .Where(tp => tp.TeamID == teamID && tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    private enum Side { None, Home, Away }

    /// <summary>The season match's side most of the team's players are on; none for a tie (or no season match).</summary>
    private static Side SideOf(MatchTeam team, HashSet<long> home, HashSet<long> away)
    {
        var onHome = team.MatchParticipants.Count(p => home.Contains(p.XboxUserID));
        var onAway = team.MatchParticipants.Count(p => away.Contains(p.XboxUserID));
        if (onHome > onAway)
            return Side.Home;
        if (onAway > onHome)
            return Side.Away;
        return Side.None;
    }

    private static MatchDetailsPlayerDto ToPlayer(MatchParticipant p) => new()
    {
        UserID = p.XboxUser.User?.Id,
        Gamertag = p.XboxUser.Gamertag,
        Score = p.Score,
        PersonalScore = p.PersonalScore,
        Rank = p.Rank,
        Kills = p.Kills,
        Deaths = p.Deaths,
        Assists = p.Assists,
        Kda = p.Kda,
        Suicides = p.Suicides,
        Betrayals = p.Betrayals,
        MeleeKills = p.MeleeKills,
        PowerWeaponKills = p.PowerWeaponKills,
        MaxKillingSpree = p.MaxKillingSpree,
        CalloutAssists = p.CalloutAssists,
        ShotsFired = p.ShotsFired,
        ShotsHit = p.ShotsHit,
        Accuracy = p.Accuracy,
        DamageDealt = p.DamageDealt,
        DamageTaken = p.DamageTaken,
        AverageLife = p.AverageLife,
        TimePlayed = p.TimePlayed,
        Spawns = p.Spawns,
        ObjectivesCompleted = p.ObjectivesCompleted,
        JoinedInProgress = p.JoinedInProgress,
        LeftInProgress = p.LeftInProgress,
        Medals = Medals(p.MedalEarned),
    };

    /// <summary>The medals, one line each with their count summed: most earned first, then Infinite's own order.</summary>
    private static MatchDetailsMedalDto[] Medals(IEnumerable<MedalEarned> earned) => earned
        .GroupBy(e => e.MedalID)
        .Select(g =>
        {
            var medal = g.First().Medal;
            return new
            {
                Weight = medal.SortingWeight,
                Dto = new MatchDetailsMedalDto
                {
                    MedalID = g.Key,
                    Name = medal.MedalName,
                    Description = medal.Description,
                    Type = medal.MedalType.MedalTypeName,
                    Difficulty = medal.MedalDifficulty.MedalDifficultyName,
                    Count = g.Sum(e => e.Count),
                },
            };
        })
        .OrderByDescending(m => m.Dto.Count)
        .ThenByDescending(m => m.Weight)
        .ThenBy(m => m.Dto.Name)
        .Select(m => m.Dto)
        .ToArray();
}
