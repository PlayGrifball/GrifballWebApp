using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Models.HaloInfinite;

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
            var teamsDto = GetTeams(match, homeTeamIDs: homeTeamIDs, awayTeamIDs: awayTeamIDs);

            if (teamsDto is null)
                continue;

            var homeTeam = teamsDto.HomeTeam;
            var awayTeam = teamsDto.AwayTeam;

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

        var transaction = await _context.Database.BeginTransactionAsync(ct);

        var match = await _context.Matches.Where(m => m.MatchID == matchID)
            .Include(m => m.MatchLink)
            .Include(m => m.MatchTeams)
                .ThenInclude(m => m.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
            .FirstOrDefaultAsync(ct);

        if (match is null)
            throw new Exception("Match does not exist");

        if (match.MatchLink is not null)
            throw new Exception("Match is already associated with a season match");

        if (match.MatchTeams.Any(x => x.Outcome is Outcomes.Won) is false)
            throw new Exception("There must be a winner for the match you are reporting");

        var seasonMatch = await _context.SeasonMatches
            .Include(sm => sm.HomeTeam.TeamPlayers)
                .ThenInclude(x => x.User.XboxUser)
            .Include(sm => sm.AwayTeam.TeamPlayers)
                .ThenInclude(x => x.User.XboxUser)
            .Include(sm => sm.MatchLinks)
                .ThenInclude(ml => ml.Match)
            .Where(sm => sm.SeasonMatchID == seasonMatchID)
            .FirstOrDefaultAsync(ct);

        if (seasonMatch is null)
            throw new Exception("Season match does not exist");

        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            throw new Exception("You cannot report a match for a season match that does not have both teams assigned");

        if (seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null)
            throw new Exception("Results for this match have already been decided");

        var homeTeamIDs = seasonMatch.HomeTeam.TeamPlayers.Select(x => x.User.XboxUser.XboxUserID).ToList();
        var awayTeamIDs = seasonMatch.AwayTeam.TeamPlayers.Select(x => x.User.XboxUser.XboxUserID).ToList();

        seasonMatch.MatchLinks.Add(new MatchLink()
        {
            Match = match,
        });

        if (seasonMatch.MatchLinks.Count > seasonMatch.BestOf)
            throw new Exception("Cannot go over max number of matches. Something is wrong");

        var orderedMatchLinks = seasonMatch.MatchLinks
            .OrderBy(x => x.Match.StartTime)
        .ToList();

        seasonMatch.HomeTeamScore = 0;
        seasonMatch.AwayTeamScore = 0;

        foreach (var (index, matchLink) in orderedMatchLinks.Select((matchLink, index) => (index, matchLink)))
        {
            matchLink.MatchNumber = index + 1;

            var teams = GetTeams(matchLink.Match, homeTeamIDs: homeTeamIDs, awayTeamIDs: awayTeamIDs);

            if (teams is null)
                throw new Exception("Unable to verify teams");

            if (teams.HomeTeam.Outcome is Outcomes.Won)
                seasonMatch.HomeTeamScore++;

            if (teams.AwayTeam.Outcome is Outcomes.Won)
                seasonMatch.AwayTeamScore++;
        }

        var winsRequired = Math.Ceiling(seasonMatch.BestOf * 0.5f);

        if (seasonMatch.HomeTeamScore >= winsRequired)
        {
            seasonMatch.HomeTeamResult = SeasonMatchResult.Won;
            seasonMatch.AwayTeamResult = SeasonMatchResult.Loss;
        }
        else if (seasonMatch.AwayTeamScore >= winsRequired)
        {
            seasonMatch.HomeTeamResult = SeasonMatchResult.Loss;
            seasonMatch.AwayTeamResult = SeasonMatchResult.Won;
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private TeamsDto? GetTeams(Match match, List<long> homeTeamIDs, List<long> awayTeamIDs)
    {
        var team1 = match.MatchTeams.ElementAt(0);
        var team1HomeTeamCount = team1.MatchParticipants.Where(x => homeTeamIDs.Contains(x.XboxUserID)).Count();
        var team1AwayTeamCount = team1.MatchParticipants.Where(x => awayTeamIDs.Contains(x.XboxUserID)).Count();

        if (team1HomeTeamCount is 0 && team1AwayTeamCount is 0)
            return null;

        var team2 = match.MatchTeams.ElementAt(1);
        var team2HomeTeamCount = team2.MatchParticipants.Where(x => homeTeamIDs.Contains(x.XboxUserID)).Count();
        var team2AwayTeamCount = team2.MatchParticipants.Where(x => awayTeamIDs.Contains(x.XboxUserID)).Count();

        if (team2HomeTeamCount is 0 && team2AwayTeamCount is 0)
            return null;

        if (team1HomeTeamCount > 0 && team1AwayTeamCount > 0)
            return null;

        if (team2HomeTeamCount > 0 && team2AwayTeamCount > 0)
            return null;

        var homeTeam = team1HomeTeamCount > 0 ? team1 : team2;

        var awayTeam = team1HomeTeamCount > 0 ? team2 : team1;

        return new TeamsDto()
        {
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
        };
    }

    private class TeamsDto
    {
        public required MatchTeam HomeTeam { get; set; }
        public required MatchTeam AwayTeam { get; set; }
    }
}
