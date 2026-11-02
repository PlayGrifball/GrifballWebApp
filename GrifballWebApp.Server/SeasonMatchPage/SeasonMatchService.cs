using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.Reschedule;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Server.SeasonMatchPage;

public class SeasonMatchService
{
    private readonly GrifballContext _context;
    private readonly IDataPullService _dataPullService;
    private readonly IBracketService _bracketService;
    private readonly ChampionshipService _championshipService;
    public SeasonMatchService(GrifballContext context, IDataPullService dataPullService, IBracketService bracketService, ChampionshipService championshipService)
    {
        _context = context;
        _dataPullService = dataPullService;
        _bracketService = bracketService;
        _championshipService = championshipService;
    }

    /// <summary>A bracket match's result can decide (or, undone, undecide) the season's champion: its title follows,
    /// in the same transaction.</summary>
    private async Task AwardChampionship(SeasonMatch seasonMatch, CancellationToken ct)
    {
        if (seasonMatch.BracketMatch is not null)
            await _championshipService.AwardFromBracketAsync(seasonMatch.SeasonID, ct);
    }

    public Task<SeasonMatchPageDto?> GetSeasonMatchPage(int seasonMatchID, CancellationToken ct = default)
        => GetSeasonMatchPage(seasonMatchID, null, ct);

    /// <param name="userID">The signed-in user, for <see cref="SeasonMatchPageDto.CanRequestReschedule"/>; null for a visitor.</param>
    public async Task<SeasonMatchPageDto?> GetSeasonMatchPage(int seasonMatchID, int? userID, CancellationToken ct)
    {
        var seasonMatch = await _context.SeasonMatches
            .Include(x => x.SeriesGames)
            .Include(x => x.Season)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            
            .Include(x => x.BracketMatch)
                .ThenInclude(x => x.HomeTeamPreviousMatchBracketInfo)
            .Include(x => x.BracketMatch)
                .ThenInclude(x => x.AwayTeamPreviousMatchBracketInfo)
            .Include(x => x.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo)
                .ThenInclude(x => x.SeasonMatch)
            .Include(x => x.BracketMatch.InverseAwayTeamNextMatchBracketInfo)
                .ThenInclude(x => x.SeasonMatch)
            .Where(sm => sm.SeasonMatchID == seasonMatchID)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (seasonMatch is null)
            return null;

        var seriesGames = await _context.SeriesGames
            .Where(sg => sg.SeasonMatchID == seasonMatchID)
            .Include(sg => sg.Match)
                .ThenInclude(m => m!.MatchTeams)
                    .ThenInclude(mt => mt.MatchParticipants)
                        .ThenInclude(mp => mp.XboxUser)
                            .ThenInclude(xu => xu.User)
            .Include(sg => sg.RecordedGame)
                .ThenInclude(rg => rg!.Teams)
                    .ThenInclude(t => t.Participants)
            .Include(sg => sg.RecordedGame)
                .ThenInclude(rg => rg!.Participants)
            .OrderBy(sg => sg.GameNumber)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(ct);

        var homeXboxIDs = seasonMatch.HomeTeamID.HasValue
            ? await _context.TeamPlayers
                .Where(tp => tp.TeamID == seasonMatch.HomeTeamID.Value && tp.User.XboxUserID != null)
                .Select(tp => tp.User.XboxUserID!.Value)
                .ToListAsync(ct)
            : [];

        var awayXboxIDs = seasonMatch.AwayTeamID.HasValue
            ? await _context.TeamPlayers
                .Where(tp => tp.TeamID == seasonMatch.AwayTeamID.Value && tp.User.XboxUserID != null)
                .Select(tp => tp.User.XboxUserID!.Value)
                .ToListAsync(ct)
            : [];

        var reportedGames = seriesGames
            .Select(sg => MapReportedGame(sg, seasonMatch.HomeTeamID, seasonMatch.AwayTeamID, homeXboxIDs, awayXboxIDs))
            .ToArray();

        var seriesTotals = reportedGames.Length > 0 ? BuildSeriesTotals(reportedGames) : null;

        NextMatchesDto? nextMatch = null;
        if (seasonMatch.BracketMatch is not null)
        {
            nextMatch = _bracketService.DetermineNextMatches(seasonMatch);
        }

        return new SeasonMatchPageDto()
        {
            SeasonID = seasonMatch.SeasonID,
            SeasonName = seasonMatch.Season.SeasonName,
            IsPlayoff = seasonMatch.BracketMatch is not null,
            HomeTeamName = seasonMatch.HomeTeam?.TeamName,
            HomeTeamID = seasonMatch.HomeTeam?.TeamID,
            HomeTeamScore = seasonMatch.HomeTeamScore,
            HomeTeamResult = MapToResult(seasonMatch.HomeTeamResult),
            AwayTeamName = seasonMatch.AwayTeam?.TeamName,
            AwayTeamID = seasonMatch.AwayTeam?.TeamID,
            AwayTeamScore = seasonMatch.AwayTeamScore,
            AwayTeamResult = MapToResult(seasonMatch.AwayTeamResult),
            ScheduledTime = seasonMatch.ScheduledTime,
            BestOf = seasonMatch.BestOf,
            ReportedGames = reportedGames,
            SeriesTotals = seriesTotals,
            BracketInfo = seasonMatch.BracketMatch is null ? null :
            new BracketInfoDto()
            {
                HomeTeamSeed = seasonMatch.BracketMatch.HomeTeamSeedNumber,
                AwayTeamSeed = seasonMatch.BracketMatch.AwayTeamSeedNumber,
                HomeTeamPreviousMatchID = seasonMatch.BracketMatch.HomeTeamPreviousMatchBracketInfo?.SeasonMatchID,
                AwayTeamPreviousMatchID = seasonMatch.BracketMatch.AwayTeamPreviousMatchBracketInfo?.SeasonMatchID,
                WinnerNextMatchID = nextMatch?.Winner?.Game.SeasonMatchID,
                LoserNextMatchID = nextMatch?.Loser?.Game.SeasonMatchID,
                IsGrandFinal = seasonMatch.BracketMatch.Bracket is Bracket.GrandFinal or Bracket.GrandFinalSuddenDeath,
            },
            ActiveRescheduleRequestId = seasonMatch.ActiveRescheduleRequestId,
            CanRequestReschedule = await RescheduleRules.CanRequestAsync(_context, seasonMatch, userID, ct),
        };
    }

