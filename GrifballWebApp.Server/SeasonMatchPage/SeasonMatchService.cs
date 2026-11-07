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

public partial class SeasonMatchService
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
            DecidedBy = seasonMatch.DecidedBy switch
            {
                SeasonMatchDecision.Games => SeasonMatchDecisionDto.Games,
                SeasonMatchDecision.Forfeit => SeasonMatchDecisionDto.Forfeit,
                SeasonMatchDecision.Bye => SeasonMatchDecisionDto.Bye,
                _ => null,
            },
            ScheduledTime = seasonMatch.ScheduledTime,
            BestOf = seasonMatch.BestOf,
            ReportedGames = reportedGames,
            SeriesTotals = seriesTotals,
            IsImported = seasonMatch.Season.HistoricalSourceID != null,
            BracketInfo = seasonMatch.BracketMatch is null ? null :
            new BracketInfoDto()
            {
                HomeTeamSeed = seasonMatch.BracketMatch.HomeTeamSeedNumber,
                AwayTeamSeed = seasonMatch.BracketMatch.AwayTeamSeedNumber,
                HomeTeamPreviousMatchID = seasonMatch.BracketMatch.HomeTeamPreviousMatchBracketInfo?.SeasonMatchID,
                AwayTeamPreviousMatchID = seasonMatch.BracketMatch.AwayTeamPreviousMatchBracketInfo?.SeasonMatchID,
                WinnerNextMatchID = nextMatch?.Winner?.Game.SeasonMatchID,
                LoserNextMatchID = nextMatch?.Loser?.Game.SeasonMatchID,
                IsGrandFinal = seasonMatch.BracketMatch.IsGrandFinal,
            },
            ActiveRescheduleRequestId = await _context.MatchReschedules.Where(MatchReschedule.IsPending)
                .Where(r => r.SeasonMatchID == seasonMatch.SeasonMatchID).Select(r => (int?)r.MatchRescheduleID).FirstOrDefaultAsync(ct),
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
        var rosters = await SeriesRosters.LoadAsync(_context, seasonMatchID, ct);
        return rosters.Home.Union(rosters.Away).ToList();
    }

    /// <summary>Before the season starts that its games can still be: a day, for a game played early.</summary>
    public static readonly TimeSpan BeforeSeason = TimeSpan.FromDays(1);

    /// <summary>After the season ends that its games can still be: a week, for a match played late.</summary>
    public static readonly TimeSpan AfterSeason = TimeSpan.FromDays(7);

    /// <summary>After a match's scheduled time that its games can still be: a day, for a match played late at night.</summary>
    public static readonly TimeSpan AfterScheduled = TimeSpan.FromDays(1);

    /// <summary>
    /// When the match's games can have been played: its season, from <see cref="BeforeSeason"/> before it starts to
    /// <see cref="AfterSeason"/> after it ends, or to <see cref="AfterScheduled"/> after the match's scheduled time when
    /// that is later (a match rescheduled past the season's end, RescheduleService). Null for a match that isn't there.
    /// </summary>
    public async Task<(DateTime From, DateTime To)?> SeasonWindow(int seasonMatchID, CancellationToken ct = default)
    {
        var season = await _context.SeasonMatches.Where(x => x.SeasonMatchID == seasonMatchID)
            .Select(x => new { x.Season.SeasonStart, x.Season.SeasonEnd, x.ScheduledTime })
            .FirstOrDefaultAsync(ct);
        if (season is null)
            return null;
        var to = season.SeasonEnd + AfterSeason;
        if (season.ScheduledTime + AfterScheduled is DateTime scheduled && scheduled > to)
            to = scheduled;
        return (season.SeasonStart - BeforeSeason, to);
    }

    /// <summary>
    /// The games both teams' players have played together that are in the database, played during the match's season
    /// (<see cref="SeasonWindow"/>): the newest 200 of them. No Halo call: their recent games are pulled by
    /// <see cref="SeasonMatchPullJob"/>, from this page (back to the season's start when asked), and
    /// <see cref="ActiveSeasonPullJob"/>, on a schedule.
    /// </summary>
    public async Task<List<PossibleMatchDto>> GetPossibleMatches(int seasonMatchID, CancellationToken ct = default)
    {
        var rosters = await SeriesRosters.LoadAsync(_context, seasonMatchID, ct);
        // Lists, for the query's IN
        var homeTeamIDs = rosters.Home.ToList();
        var awayTeamIDs = rosters.Away.ToList();

        if (homeTeamIDs.Count == 0 || awayTeamIDs.Count == 0)
            return Enumerable.Empty<PossibleMatchDto>().ToList();

        // Only its season's games: an old match's newest 200 would otherwise be games played long after it. There's a
        // window: the match is there (its teams' players were just read).
        var (from, to) = (await SeasonWindow(seasonMatchID, ct))!.Value;

        var matches = await _context.Matches
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
                        .ThenInclude(x => x.User)
            // A home player and an away player in it, before the newest 200 are taken: games with only one side's players
            // (scrims, other teams) would otherwise fill the 200 and push the match's own out
            .Where(x => x.MatchTeams.Any(t => t.MatchParticipants.Any(p => homeTeamIDs.Contains(p.XboxUserID))))
            .Where(x => x.MatchTeams.Any(t => t.MatchParticipants.Any(p => awayTeamIDs.Contains(p.XboxUserID))))
            .Where(x => x.StartTime >= from && x.StartTime <= to)
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
            var teamsDto = GetTeams(match, rosters);

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
                        IsOnTeam = rosters.Home.Contains(x.XboxUserID),
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
                        IsOnTeam = rosters.Away.Contains(x.XboxUserID),
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
            .Include(sm => sm.SeriesGames)
                .ThenInclude(g => g.RecordedGame!.Teams)
            .Include(sm => sm.SeriesGames)
                .ThenInclude(g => g.RecordedGame!.Participants)
            .Include(sm => sm.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo)
                .ThenInclude(x => x.SeasonMatch)
            .Include(sm => sm.BracketMatch.InverseAwayTeamNextMatchBracketInfo)
                .ThenInclude(x => x.SeasonMatch)
            .Where(sm => sm.SeasonMatchID == seasonMatchID)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Reports a Waypoint game as a game of the series: a new game, or replacing the game the commissioner picked
    /// (<paramref name="replacesSeriesGameID"/>; one entered by hand that this is the real record of, say). The site
    /// never guesses which game a game replaces.
    /// </summary>
    public async Task ReportMatch(int seasonMatchID, Guid matchID, CancellationToken ct = default, int? replacesSeriesGameID = null)
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
            throw new SeasonMatchActionNotAllowedException("Match is already associated with a season match");

        if (match.MatchTeams.Any(x => x.Outcome is Outcomes.Won) is false)
            throw new SeasonMatchActionNotAllowedException("There must be a winner for the match you are reporting");

        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct);

        if (seasonMatch is null)
            throw new SeasonMatchActionNotAllowedException("Season match does not exist");

        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            throw new SeasonMatchActionNotAllowedException("You cannot report a match for a season match that does not have both teams assigned");

        var number = 0;
        if (replacesSeriesGameID is int replaces)
            number = await ReplaceAsync(seasonMatch, replaces, ct);
        else
            RefuseNewGame(seasonMatch);

        seasonMatch.AddGame(match, number);

        await SavingSeries(async () =>
        {
            await ScoreSeriesAsync(seasonMatch, ct);
            await _context.SaveChangesAsync(ct);
        });
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    private void HandleSettingNextMatchesInBracket(SeasonMatch seasonMatch)
    {
        if (seasonMatch.BracketMatch!.Bracket is Bracket.GrandFinal && seasonMatch.Winner is SeasonMatchSide.Home)
        {
            return; // To not proceed to sudden death if the home team wins grand final
        }

        var nextMatches = _bracketService.DetermineNextMatches(seasonMatch);

        if (nextMatches.Winner is not null)
            nextMatches.Winner.Game.PlaceTeam(nextMatches.Winner.Side, seasonMatch.WinnerTeamID ?? throw new Exception("Missing winning team"));

        if (nextMatches.Loser is not null)
            nextMatches.Loser.Game.PlaceTeam(nextMatches.Loser.Side, seasonMatch.LoserTeamID ?? throw new Exception("Missing losing team"));
    }

    /// <summary>One team of one game: a Waypoint match team, or a side of a recorded game.</summary>
    private sealed class BoxScoreTeam
    {
        /// <summary>The team in the game: a match team's TeamID, a recorded game's Side.</summary>
        public int InGameTeam { get; init; }
        /// <summary>The season's team a recorded game's side was, when known.</summary>
        public int? TeamID { get; init; }
        public int? Score { get; init; }
        /// <summary>Its players' lines, the season stats' own (<see cref="PlayerGameStatsQuery.Series"/>).</summary>
        public List<PlayerGameStat> Players { get; set; } = [];
    }

    private sealed class BoxScoreGame
    {
        public int SeriesGameID { get; init; }
        public int GameNumber { get; init; }
        public Guid? MatchID { get; init; }
        public int? RecordedGameID { get; init; }
        /// <summary>A recorded game entered by hand (<see cref="ManualGames"/>).</summary>
        public bool IsManual { get; init; }
        public int? GameVersionID { get; init; }
        public string? GameVersionName { get; init; }
        public string? Map { get; init; }
        public string? Gametype { get; init; }
        public DateTime? PlayedAt { get; init; }
        public List<BoxScoreTeam> InfiniteTeams { get; init; } = [];
        public List<BoxScoreTeam> RecordedTeams { get; init; } = [];
    }

    /// <summary>
    /// The series' games with each side's players, and their totals over the series. A player's line is the season
    /// stats' own (<see cref="PlayerGameStatsQuery.Series"/>: its goals, score and K/D count as the stats count them),
    /// and a Waypoint game's sides are decided by <see cref="SeriesSides"/>, as the series is scored.
    /// </summary>
    private async Task<(ReportedGameDto[] Games, SeasonMatchSeriesTotalsDto? Totals)> GetBoxScores(SeasonMatch seasonMatch, CancellationToken ct)
    {
        var homeTeamID = seasonMatch.HomeTeamID;
        var awayTeamID = seasonMatch.AwayTeamID;

        var games = await _context.SeasonSeriesGames
            .Where(sg => sg.SeasonMatchID == seasonMatch.SeasonMatchID)
            .OrderBy(sg => sg.GameNumber)
            .Select(sg => new BoxScoreGame
            {
                SeriesGameID = sg.SeriesGameID,
                GameNumber = sg.GameNumber,
                MatchID = sg.MatchID,
                RecordedGameID = sg.RecordedGameID,
                IsManual = sg.RecordedGame != null && sg.RecordedGame.Origin == RecordedGameOrigin.Manual,
                GameVersionID = sg.RecordedGame != null ? (int?)sg.RecordedGame.GameVersionID : null,
                GameVersionName = sg.RecordedGame != null ? sg.RecordedGame.GameVersion.GameVersionName : null,
                Map = sg.RecordedGame != null ? sg.RecordedGame.Map : null,
                Gametype = sg.RecordedGame != null ? sg.RecordedGame.Gametype : null,
                PlayedAt = sg.RecordedGame != null ? sg.RecordedGame.PlayedAt : null,
                InfiniteTeams = sg.Match!.MatchTeams.Select(mt => new BoxScoreTeam { InGameTeam = mt.TeamID, Score = mt.Score }).ToList(),
                RecordedTeams = sg.RecordedGame!.Teams.Select(t => new BoxScoreTeam { InGameTeam = t.Side, TeamID = t.TeamID, Score = t.Score }).ToList(),
            })
            .AsSingleQuery()
            .ToListAsync(ct);

        if (games.Count == 0)
            return (Array.Empty<ReportedGameDto>(), null);

        // Each team's players: the season stats' lines of the series' games, by game and in-game team
        var lines = (await PlayerGameStatsQuery.Series(_context, seasonMatch.SeasonMatchID).ToListAsync(ct))
            .ToLookup(l => (l.MatchID, l.RecordedGameID, l.InGameTeam));
        foreach (var game in games)
            foreach (var team in game.InfiniteTeams.Concat(game.RecordedTeams))
                team.Players = lines[(game.MatchID, game.RecordedGameID, (int?)team.InGameTeam)].ToList();

        var rosters = await SeriesRosters.LoadAsync(_context, homeTeamID, awayTeamID, ct);
        var sides = games
            .Select(g => g.InfiniteTeams.Count >= 2
                ? InfiniteSides(g.InfiniteTeams, rosters)
                : RecordedSides(g.RecordedTeams, homeTeamID, awayTeamID))
            .ToList();

        var reportedGames = games.Zip(sides, (game, side) => new ReportedGameDto
        {
            SeriesGameID = game.SeriesGameID,
            MatchID = game.MatchID,
            RecordedGameID = game.RecordedGameID,
            MatchNumber = game.GameNumber,
            HomeScore = side.Home?.Score,
            AwayScore = side.Away?.Score,
            HomePlayers = InBoxScoreOrder(PlayersOf(side.Home).Select(ToBoxScoreLine)),
            AwayPlayers = InBoxScoreOrder(PlayersOf(side.Away).Select(ToBoxScoreLine)),
            IsManual = game.IsManual,
            GameVersionID = game.GameVersionID,
            GameVersionName = game.GameVersionName,
            Map = game.Map,
            Gametype = game.Gametype,
            PlayedAt = game.PlayedAt,
        }).ToArray();

        var totals = new SeasonMatchSeriesTotalsDto
        {
            HomePlayers = SeriesTotals(sides.Select(s => s.Home)),
            AwayPlayers = SeriesTotals(sides.Select(s => s.Away)),
        };

        return (reportedGames, totals);
    }

    /// <summary>
    /// A Waypoint game's home and away teams, by <see cref="SeriesSides"/>. One it can't decide (nobody rostered, say) is
    /// still laid out, by in-game team, so where its teams show never depends on row order.
    /// </summary>
    private static (BoxScoreTeam? Home, BoxScoreTeam? Away) InfiniteSides(List<BoxScoreTeam> teams, SeriesRosters rosters)
    {
        if (SeriesSides.Of(teams, t => rosters.Lean(t.Players.Select(p => p.XboxUserID).OfType<long>())) is { } sides)
            return (sides.Home, sides.Away);
        var ordered = teams.OrderBy(t => t.InGameTeam).ToList();
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

    private static IEnumerable<PlayerGameStat> PlayersOf(BoxScoreTeam? team) => team?.Players ?? Enumerable.Empty<PlayerGameStat>();

    private static int? UserOf(PlayerGameStat p) => p.UserID == PlayerGameStat.NoUser ? null : p.UserID;

    private static SeasonMatchGameParticipantDto ToBoxScoreLine(PlayerGameStat p) => new()
    {
        UserID = UserOf(p),
        Gamertag = p.Gamertag,
        Kills = p.Kills,
        Deaths = p.Deaths,
        Assists = p.Assists,
        // A game whose goals can't be read yet (#217) shows none
        BombScores = p.Goals ?? 0,
        Score = p.Score,
        KdRatio = StatMath.KdRatio(p.Kills, p.Deaths),
    };

    /// <summary>A side's players over the series, one row each: by their user, else (a Waypoint player with none) by
    /// their Xbox account, under the gamertag of their latest game.</summary>
    private static List<SeasonMatchGameParticipantDto> SeriesTotals(IEnumerable<BoxScoreTeam?> sideInGameOrder) =>
        InBoxScoreOrder(sideInGameOrder
            .SelectMany(PlayersOf)
            .GroupBy(PlayerKey)
            .Select(g =>
            {
                var (kills, deaths) = (g.Sum(x => x.Kills), g.Sum(x => x.Deaths));
                return new SeasonMatchGameParticipantDto
                {
                    UserID = g.Key.UserID,
                    Gamertag = g.Last().Gamertag,
                    Kills = kills,
                    Deaths = deaths,
                    Assists = g.Sum(x => x.Assists),
                    BombScores = g.Sum(x => x.Goals ?? 0),
                    Score = g.Any(x => x.Score != null) ? g.Sum(x => x.Score ?? 0) : null,
                    KdRatio = StatMath.KdRatio(kills, deaths),
                };
            }));

    private static (int? UserID, long? XboxUserID) PlayerKey(PlayerGameStat p) =>
        UserOf(p) is int userID ? (userID, null) : (null, p.XboxUserID);

    /// <summary>A box score's order: most goals first, then most kills, then fewest deaths, then by gamertag (the lines
    /// come in no order of their own).</summary>
    private static List<SeasonMatchGameParticipantDto> InBoxScoreOrder(IEnumerable<SeasonMatchGameParticipantDto> players) => players
        .OrderByDescending(p => p.BombScores)
        .ThenByDescending(p => p.Kills)
        .ThenBy(p => p.Deaths)
        .ThenBy(p => p.Gamertag, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// A Waypoint game's teams as the season match's sides, when its players match the rosters exactly: each team has
    /// players of one roster only, and the two teams of different rosters. Which is home is <see cref="SeriesSides"/>'
    /// call. Null otherwise: not a game between the two teams, or (a game reported before) one whose rosters changed.
    /// </summary>
    private static TeamsDto? GetTeams(Match match, SeriesRosters rosters)
    {
        static bool Has(MatchTeam team, IReadOnlySet<long> roster) => team.MatchParticipants.Any(p => roster.Contains(p.XboxUserID));

        var teams = match.MatchTeams.ToList();
        if (teams.Count != 2
            || teams.Any(t => Has(t, rosters.Home) == Has(t, rosters.Away))
            || Has(teams[0], rosters.Home) == Has(teams[1], rosters.Home))
            return null;

        if (SeriesSides.Of(teams, t => rosters.Lean(t.MatchParticipants.Select(p => p.XboxUserID))) is not { } sides)
            return null;

        return new TeamsDto()
        {
            HomeTeam = sides.Home,
            AwayTeam = sides.Away,
        };
    }

    public Task HomeForfeit(int seasonMatchID, CancellationToken ct = default) =>
        Forfeit(seasonMatchID, Forfeiter.Home, ct);

    public Task AwayForfeit(int seasonMatchID, CancellationToken ct = default) =>
        Forfeit(seasonMatchID, Forfeiter.Away, ct);

    /// <summary>
    /// Both teams forfeit: both take a loss and nobody wins. In a bracket every match this one feeds gets a BYE
    /// in its place, so the teams waiting there go straight on.
    /// </summary>
    public Task DoubleForfeit(int seasonMatchID, CancellationToken ct = default) =>
        Forfeit(seasonMatchID, Forfeiter.Both, ct);

    private async Task Forfeit(int seasonMatchID, Forfeiter forfeiter, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct);

        if (seasonMatch is null)
            throw new Exception("Season match does not exist");

        switch (seasonMatch.Forfeit(forfeiter))
        {
            case ForfeitResult.TeamsMissing:
                throw new Exception("You cannot report a match for a season match that does not have both teams assigned");
            case ForfeitResult.AlreadyDecided:
                throw new Exception("Results for this match have already been decided");
            case ForfeitResult.GrandFinal:
                throw new SeasonMatchActionNotAllowedException("The grand final cannot be forfeited, report its games instead");
        }

        if (seasonMatch.BracketMatch is not null)
        {
            if (forfeiter is Forfeiter.Both)
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
    /// Resets a reported or forfeited match to undecided: clears its results and, in a bracket, takes back everything
    /// the result sent on. A forfeit keeps the games played before it (and their score); otherwise the score and the
    /// series' games are cleared too: a Waypoint game is freed to be reported again, a game entered by hand is deleted.
    /// Not allowed once a later match it fed into has a result of its own, other than one a BYE gave out, or games.
    /// </summary>
    public async Task UndoResult(int seasonMatchID, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct);

        if (seasonMatch is null)
            throw new Exception("Season match does not exist");

        if (!seasonMatch.CanReopen)
            throw new SeasonMatchActionNotAllowedException("This match has a BYE, so its result is decided by the bracket and cannot be undone");

        if (!seasonMatch.IsDecided && seasonMatch.SeriesGames.Count is 0)
            throw new SeasonMatchActionNotAllowedException("This match has no result to undo");

        if (seasonMatch.IsDecided && seasonMatch.BracketMatch is not null)
            await _bracketService.UndoAdvancementAsync(seasonMatch, ct);

        var forfeited = seasonMatch.DecidedBy is SeasonMatchDecision.Forfeit;

        seasonMatch.Reopen();
        // A forfeit did not come from the games: they and their score stay, and the series goes on from them. Any
        // other undo clears the games too
        if (forfeited is false)
        {
            var games = _context.ClearSeries(seasonMatch);
            // Frees the games so they can be reported again; a game entered by hand has nothing to go back to, so it
            // is deleted (the confirmation says so)
            foreach (var game in games.Where(g => ManualGames.IsManual(g.RecordedGame)))
                RemoveRecordedGame(game.RecordedGame!);
        }

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
