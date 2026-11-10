using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.MatchPlanner;

public class MatchPlannerService
{
    private readonly IApplicationDbContext _grifballContext;
    public MatchPlannerService(IApplicationDbContext grifballContext)
    {
        _grifballContext = grifballContext;
    }

    public async Task CreateSeasonMatches(int seasonID, int homeMatchesPerTeam, int bestOf, CancellationToken ct = default)
    {
        using var t = await _grifballContext.Database.BeginTransactionAsync(ct);
        var regularSeason = _grifballContext.SeasonMatches.Where(x => x.SeasonID == seasonID && x.BracketMatch == null);
        await _grifballContext.RecordedGames.DeleteManualForAsync(regularSeason, ct);
        var deletedCount = await regularSeason.ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            // TODO add guard against user deleting unless they approve
        }

        var teams = await _grifballContext.Teams.Where(t => t.SeasonID == seasonID).ToListAsync(ct);

        var newMatches = new List<SeasonMatch>();

        // Each team should have 1 home game against the other teams
        foreach (var homeTeam in teams)
        {
            for (int i = 1; i <= homeMatchesPerTeam; i++)
            {
                foreach (var awayTeam in teams.Where(team => team != homeTeam))
                {
                    var newMatch = SeasonMatch.Between(seasonID, homeTeam.TeamID, awayTeam.TeamID, bestOf);
                    newMatches.Add(newMatch);
                }
            }
        }


        _grifballContext.SeasonMatches.AddRange(newMatches);
        await _grifballContext.SaveChangesAsync(ct);

        await t.CommitAsync(ct);
    }

    public async Task<List<UnscheduledMatchDto>> GetUnscheduledMatches(int seasonID, CancellationToken ct = default)
    {
        var seasonMatches = await _grifballContext.SeasonMatches.AsSplitQuery().AsNoTracking()
            .Include(x => x.HomeTeam.Captain.User.XboxUser)
            .Include(x => x.AwayTeam.Captain.User.XboxUser)
            .Include(x => x.HomeTeam.Captain.User.DiscordUser)
            .Include(x => x.AwayTeam.Captain.User.DiscordUser)
            .Where(sm => sm.SeasonID == seasonID && sm.BracketMatch == null && sm.ScheduledTime == null)
            .ToListAsync(ct);

        var ordered = seasonMatches
            .OrderBy(x => TeamLabel(x.HomeTeam))
            .ThenBy(x => TeamLabel(x.AwayTeam))
            .Select(x => new UnscheduledMatchDto()
            {
                SeasonMatchID = x.SeasonMatchID,
                HomeCaptain = CaptainName(x.HomeTeam),
                AwayCaptain = CaptainName(x.AwayTeam),
                HomeTeam = TeamLabel(x.HomeTeam),
                AwayTeam = TeamLabel(x.AwayTeam),
                Complete = x.IsDecided,
            });
        return ordered.ToList();
    }

    public async Task<List<ScheduledMatchDto>> GetScheduledMatches(int seasonID, CancellationToken ct = default)
    {
        var seasonMatches = await _grifballContext.SeasonMatches.AsSplitQuery().AsNoTracking()
            .Include(x => x.HomeTeam.Captain.User.XboxUser)
            .Include(x => x.AwayTeam.Captain.User.XboxUser)
            .Include(x => x.HomeTeam.Captain.User.DiscordUser)
            .Include(x => x.AwayTeam.Captain.User.DiscordUser)
            .Where(sm => sm.SeasonID == seasonID && sm.BracketMatch == null && sm.ScheduledTime != null)
            .ToListAsync(ct);

        var ordered = seasonMatches
            .OrderBy(x => x.ScheduledTime)
            .Select(x => new ScheduledMatchDto()
            {
                SeasonMatchID = x.SeasonMatchID,
                HomeCaptain = CaptainName(x.HomeTeam),
                AwayCaptain = CaptainName(x.AwayTeam),
                HomeTeam = TeamLabel(x.HomeTeam),
                AwayTeam = TeamLabel(x.AwayTeam),
                Complete = x.IsDecided,
                Time = x.ScheduledTime ?? throw new Exception("No time on season match")
            });
        return ordered.ToList();
    }

    /// <summary>
    /// What the app calls a captain elsewhere (TeamService): gamertag, else Discord name, display name or user name. A captain
    /// without an Xbox account used to make the whole list fail.
    /// </summary>
    private static string CaptainName(Team team)
    {
        var user = team.Captain?.User;
        return user?.XboxUser?.Gamertag ?? user?.DiscordUser?.DiscordUsername ?? user?.DisplayName ?? user?.UserName ?? "";
    }

    /// <summary>A team as the schedule shows it: "Asking Alexandra (Hells Angel cx)"; a team without a captain (most imported
    /// seasons) or named after its captain is its name alone.</summary>
    private static string TeamLabel(Team team)
    {
        var captain = CaptainName(team);
        return captain.Length == 0 || team.TeamName.Contains(captain, StringComparison.OrdinalIgnoreCase) ? team.TeamName : $"{team.TeamName} ({captain})";
    }

    public async Task UpdateMatchTime(UpdateMatchTimeDto dto, CancellationToken ct = default)
    {
        var seasonMatch = await _grifballContext.SeasonMatches.Where(x => x.SeasonMatchID == dto.SeasonMatchID).FirstOrDefaultAsync(ct);

        if (seasonMatch is null)
            return;

        seasonMatch.ScheduleAt(dto.Time);

        await _grifballContext.SaveChangesAsync(ct);
    }
}
