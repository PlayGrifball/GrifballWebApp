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
        var (homeRoster, awayRoster) = await RostersAsync(seasonMatch?.HomeTeamID, seasonMatch?.AwayTeamID, ct);
        var sides = Sides(match.MatchTeams, homeRoster, awayRoster);

        var teams = match.MatchTeams
            .OrderBy(t => t.TeamID)
            .Select(t =>
            {
                var side = sides.GetValueOrDefault(t.TeamID, Side.None);
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

    /// <summary>
    /// The Xbox accounts of both season teams' players, in one query. These are today's rosters, not the rosters
    /// on the day the game was played: fine for naming the sides, but a player moved off a team since then no
    /// longer counts toward its majority.
    /// </summary>
    private async Task<(HashSet<long> Home, HashSet<long> Away)> RostersAsync(int? homeTeamID, int? awayTeamID, CancellationToken ct)
    {
        if (homeTeamID is null && awayTeamID is null)
            return (new HashSet<long>(), new HashSet<long>());
        var players = await _context.TeamPlayers
            .Where(tp => (tp.TeamID == homeTeamID || tp.TeamID == awayTeamID) && tp.User.XboxUserID != null)
            .Select(tp => new { tp.TeamID, XboxUserID = tp.User.XboxUserID!.Value })
            .ToListAsync(ct);
        return (
            players.Where(p => p.TeamID == homeTeamID).Select(p => p.XboxUserID).ToHashSet(),
            players.Where(p => p.TeamID == awayTeamID).Select(p => p.XboxUserID).ToHashSet());
    }

    private enum Side { None, Home, Away }

    /// <summary>
    /// The season match's side for each Infinite team, decided together so two teams never get the same side.
    /// A team's lead is its home players minus its away players. Home goes to the team with the highest positive
    /// lead, Away to the team with the most negative one. A tie for either, or no majority, leaves it unnamed.
    /// </summary>
    private static Dictionary<int, Side> Sides(IEnumerable<MatchTeam> teams, HashSet<long> home, HashSet<long> away)
    {
        var leads = teams
            .Select(t => (t.TeamID, Lead: t.MatchParticipants.Count(p => home.Contains(p.XboxUserID)) - t.MatchParticipants.Count(p => away.Contains(p.XboxUserID))))
            .ToList();
        var sides = new Dictionary<int, Side>();
        var homeTeam = Strongest(leads.Where(l => l.Lead > 0).Select(l => (l.TeamID, Margin: l.Lead)));
        if (homeTeam is int h)
            sides[h] = Side.Home;
        var awayTeam = Strongest(leads.Where(l => l.Lead < 0).Select(l => (l.TeamID, Margin: -l.Lead)));
        if (awayTeam is int a)
            sides[a] = Side.Away;
        return sides;
    }

    /// <summary>The team with the single highest margin; null when there is none or two share it.</summary>
    private static int? Strongest(IEnumerable<(int TeamID, int Margin)> candidates)
    {
        var top = candidates.OrderByDescending(c => c.Margin).Take(2).ToList();
        if (top.Count == 0 || (top.Count == 2 && top[0].Margin == top[1].Margin))
            return null;
        return top[0].TeamID;
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
