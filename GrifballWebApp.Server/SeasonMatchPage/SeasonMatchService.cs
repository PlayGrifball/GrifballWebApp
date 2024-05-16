using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.SeasonMatchPage;

public class SeasonMatchService
{
    private readonly GrifballContext _context;
    private readonly DataPullService _dataPullService;
    public SeasonMatchService(GrifballContext context, DataPullService dataPullService)
    {
        _context = context;
        _dataPullService = dataPullService;
    }

    public async Task<SeasonMatchPageDto?> GetSeasonMatchPage(int seasonMatchID, CancellationToken ct = default)
    {
        var seasonMatch = await _context.SeasonMatches
            .Include(x => x.MatchLinks)
            .Include(x => x.Season)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Include(x => x.BracketMatch)
            .Where(sm => sm.SeasonMatchID == seasonMatchID)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (seasonMatch is null)
            return null;

        return new SeasonMatchPageDto()
        {
            SeasonID = seasonMatch.SeasonID,
            SeasonName = seasonMatch.Season.SeasonName,
            IsPlayoff = seasonMatch.BracketMatch is not null,
            HomeTeamName = seasonMatch.HomeTeam?.TeamName,
            HomeTeamID = seasonMatch.HomeTeam?.TeamID,
            HomeTeamScore = seasonMatch.HomeTeamScore,
            AwayTeamName = seasonMatch.AwayTeam?.TeamName,
            AwayTeamID = seasonMatch.AwayTeam?.TeamID,
            AwayTeamScore = seasonMatch.AwayTeamScore,
            ScheduledTime = seasonMatch.ScheduledTime,
            BestOf = seasonMatch.BestOf,
            ReportedGames = seasonMatch.MatchLinks.Select(x => new ReportedGameDto()
            {
                MatchID = x.MatchID,
                MatchNumber = x.MatchNumber
            }).ToArray(),
        };
    }

    public async Task GetPossibleMatches(int seasonMatchID, CancellationToken ct = default)
    {
        var homeTeamIDs = _context.SeasonMatches.Where(x => x.SeasonMatchID == seasonMatchID)
            .AsNoTracking().AsSplitQuery()
            .SelectMany(x => x.HomeTeam.TeamPlayers)
            .Where(tp => tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .ToList()!;


        var awayTeamIDs = _context.SeasonMatches.Where(x => x.SeasonMatchID == seasonMatchID)
            .AsNoTracking().AsSplitQuery()
            .SelectMany(x => x.AwayTeam.TeamPlayers)
            .Where(tp => tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .ToList();

        var allIDs = homeTeamIDs.Union(awayTeamIDs).Distinct().ToList();

        await _dataPullService.DownloadRecentMatchesForPlayers(allIDs);
    }

    public async Task ReportMatch(int seasonMatchID, Guid matchID, CancellationToken ct = default)
    {
        await GetPossibleMatches(seasonMatchID, ct);

        await _dataPullService.GetAndSaveMatch(matchID);

        var match = await _context.Matches.Where(m =>  m.MatchID == matchID).FirstOrDefaultAsync(ct);

    }
}
