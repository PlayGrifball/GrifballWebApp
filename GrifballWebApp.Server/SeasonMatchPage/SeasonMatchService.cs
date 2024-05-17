using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
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

    public async Task<List<PossibleMatchDto>> GetPossibleMatches(int seasonMatchID, CancellationToken ct = default)
    {
        var homeTeamIDs = _context.SeasonMatches.Where(x => x.SeasonMatchID == seasonMatchID)
            .AsNoTracking().AsSplitQuery()
            .SelectMany(x => x.HomeTeam.TeamPlayers)
            .Where(tp => tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .ToList()!;

        if (homeTeamIDs.Any() is false)
            return Enumerable.Empty<PossibleMatchDto>().ToList();

        var awayTeamIDs = _context.SeasonMatches.Where(x => x.SeasonMatchID == seasonMatchID)
            .AsNoTracking().AsSplitQuery()
            .SelectMany(x => x.AwayTeam.TeamPlayers)
            .Where(tp => tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .ToList();

        if (awayTeamIDs.Any() is false)
            return Enumerable.Empty<PossibleMatchDto>().ToList();

        var allIDs = homeTeamIDs.Union(awayTeamIDs).Distinct().ToList();

        await _dataPullService.DownloadRecentMatchesForPlayers(allIDs);

        var matches = await _context.Matches
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
                        .ThenInclude(x => x.User)
            .Where(x => x.MatchTeams.Any(x => x.MatchParticipants.Any(x => allIDs.Contains(x.XboxUserID))))
            .Where(x => x.MatchTeams.Count == 2)
            .Where(x => x.MatchTeams.All(x => x.MatchParticipants.Count == 4))
            .Take(100)
            .OrderByDescending(x => x.StartTime)
            .AsSplitQuery().AsNoTracking()
            .ToListAsync(ct);

        var possibleMatches = new List<PossibleMatchDto>();
        foreach (Match match in matches)
        {
            var team1 = match.MatchTeams.ElementAt(0);
            var team1HomeTeamCount = team1.MatchParticipants.Where(x => homeTeamIDs.Contains(x.XboxUserID)).Count();
            var team1AwayTeamCount = team1.MatchParticipants.Where(x => awayTeamIDs.Contains(x.XboxUserID)).Count();

            if (team1HomeTeamCount is 0 && team1AwayTeamCount is 0)
                continue;

            var team2 = match.MatchTeams.ElementAt(1);
            var team2HomeTeamCount = team2.MatchParticipants.Where(x => homeTeamIDs.Contains(x.XboxUserID)).Count();
            var team2AwayTeamCount = team2.MatchParticipants.Where(x => awayTeamIDs.Contains(x.XboxUserID)).Count();

            if (team2HomeTeamCount is 0 && team2AwayTeamCount is 0)
                continue;

            if (team1HomeTeamCount > 0 && team1AwayTeamCount > 0)
                continue;

            if (team2HomeTeamCount > 0 && team2AwayTeamCount > 0)
                continue;

            var homeTeam = team1HomeTeamCount > 0 ? team1 : team2;

            var awayTeam = team1HomeTeamCount > 0 ? team2 : team1;

            var possibleMatchDto = new PossibleMatchDto()
            {
                MatchID = match.MatchID,
                HomeTeam = new PossibleTeamDto()
                {
                    TeamID = homeTeam.TeamID,
                    Score = homeTeam.Score,
                    Outcome = homeTeam.Outcome,
                    Players = homeTeam.MatchParticipants.Select(x => new PossiblePlayerDto()
                    {
                        XboxUserID = x.XboxUserID,
                        Gamertag = x.XboxUser.Gamertag,
                        Score = x.Score,
                        Kills = x.Kills,
                        Deaths = x.Deaths,
                        IsOnTeam = homeTeamIDs.Contains(x.XboxUserID),
                    }).ToArray(),
                },
                AwayTeam = new PossibleTeamDto()
                {
                    TeamID = awayTeam.TeamID,
                    Score = awayTeam.Score,
                    Outcome = awayTeam.Outcome,
                    Players = awayTeam.MatchParticipants.Select(x => new PossiblePlayerDto()
                    {
                        XboxUserID = x.XboxUserID,
                        Gamertag = x.XboxUser.Gamertag,
                        Score = x.Score,
                        Kills = x.Kills,
                        Deaths = x.Deaths,
                        IsOnTeam = awayTeamIDs.Contains(x.XboxUserID),
                    }).ToArray(),
                },
            };
            possibleMatches.Add(possibleMatchDto);
        }

        return possibleMatches;
    }

    public async Task ReportMatch(int seasonMatchID, Guid matchID, CancellationToken ct = default)
    {
        await _dataPullService.GetAndSaveMatch(matchID);

        var match = await _context.Matches.Where(m =>  m.MatchID == matchID).FirstOrDefaultAsync(ct);
    }
}
