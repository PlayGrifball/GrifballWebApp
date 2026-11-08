using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Stats;
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
            // With identity resolution EF sets the inverse navigations too: GrifballGoals reads a participant's match (its layout)
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (match is null)
            return null;

        var seasonMatch = match.SeriesGame?.SeasonMatch;
        var rosters = await SeriesRosters.LoadAsync(_context, seasonMatch?.HomeTeamID, seasonMatch?.AwayTeamID, ct);
        var sides = Sides(match.MatchTeams, rosters);

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
                        .OrderByDescending(p => GrifballGoals.Infinite(p) ?? -1)
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

    private enum Side { None, Home, Away }

    /// <summary>
    /// The season match's side for each Infinite team, by <see cref="SeriesSides"/>: the rule the box score and scoring
    /// the series decide it by. One it can't decide leaves both teams unnamed.
    /// </summary>
    private static Dictionary<int, Side> Sides(IEnumerable<MatchTeam> teams, SeriesRosters rosters)
    {
        var sides = new Dictionary<int, Side>();
        if (SeriesSides.Of(teams.ToList(), t => rosters.Lean(t.MatchParticipants.Select(p => p.XboxUserID))) is { } decided)
        {
            sides[decided.Home.TeamID] = Side.Home;
            sides[decided.Away.TeamID] = Side.Away;
        }
        return sides;
    }

    private static MatchDetailsPlayerDto ToPlayer(MatchParticipant p) => new()
    {
        UserID = p.XboxUser.User?.Id,
        Gamertag = p.XboxUser.Gamertag,
        Goals = GrifballGoals.Infinite(p),
        // As PlayerGameStatsQuery: a mode that packs its counts into the scores (#217) has no points to show
        PersonalScore = p.MatchTeam.Match.ScoreLayout is null or CustomScoreLayout.Plain ? p.PersonalScore : null,
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