    private SeasonMatchResultDto MapToResult(SeasonMatchResult? r)
    {
        return r switch
        {
            null => SeasonMatchResultDto.TBD,
            SeasonMatchResult.Won => SeasonMatchResultDto.Won,
            SeasonMatchResult.Loss => SeasonMatchResultDto.Loss,
            SeasonMatchResult.Forfeit => SeasonMatchResultDto.Forfeit,
            SeasonMatchResult.Bye => SeasonMatchResultDto.Bye,
            _ => throw new ArgumentOutOfRangeException(nameof(r), r, "Season match result out of range"),
        };
    }

    public Task<bool> Exists(int seasonMatchID, CancellationToken ct = default)
        => _context.SeasonMatches.AnyAsync(x => x.SeasonMatchID == seasonMatchID, ct);

    /// <summary>The Xbox accounts of both teams' players (those who have one): whose recent games a pull looks through.</summary>
    public async Task<List<long>> PlayerXboxIDs(int seasonMatchID, CancellationToken ct = default)
    {
        var teams = _context.SeasonMatches.Where(x => x.SeasonMatchID == seasonMatchID);
        var players = teams.SelectMany(x => x.HomeTeam.TeamPlayers).Concat(teams.SelectMany(x => x.AwayTeam.TeamPlayers));
        return await players
            .Where(tp => tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// The games both teams' players have played together that are in the database (no Halo call: their recent games are
    /// pulled by <see cref="SeasonMatchPullJob"/>, from this page, and <see cref="ActiveSeasonPullJob"/>, on a schedule).
    /// </summary>
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

        var matches = await _context.Matches
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
                        .ThenInclude(x => x.User)
            .Where(x => x.MatchTeams.Any(x => x.MatchParticipants.Any(x => allIDs.Contains(x.XboxUserID))))
            .Where(x => x.MatchTeams.Count == 2)
            .Where(x => x.MatchTeams.All(x => x.MatchParticipants.Count == 4))
            .Where(x => x.SeriesGame == null)
            .Where(x => x.MatchTeams.Any(x => x.Outcome == Outcomes.Won))
            .OrderByDescending(x => x.StartTime) // The newest 200, not any 200
            .Take(200)
            //.AsSplitQuery() // Include does not work correctly when running as split query, with take 100, and order by desc.
            // Split query causes the include to silently fail. Disabling split query for now but may want to revist this later
            // and write multiple smaller queries to get matches
            .AsNoTracking()
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

    private Task<SeasonMatch?> GetSeasonMatch(int seasonMatchID, CancellationToken ct = default)
    {
        return _context.SeasonMatches
            .Include(sm => sm.HomeTeam.TeamPlayers)
                .ThenInclude(x => x.User.XboxUser)
            .Include(sm => sm.AwayTeam.TeamPlayers)
                .ThenInclude(x => x.User.XboxUser)
            .Include(sm => sm.SeriesGames)
                .ThenInclude(g => g.Match)
                    .ThenInclude(m => m.MatchTeams)
                        .ThenInclude(t => t.MatchParticipants)
            .Include(sm => sm.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo)
                .ThenInclude(x => x.SeasonMatch)
            .Include(sm => sm.BracketMatch.InverseAwayTeamNextMatchBracketInfo)
                .ThenInclude(x => x.SeasonMatch)
            .Where(sm => sm.SeasonMatchID == seasonMatchID)
            .FirstOrDefaultAsync(ct);
    }

    public async Task ReportMatch(int seasonMatchID, Guid matchID, CancellationToken ct = default)
    {
        await _dataPullService.GetAndSaveMatch(matchID);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var match = await _context.Matches.Where(m => m.MatchID == matchID)
            .Include(m => m.SeriesGame)
            .Include(m => m.MatchTeams)
                .ThenInclude(m => m.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
            .FirstOrDefaultAsync(ct);

        if (match is null)
            throw new Exception("Match does not exist");

        if (match.SeriesGame is not null)
            throw new Exception("Match is already associated with a season match");

        if (match.MatchTeams.Any(x => x.Outcome is Outcomes.Won) is false)
            throw new Exception("There must be a winner for the match you are reporting");

        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct);

        if (seasonMatch is null)
            throw new Exception("Season match does not exist");

        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            throw new Exception("You cannot report a match for a season match that does not have both teams assigned");

        if (seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null)
            throw new Exception("Results for this match have already been decided");

        // A rostered player without a gamertag can't have played the match: they are left out, not a 500.
        var homeTeamIDs = seasonMatch.HomeTeam.TeamPlayers.Where(x => x.User.XboxUser is not null).Select(x => x.User.XboxUser.XboxUserID).ToList();
        var awayTeamIDs = seasonMatch.AwayTeam.TeamPlayers.Where(x => x.User.XboxUser is not null).Select(x => x.User.XboxUser.XboxUserID).ToList();

        seasonMatch.SeriesGames.Add(new SeriesGame()
        {
            Match = match,
        });

        if (seasonMatch.SeriesGames.Count > seasonMatch.BestOf)
            throw new Exception("Cannot go over max number of matches. Something is wrong");

        var orderedGames = seasonMatch.SeriesGames
            .OrderBy(x => x.Match!.StartTime)
        .ToList();

        seasonMatch.HomeTeamScore = 0;
        seasonMatch.AwayTeamScore = 0;

        foreach (var (index, game) in orderedGames.Select((game, index) => (index, game)))
        {
            game.GameNumber = index + 1;

            var teams = GetTeams(game.Match!, homeTeamIDs: homeTeamIDs, awayTeamIDs: awayTeamIDs);

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

        if ((seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null) && seasonMatch.BracketMatch is not null)
        {
            HandleSettingNextMatchesInBracket(seasonMatch);
            // A team that lands opposite a BYE goes straight on to its next match
            await _bracketService.AdvanceByesAsync(seasonMatch.SeasonID, ct);
        }

        await _context.SaveChangesAsync(ct);
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    private void HandleSettingNextMatchesInBracket(SeasonMatch seasonMatch)
    {
        if (seasonMatch.BracketMatch.Bracket is Bracket.GrandFinal && seasonMatch.HomeTeamResult is SeasonMatchResult.Won)
        {
            return; // To not proceed to sudden death if the home team wins grand final
        }

        var nextMatches = _bracketService.DetermineNextMatches(seasonMatch);

        if (nextMatches.Winner is not null)
        {
            var winningTeam = seasonMatch switch
            {
                _ when seasonMatch.HomeTeamResult is SeasonMatchResult.Won => seasonMatch.HomeTeamID,
                _ when seasonMatch.AwayTeamResult is SeasonMatchResult.Won => seasonMatch.AwayTeamID,
                _ => throw new Exception("Missing winning team") // TODO: Support byes?
            };
            if (nextMatches.Winner.IsHomeTeam)
            {
                nextMatches.Winner.Game.HomeTeamID = winningTeam;
            }
            else
            {
                nextMatches.Winner.Game.AwayTeamID = winningTeam;
            }
        }

        if (nextMatches.Loser is not null)
        {
            var losingTeam = seasonMatch switch
            {
                _ when seasonMatch.HomeTeamResult is SeasonMatchResult.Loss or SeasonMatchResult.Forfeit => seasonMatch.HomeTeamID,
                _ when seasonMatch.AwayTeamResult is SeasonMatchResult.Loss or SeasonMatchResult.Forfeit => seasonMatch.AwayTeamID,
                _ => throw new Exception("Missing losing team") // TODO: ????
            };
            if (nextMatches.Loser.IsHomeTeam)
            {
                nextMatches.Loser.Game.HomeTeamID = losingTeam;
            }
            else
            {
                nextMatches.Loser.Game.AwayTeamID = losingTeam;
            }
        }
    }

    private ReportedGameDto MapReportedGame(
        SeriesGame sg,
        int? homeTeamID,
        int? awayTeamID,
        List<long> homeXboxIDs,
        List<long> awayXboxIDs)
    {
        var reportedGame = new ReportedGameDto
        {
            MatchID = sg.MatchID,
            RecordedGameID = sg.RecordedGameID,
            MatchNumber = sg.GameNumber
        };

        if (sg.Match != null && sg.Match.MatchTeams.Count >= 2)
        {
            var teams = GetTeams(sg.Match, homeXboxIDs, awayXboxIDs);
            var homeTeam = teams?.HomeTeam ?? sg.Match.MatchTeams.ElementAt(0);
            var awayTeam = teams?.AwayTeam ?? sg.Match.MatchTeams.ElementAt(1);

            reportedGame.HomeScore = homeTeam.Score;
            reportedGame.AwayScore = awayTeam.Score;

            reportedGame.HomePlayers = homeTeam.MatchParticipants
                .Select(MapParticipant)
                .OrderByDescending(p => p.BombScores)
                .ThenByDescending(p => p.Kills)
                .ThenBy(p => p.Deaths)
                .ToList();

            reportedGame.AwayPlayers = awayTeam.MatchParticipants
                .Select(MapParticipant)
                .OrderByDescending(p => p.BombScores)
                .ThenByDescending(p => p.Kills)
                .ThenBy(p => p.Deaths)
                .ToList();
        }
        else if (sg.RecordedGame != null)
        {
            var homeTeam = sg.RecordedGame.Teams.FirstOrDefault(t => (homeTeamID.HasValue && t.TeamID == homeTeamID.Value) || t.Side == 1);
            var awayTeam = sg.RecordedGame.Teams.FirstOrDefault(t => (awayTeamID.HasValue && t.TeamID == awayTeamID.Value) || t.Side == 2);

            reportedGame.HomeScore = homeTeam?.Score;
            reportedGame.AwayScore = awayTeam?.Score;

            var homeParticipants = homeTeam != null
                ? (homeTeam.Participants.Count > 0
                    ? (IEnumerable<RecordedGameParticipant>)homeTeam.Participants
                    : sg.RecordedGame.Participants.Where(p => p.RecordedGameTeamID == homeTeam.RecordedGameTeamID))
                : Enumerable.Empty<RecordedGameParticipant>();

            var awayParticipants = awayTeam != null
                ? (awayTeam.Participants.Count > 0
                    ? (IEnumerable<RecordedGameParticipant>)awayTeam.Participants
                    : sg.RecordedGame.Participants.Where(p => p.RecordedGameTeamID == awayTeam.RecordedGameTeamID))
                : Enumerable.Empty<RecordedGameParticipant>();

            reportedGame.HomePlayers = homeParticipants
                .Select(MapRecordedParticipant)
                .OrderByDescending(p => p.BombScores)
                .ThenByDescending(p => p.Kills)
                .ThenBy(p => p.Deaths)
                .ToList();

            reportedGame.AwayPlayers = awayParticipants
                .Select(MapRecordedParticipant)
                .OrderByDescending(p => p.BombScores)
                .ThenByDescending(p => p.Kills)
                .ThenBy(p => p.Deaths)
                .ToList();
        }

        return reportedGame;
    }

    private static SeasonMatchGameParticipantDto MapParticipant(MatchParticipant p)
    {
        var isCustomScore = p.Score > 20;
        var bombScores = isCustomScore ? p.ObjectivesCompleted : Math.Max(p.Score, p.ObjectivesCompleted);
        return new SeasonMatchGameParticipantDto
        {
            UserID = p.XboxUser?.User?.Id,
            Gamertag = p.XboxUser?.Gamertag ?? "Unknown Spartan",
            Kills = p.Kills,
            Deaths = p.Deaths,
            Assists = p.Assists,
            BombScores = bombScores,
            Score = isCustomScore ? null : (int?)p.PersonalScore
        };
    }

    private static SeasonMatchGameParticipantDto MapRecordedParticipant(RecordedGameParticipant p)
    {
        return new SeasonMatchGameParticipantDto
        {
            UserID = p.UserID,
            Gamertag = p.Gamertag,
            Kills = p.Kills,
            Deaths = p.Deaths,
            Assists = p.Assists,
            BombScores = p.Goals ?? 0,
            Score = p.Score
        };
    }

    private static SeasonMatchSeriesTotalsDto BuildSeriesTotals(IEnumerable<ReportedGameDto> reportedGames)
    {
        static List<SeasonMatchGameParticipantDto> Aggregate(IEnumerable<SeasonMatchGameParticipantDto> players) => players
            .GroupBy(p => (p.UserID, p.Gamertag))
            .Select(g => new SeasonMatchGameParticipantDto
            {
                UserID = g.Key.UserID,
                Gamertag = g.Key.Gamertag,
                Kills = g.Sum(x => x.Kills),
                Deaths = g.Sum(x => x.Deaths),
                Assists = g.Sum(x => x.Assists),
                BombScores = g.Sum(x => x.BombScores),
                Score = g.Any(x => x.Score != null) ? g.Sum(x => x.Score ?? 0) : null,
            })
            .OrderByDescending(p => p.BombScores)
            .ThenByDescending(p => p.Kills)
            .ThenBy(p => p.Deaths)
            .ToList();

        var games = reportedGames.ToList();
        return new SeasonMatchSeriesTotalsDto
        {
            HomePlayers = Aggregate(games.SelectMany(g => g.HomePlayers)),
            AwayPlayers = Aggregate(games.SelectMany(g => g.AwayPlayers)),
        };
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

    public Task HomeForfeit(int seasonMatchID, CancellationToken ct = default) =>
        Forfeit(seasonMatchID, SeasonMatchResult.Forfeit, SeasonMatchResult.Won, ct);

    public Task AwayForfeit(int seasonMatchID, CancellationToken ct = default) =>
        Forfeit(seasonMatchID, SeasonMatchResult.Won, SeasonMatchResult.Forfeit, ct);

    /// <summary>
    /// Both teams forfeit: both take a loss and nobody wins. In a bracket every match this one feeds gets a BYE
    /// in its place, so the teams waiting there go straight on.
    /// </summary>
    public Task DoubleForfeit(int seasonMatchID, CancellationToken ct = default) =>
        Forfeit(seasonMatchID, SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit, ct);

    private async Task Forfeit(int seasonMatchID, SeasonMatchResult homeTeamResult, SeasonMatchResult awayTeamResult, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct);

        if (seasonMatch is null)
            throw new Exception("Season match does not exist");

        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            throw new Exception("You cannot report a match for a season match that does not have both teams assigned");

        if (seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null)
            throw new Exception("Results for this match have already been decided");

        if (seasonMatch.BracketMatch?.Bracket is Bracket.GrandFinal or Bracket.GrandFinalSuddenDeath)
            throw new SeasonMatchActionNotAllowedException("The grand final cannot be forfeited, report its games instead");

        seasonMatch.HomeTeamResult = homeTeamResult;
        seasonMatch.AwayTeamResult = awayTeamResult;

        if (seasonMatch.BracketMatch is not null)
        {
            if (homeTeamResult is SeasonMatchResult.Forfeit && awayTeamResult is SeasonMatchResult.Forfeit)
                _bracketService.SendByesToNextMatches(seasonMatch);
            else
                HandleSettingNextMatchesInBracket(seasonMatch);
            // A team that lands opposite a BYE goes straight on to its next match
            await _bracketService.AdvanceByesAsync(seasonMatch.SeasonID, ct);
        }

        await _context.SaveChangesAsync(ct);
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Resets a reported or forfeited match to undecided: clears its results, scores and reported games and, in a
    /// bracket, takes back everything the result sent on. Not allowed once a later match it fed into has a result
    /// of its own, other than one a BYE gave out.
    /// </summary>
    public async Task UndoResult(int seasonMatchID, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct);

        if (seasonMatch is null)
            throw new Exception("Season match does not exist");

        if (seasonMatch.HomeTeamResult is SeasonMatchResult.Bye || seasonMatch.AwayTeamResult is SeasonMatchResult.Bye)
            throw new SeasonMatchActionNotAllowedException("This match has a BYE, so its result is decided by the bracket and cannot be undone");

        var decided = seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null;

        if (decided is false && seasonMatch.SeriesGames.Count is 0)
            throw new SeasonMatchActionNotAllowedException("This match has no result to undo");

        if (decided && seasonMatch.BracketMatch is not null)
            await _bracketService.UndoAdvancementAsync(seasonMatch, ct);

        seasonMatch.HomeTeamResult = null;
        seasonMatch.AwayTeamResult = null;
        seasonMatch.HomeTeamScore = null;
        seasonMatch.AwayTeamScore = null;
        // Frees the games so they can be reported again
        _context.SeriesGames.RemoveRange(seasonMatch.SeriesGames);

        await _context.SaveChangesAsync(ct);
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    private class TeamsDto
    {
        public required MatchTeam HomeTeam { get; set; }
        public required MatchTeam AwayTeam { get; set; }
    }
}
