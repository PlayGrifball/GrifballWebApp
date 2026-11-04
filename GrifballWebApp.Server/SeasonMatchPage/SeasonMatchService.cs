using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.Reschedule;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
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

        var (reportedGames, seriesTotals) = await GetBoxScores(seasonMatch, ct);

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

    /// <summary>One player's line of one game, as the box score reads it.</summary>
    private sealed class BoxScorePlayer
    {
        public int? UserID { get; init; }
        /// <summary>A Waypoint player's Xbox account: tells players with no user apart.</summary>
        public long? XboxUserID { get; init; }
        /// <summary>The season match's team whose roster a Waypoint player is on, if either.</summary>
        public int? RosterTeamID { get; init; }
        public string Gamertag { get; init; } = "";
        public int Kills { get; init; }
        public int Deaths { get; init; }
        public int Assists { get; init; }
        public int Goals { get; init; }
        public int? Score { get; init; }
    }

    /// <summary>One team of one game: a Waypoint match team, or a side of a recorded game.</summary>
    private sealed class BoxScoreTeam
    {
        /// <summary>The team in the game: a match team's TeamID, a recorded game's Side.</summary>
        public int InGameTeam { get; init; }
        /// <summary>The season's team a recorded game's side was, when known.</summary>
        public int? TeamID { get; init; }
        public int? Score { get; init; }
        public List<BoxScorePlayer> Players { get; init; } = [];
    }

    private sealed class BoxScoreGame
    {
        public int GameNumber { get; init; }
        public Guid? MatchID { get; init; }
        public int? RecordedGameID { get; init; }
        public List<BoxScoreTeam> InfiniteTeams { get; init; } = [];
        public List<BoxScoreTeam> RecordedTeams { get; init; } = [];
    }

    /// <summary>
    /// The series' games with each side's players, and their totals over the series: one query, Waypoint and recorded
    /// games alike, projected to just what the box score shows. Goals are <see cref="GrifballGoals"/>' (the season
    /// stats' and grades'), and a Waypoint player's roster is decided as <see cref="PlayerGameStatsQuery"/> decides it.
    /// </summary>
    private async Task<(ReportedGameDto[] Games, SeasonMatchSeriesTotalsDto? Totals)> GetBoxScores(SeasonMatch seasonMatch, CancellationToken ct)
    {
        var homeTeamID = seasonMatch.HomeTeamID;
        var awayTeamID = seasonMatch.AwayTeamID;

        var games = await _context.SeriesGames
            .Where(sg => sg.SeasonMatchID == seasonMatch.SeasonMatchID)
            .OrderBy(sg => sg.GameNumber)
            .Select(GrifballGoals.Inline((SeriesGame sg) => new BoxScoreGame
            {
                GameNumber = sg.GameNumber,
                MatchID = sg.MatchID,
                RecordedGameID = sg.RecordedGameID,
                InfiniteTeams = sg.Match!.MatchTeams.Select(mt => new BoxScoreTeam
                {
                    InGameTeam = mt.TeamID,
                    Score = mt.Score,
                    Players = mt.MatchParticipants.Select(p => new BoxScorePlayer
                    {
                        UserID = p.XboxUser.User != null ? (int?)p.XboxUser.User.Id : null,
                        XboxUserID = p.XboxUserID,
                        RosterTeamID = _context.TeamPlayers.Any(tp => tp.TeamID == homeTeamID && tp.UserID == p.XboxUser.User!.Id)
                            ? homeTeamID
                            : _context.TeamPlayers.Any(tp => tp.TeamID == awayTeamID && tp.UserID == p.XboxUser.User!.Id)
                                ? awayTeamID
                                : null,
                        Gamertag = p.XboxUser.Gamertag,
                        Kills = p.Kills,
                        Deaths = p.Deaths,
                        Assists = p.Assists,
                        Goals = GrifballGoals.Infinite(p),
                        Score = p.PersonalScore,
                    }).ToList(),
                }).ToList(),
                RecordedTeams = sg.RecordedGame!.Teams.Select(t => new BoxScoreTeam
                {
                    InGameTeam = t.Side,
                    TeamID = t.TeamID,
                    Score = t.Score,
                    Players = t.Participants.Select(p => new BoxScorePlayer
                    {
                        UserID = p.UserID,
                        Gamertag = p.Gamertag,
                        Kills = p.Kills,
                        Deaths = p.Deaths,
                        Assists = p.Assists,
                        Goals = p.Goals ?? 0,
                        Score = p.Score,
                    }).ToList(),
                }).ToList(),
            }))
            .AsSingleQuery()
            .ToListAsync(ct);

        if (games.Count == 0)
            return (Array.Empty<ReportedGameDto>(), null);

        var sides = games
            .Select(g => g.InfiniteTeams.Count >= 2
                ? InfiniteSides(g.InfiniteTeams, homeTeamID, awayTeamID)
                : RecordedSides(g.RecordedTeams, homeTeamID, awayTeamID))
            .ToList();

        var reportedGames = games.Zip(sides, (game, side) => new ReportedGameDto
        {
            MatchID = game.MatchID,
            RecordedGameID = game.RecordedGameID,
            MatchNumber = game.GameNumber,
            HomeScore = side.Home?.Score,
            AwayScore = side.Away?.Score,
            HomePlayers = InBoxScoreOrder(PlayersOf(side.Home).Select(ToBoxScoreLine)),
            AwayPlayers = InBoxScoreOrder(PlayersOf(side.Away).Select(ToBoxScoreLine)),
        }).ToArray();

        var totals = new SeasonMatchSeriesTotalsDto
        {
            HomePlayers = SeriesTotals(sides.Select(s => s.Home)),
            AwayPlayers = SeriesTotals(sides.Select(s => s.Away)),
        };

        return (reportedGames, totals);
    }

    /// <summary>
    /// A Waypoint game's home and away teams: home is the team leaning most to the home roster (its players on the home
    /// roster less those on the away one), away the other. A tie, e.g. when nobody played on either roster, goes to the
    /// in-game team, so the result never depends on row order.
    /// </summary>
    private static (BoxScoreTeam? Home, BoxScoreTeam? Away) InfiniteSides(List<BoxScoreTeam> teams, int? homeTeamID, int? awayTeamID)
    {
        int Lean(BoxScoreTeam team) =>
            team.Players.Count(p => p.RosterTeamID != null && p.RosterTeamID == homeTeamID)
            - team.Players.Count(p => p.RosterTeamID != null && p.RosterTeamID == awayTeamID);

        var ordered = teams.OrderByDescending(Lean).ThenBy(t => t.InGameTeam).ToList();
        return (ordered[0], ordered[^1]);
    }

    /// <summary>
    /// A recorded game's home and away sides: each by its season team. With one of them found the other is the game's
    /// other side; with neither (teams not decided, or not recorded), side 1 is home and side 2 away.
    /// </summary>
    private static (BoxScoreTeam? Home, BoxScoreTeam? Away) RecordedSides(List<BoxScoreTeam> teams, int? homeTeamID, int? awayTeamID)
    {
        var home = homeTeamID is null ? null : teams.FirstOrDefault(t => t.TeamID == homeTeamID);
        var away = awayTeamID is null ? null : teams.FirstOrDefault(t => t.TeamID == awayTeamID);

        if (home is null && away is null)
            return (teams.FirstOrDefault(t => t.InGameTeam == 1), teams.FirstOrDefault(t => t.InGameTeam == 2));

        var others = teams.Where(t => t != home && t != away).ToList();
        if (others.Count == 1)
        {
            home ??= others[0];
            away ??= others[0];
        }
        return (home, away);
    }

    private static IEnumerable<BoxScorePlayer> PlayersOf(BoxScoreTeam? team) => team?.Players ?? Enumerable.Empty<BoxScorePlayer>();

    private static SeasonMatchGameParticipantDto ToBoxScoreLine(BoxScorePlayer p) => new()
    {
        UserID = p.UserID,
        Gamertag = p.Gamertag,
        Kills = p.Kills,
        Deaths = p.Deaths,
        Assists = p.Assists,
        BombScores = p.Goals,
        Score = p.Score,
    };

    /// <summary>A side's players over the series, one row each: by their user, else (a Waypoint player with none) by
    /// their Xbox account, under the gamertag of their latest game.</summary>
    private static List<SeasonMatchGameParticipantDto> SeriesTotals(IEnumerable<BoxScoreTeam?> sideInGameOrder) =>
        InBoxScoreOrder(sideInGameOrder
            .SelectMany(PlayersOf)
            .GroupBy(PlayerKey)
            .Select(g => new SeasonMatchGameParticipantDto
            {
                UserID = g.Key.UserID,
                Gamertag = g.Last().Gamertag,
                Kills = g.Sum(x => x.Kills),
                Deaths = g.Sum(x => x.Deaths),
                Assists = g.Sum(x => x.Assists),
                BombScores = g.Sum(x => x.Goals),
                Score = g.Any(x => x.Score != null) ? g.Sum(x => x.Score ?? 0) : null,
            }));

    private static (int? UserID, long? XboxUserID) PlayerKey(BoxScorePlayer p) =>
        p.UserID is not null ? (p.UserID, null) : (null, p.XboxUserID);

    /// <summary>A box score's order: most goals first, then most kills, then fewest deaths.</summary>
    private static List<SeasonMatchGameParticipantDto> InBoxScoreOrder(IEnumerable<SeasonMatchGameParticipantDto> players) => players
        .OrderByDescending(p => p.BombScores)
        .ThenByDescending(p => p.Kills)
        .ThenBy(p => p.Deaths)
        .ToList();

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
