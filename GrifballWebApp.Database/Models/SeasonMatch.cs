using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A match of a season between two teams, a series of <see cref="BestOf"/> games: a regular-season match, a bracket slot
/// (<see cref="BracketMatch"/>), or a series imported from the historical archive. The root of its result and its series'
/// games: its result is changed only through its own methods, and each side's result pairs with the other's in one of
/// the legal ways (CK_Event_SeasonMatches_LegalResults):
/// <list type="bullet">
/// <item>undecided: no result on either side (waiting for its teams, ready, being played, or an imported draw);</item>
/// <item>decided by its games (<see cref="DecideByGames"/>): Won and Loss;</item>
/// <item>forfeited (<see cref="Forfeit"/>): Won and Forfeit, or Forfeit on both sides;</item>
/// <item>a BYE (<see cref="PlaceBye"/>, <see cref="AwardByeWin"/>): a BYE against nothing yet, a BYE, or the win of the
/// team facing it. A BYE side never has a team (CK_Event_SeasonMatches_ByeHasNoTeam).</item>
/// </list>
/// </summary>
// Keeps its teams' match lists (Team.Plays), the inverse of its own team links, and closes its pending reschedule request
// once it is decided (MatchReschedule.CloseBecauseDecided)
[AggregateRoot]
[AggregateAccess(typeof(Team))]
[AggregateAccess(typeof(MatchReschedule))]
public class SeasonMatch : AuditableEntity
{
    /// <summary>For EF. A match begins through <see cref="Between"/>, <see cref="InBracket"/> or <see cref="Imported"/>.</summary>
    private SeasonMatch() { }

    /// <summary>A regular-season match between two teams (the match planner's), undecided, at <paramref name="scheduledTime"/>
    /// if one is known.</summary>
    public static SeasonMatch Between(int seasonID, int homeTeamID, int awayTeamID, int bestOf, DateTime? scheduledTime = null)
    {
        if (homeTeamID == awayTeamID)
            throw new ArgumentException($"Team {homeTeamID} can't play itself", nameof(awayTeamID));
        return new()
        {
            SeasonID = seasonID,
            HomeTeamID = homeTeamID,
            AwayTeamID = awayTeamID,
            BestOf = bestOf,
            ScheduledTime = scheduledTime,
        };
    }

    /// <summary>
    /// A bracket slot: its teams come later, seeded or sent on by the matches before it. A first-round side whose seed is
    /// past the bracket's teams is a BYE from the start (<paramref name="homeBye"/>, <paramref name="awayBye"/>).
    /// </summary>
    public static SeasonMatch InBracket(int seasonID, MatchBracketInfo bracketMatch, int bestOf, bool homeBye = false, bool awayBye = false) => new()
    {
        SeasonID = seasonID,
        BracketMatch = bracketMatch,
        BestOf = bestOf,
        HomeTeamResult = homeBye ? SeasonMatchResult.Bye : null,
        AwayTeamResult = awayBye ? SeasonMatchResult.Bye : null,
    };

    /// <summary>
    /// A series of the historical archive, already played: its teams (null for a side the import could not tell), the
    /// games each won, and the result they give: more wins win, an even count is a draw and stays undecided. Its games
    /// come after (<see cref="AddImportedGame"/>), and its bracket slot, for a playoff series.
    /// </summary>
    public static SeasonMatch Imported(int seasonID, int? homeTeamID, int? awayTeamID, int homeWins, int awayWins, int bestOf, DateTime? scheduledTime, MatchBracketInfo? bracketMatch) => new()
    {
        SeasonID = seasonID,
        HomeTeamID = homeTeamID,
        AwayTeamID = awayTeamID,
        HomeTeamScore = homeWins,
        AwayTeamScore = awayWins,
        HomeTeamResult = ImportedResult(homeWins, awayWins),
        AwayTeamResult = ImportedResult(awayWins, homeWins),
        BestOf = bestOf,
        ScheduledTime = scheduledTime,
        BracketMatch = bracketMatch,
    };

    /// <summary>An imported side's result from the wins (<see cref="Imported"/>): more wins win, an even count is undecided.</summary>
    private static SeasonMatchResult? ImportedResult(int wins, int otherWins)
        => wins > otherWins ? SeasonMatchResult.Won : wins < otherWins ? SeasonMatchResult.Loss : null;

    /// <summary>An imported series' length (<see cref="Imported"/>): its games, or the games the leader's wins need, if
    /// more; at least one.</summary>
    public static int ImportedBestOf(int games, int homeWins, int awayWins) => Math.Max(1, Math.Max(games, 2 * Math.Max(homeWins, awayWins) - 1));

    /// <summary>
    /// The same as the app's own, of any teams and in any season and slot, undecided: for tests, which seed matches of
    /// their own (a bracket slot already holding its teams, a match without teams) and decide them through its methods.
    /// </summary>
    internal static SeasonMatch Seed(int seasonID, int? homeTeamID, int? awayTeamID, int bestOf = 1, DateTime? scheduledTime = null, MatchBracketInfo? bracketMatch = null) => new()
    {
        SeasonID = seasonID,
        HomeTeamID = homeTeamID,
        AwayTeamID = awayTeamID,
        BestOf = bestOf,
        ScheduledTime = scheduledTime,
        BracketMatch = bracketMatch,
    };

    /// <summary>The same, of entities not saved yet: for tests, whose season and teams are saved with it.</summary>
    internal static SeasonMatch Seed(Season? season, Team? home, Team? away, int bestOf = 1, DateTime? scheduledTime = null, MatchBracketInfo? bracketMatch = null)
    {
        var match = Seed(season?.SeasonID ?? 0, home?.TeamID is 0 ? null : home?.TeamID, away?.TeamID is 0 ? null : away?.TeamID, bestOf, scheduledTime, bracketMatch);
        match.Season = season!;
        match.HomeTeam = home;
        match.AwayTeam = away;
        home?.Plays(match, home: true);
        away?.Plays(match, home: false);
        return match;
    }

    // Nothing outside the match sets its state, its foreign keys or its navigations: only its own methods below, and EF
    // (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int SeasonMatchID { get; internal set; }
    public int SeasonID { get; private set; }
    public Season Season { get; private set; } = null!; // Should never be null if included in query
    /// <summary>The home team, once known: a bracket slot gets it by its seed or from the match before it (<see cref="PlaceTeam"/>).</summary>
    public int? HomeTeamID { get; private set; }
    public Team? HomeTeam { get; private set; }
    /// <summary>The games the home team has won in the series, once it has games (or was imported).</summary>
    public int? HomeTeamScore { get; private set; }
    /// <summary>The home side's result: null while undecided. Paired with the away side's only in the legal ways (see the class).</summary>
    public SeasonMatchResult? HomeTeamResult { get; private set; }
    public int? AwayTeamID { get; private set; }
    public Team? AwayTeam { get; private set; }
    public int? AwayTeamScore { get; private set; }
    public SeasonMatchResult? AwayTeamResult { get; private set; }
    /// <summary>When it is to be played, once scheduled (<see cref="ScheduleAt"/>).</summary>
    public DateTime? ScheduledTime { get; private set; }
    /// <summary>The series' length: a team with more than half of it in wins wins the match.</summary>
    public int BestOf { get; private set; }
    // Read-only: only the match's own methods add, number or remove a game (EF uses the field)
    public IReadOnlyCollection<SeriesGame> SeriesGames => _seriesGames;
    private readonly HashSet<SeriesGame> _seriesGames = [];
    /// <summary>Its slot in the season's bracket; null for a regular-season match.</summary>
    public MatchBracketInfo? BracketMatch { get; private set; }
    // Read-only: a request is made only by MatchReschedule.Request (EF uses the field)
    public IReadOnlyCollection<MatchReschedule> Reschedules => _reschedules;
    private readonly HashSet<MatchReschedule> _reschedules = [];

    /// <summary>Its pending reschedule request, the active one (one at most), when its requests are loaded.</summary>
    public MatchReschedule? PendingReschedule => _reschedules.SingleOrDefault(r => r.AwaitsDecision);

    internal void AddReschedule(MatchReschedule reschedule) => _reschedules.Add(reschedule);

    /// <summary>
    /// Once decided, a match keeps its time, so its pending reschedule request has nothing left to approve: it is closed
    /// (<see cref="MatchReschedule.CloseBecauseDecided"/>, #297). Every transition that decides the match calls this; its
    /// requests must be loaded (<see cref="Reschedules"/>) for a pending one to be found.
    /// </summary>
    private void CloseRescheduleOnceDecided()
    {
        if (IsDecided)
            PendingReschedule?.CloseBecauseDecided(DateTime.UtcNow);
    }

    /// <summary>A match with no result on either side yet: neither decided nor a BYE's (for queries; <see cref="IsDecided"/>
    /// for a loaded match).</summary>
    public static readonly Expression<Func<SeasonMatch, bool>> Undecided = m => m.HomeTeamResult == null && m.AwayTeamResult == null;

    /// <summary>Whether it has a result: decided by its games, a forfeit, or a BYE (a BYE slot is the bracket's to decide,
    /// never its games'). <see cref="Undecided"/> for queries.</summary>
    public bool IsDecided => HomeTeamResult is not null || AwayTeamResult is not null;

    /// <summary>What decided it; null while undecided. A BYE's (<see cref="SeasonMatchDecision.Bye"/>) whether or not the
    /// team facing it has arrived yet.</summary>
    public SeasonMatchDecision? DecidedBy => (HomeTeamResult, AwayTeamResult) switch
    {
        (null, null) => null,
        (SeasonMatchResult.Bye, _) or (_, SeasonMatchResult.Bye) => SeasonMatchDecision.Bye,
        (SeasonMatchResult.Forfeit, _) or (_, SeasonMatchResult.Forfeit) => SeasonMatchDecision.Forfeit,
        _ => SeasonMatchDecision.Games,
    };

    /// <summary>The side that won it: by its games, the other side's forfeit, or a BYE. Null while undecided, when both
    /// sides forfeited, and for a BYE still waiting for its team (or facing another BYE).</summary>
    public SeasonMatchSide? Winner => HomeTeamResult is SeasonMatchResult.Won ? (SeasonMatchSide?)SeasonMatchSide.Home
        : AwayTeamResult is SeasonMatchResult.Won ? SeasonMatchSide.Away
        : null;

    /// <summary>The team that lost it to the winner: by its games or by forfeiting. Null without a winner, and when the
    /// winner won by a BYE (a BYE is no team).</summary>
    public SeasonMatchSide? Loser => Winner is { } winner && ResultOf(Other(winner)) is SeasonMatchResult.Loss or SeasonMatchResult.Forfeit
        ? Other(winner)
        : null;

    /// <summary>The winner's team (<see cref="Winner"/>); null without a winner.</summary>
    public int? WinnerTeamID => Winner is { } winner ? TeamOf(winner) : null;

    /// <summary>The loser's team (<see cref="Loser"/>); null without a loser.</summary>
    public int? LoserTeamID => Loser is { } loser ? TeamOf(loser) : null;

    /// <summary>The games a side has won in the series, once it has games.</summary>
    public int? ScoreOf(SeasonMatchSide side) => side is SeasonMatchSide.Home ? HomeTeamScore : AwayTeamScore;

    /// <summary>The team of a side, once known.</summary>
    public int? TeamOf(SeasonMatchSide side) => side is SeasonMatchSide.Home ? HomeTeamID : AwayTeamID;

    /// <summary>A side's result: null while undecided.</summary>
    public SeasonMatchResult? ResultOf(SeasonMatchSide side) => side is SeasonMatchSide.Home ? HomeTeamResult : AwayTeamResult;

    /// <summary>Whether a side is a BYE: no team will ever play there.</summary>
    public bool IsBye(SeasonMatchSide side) => ResultOf(side) is SeasonMatchResult.Bye;

    /// <summary>Whether either side is a BYE.</summary>
    public bool HasBye => IsBye(SeasonMatchSide.Home) || IsBye(SeasonMatchSide.Away);

    /// <summary>Whether both of its teams are known.</summary>
    public bool HasBothTeams => HomeTeamID is not null && AwayTeamID is not null;

    /// <summary>A grand final (or its sudden death), which is never forfeited: its games decide the bracket.</summary>
    private bool IsGrandFinal => BracketMatch?.IsGrandFinal is true;

    public static SeasonMatchSide Other(SeasonMatchSide side) => side is SeasonMatchSide.Home ? SeasonMatchSide.Away : SeasonMatchSide.Home;

    /// <summary>Moves it to another time, or (null) back to unscheduled.</summary>
    public void ScheduleAt(DateTime? time) => ScheduledTime = time;

    /// <summary>
    /// Scores the series from its games' wins, and decides it when a side has the wins it needs (more than half of
    /// <see cref="BestOf"/>): Won and Loss. A series without games (none won, none in it) has no score, as before any were
    /// reported. Only an undecided match with both teams: a decided one is reopened first (<see cref="Reopen"/>).
    /// Returns whether it is decided now.
    /// </summary>
    public bool DecideByGames(int homeWins, int awayWins)
    {
        if (IsDecided)
            throw new InvalidOperationException($"Season match {SeasonMatchID} is already decided ({DecidedBy}): reopen it first");
        if (!HasBothTeams)
            throw new InvalidOperationException($"Season match {SeasonMatchID} does not have both teams");
        if (homeWins < 0 || awayWins < 0)
            throw new ArgumentOutOfRangeException(nameof(homeWins), "A series can't have negative wins");

        var played = _seriesGames.Count > 0 || homeWins + awayWins > 0;
        HomeTeamScore = played ? homeWins : null;
        AwayTeamScore = played ? awayWins : null;

        var winsRequired = Math.Ceiling(BestOf * 0.5f);
        if (homeWins >= winsRequired)
            (HomeTeamResult, AwayTeamResult) = (SeasonMatchResult.Won, SeasonMatchResult.Loss);
        else if (awayWins >= winsRequired)
            (HomeTeamResult, AwayTeamResult) = (SeasonMatchResult.Loss, SeasonMatchResult.Won);
        CloseRescheduleOnceDecided();
        return IsDecided;
    }

    /// <summary>
    /// A side forfeits, or both do (<paramref name="forfeiter"/>): the other side wins, or nobody does and both take a loss.
    /// Its games so far, and their score, stay. Only an undecided match with both teams, and never a grand final, whose
    /// games decide the bracket.
    /// </summary>
    public ForfeitResult Forfeit(Forfeiter forfeiter)
    {
        if (!HasBothTeams)
            return ForfeitResult.TeamsMissing;
        if (IsDecided)
            return ForfeitResult.AlreadyDecided;
        if (IsGrandFinal)
            return ForfeitResult.GrandFinal;

        (HomeTeamResult, AwayTeamResult) = forfeiter switch
        {
            Forfeiter.Home => (SeasonMatchResult.Forfeit, SeasonMatchResult.Won),
            Forfeiter.Away => (SeasonMatchResult.Won, SeasonMatchResult.Forfeit),
            Forfeiter.Both => (SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit),
            _ => throw new ArgumentOutOfRangeException(nameof(forfeiter), forfeiter, null),
        };
        CloseRescheduleOnceDecided();
        return ForfeitResult.Forfeited;
    }

    /// <summary>Whether its result can be taken back (<see cref="Reopen"/>): any but a BYE's, which is the bracket's.</summary>
    public bool CanReopen => DecidedBy is not SeasonMatchDecision.Bye;

    /// <summary>Whether a commissioner's Undo Result has anything to take back: a result it may (<see cref="CanReopen"/>),
    /// or the games of an undecided series. What the match page offers; its games must be loaded (<see cref="SeriesGames"/>).</summary>
    public bool CanUndoResult => CanReopen && (IsDecided || _seriesGames.Count > 0);

    /// <summary>
    /// Takes its result back: undecided again, its games and their score as they were (<see cref="ClearSeries"/> clears
    /// them). Nothing to take back from an undecided match. Never a BYE's (<see cref="CanReopen"/>). In a bracket, what the
    /// result sent on is the bracket's to take back first.
    /// </summary>
    public void Reopen()
    {
        if (!CanReopen)
            throw new InvalidOperationException($"Season match {SeasonMatchID} has a BYE: its result is the bracket's");
        HomeTeamResult = null;
        AwayTeamResult = null;
    }

    /// <summary>
    /// Why a new game, one not replacing a game of the series, has no room in it, or null when it has: a decided series
    /// has none, nor (only inconsistent data) one with all its games.
    /// </summary>
    public NewGameRefusal? WhyNoNewGame()
    {
        if (IsDecided)
            return NewGameRefusal.Decided;
        if (_seriesGames.Count >= BestOf)
            return NewGameRefusal.Full;
        return null;
    }

    /// <summary>A Waypoint game reported as a game of the series, numbered <paramref name="gameNumber"/> (0 until the series
    /// is numbered, <see cref="NumberGame"/>). The series is scored after (<see cref="DecideByGames"/>).</summary>
    public SeriesGame AddGame(Match match, int gameNumber = 0) => Add(new SeriesGame { SeasonMatch = this, Match = match, GameNumber = gameNumber });

    /// <summary>A recorded game (one entered by hand) as a game of the series; as <see cref="AddGame(Match, int)"/>.</summary>
    public SeriesGame AddGame(RecordedGame game, int gameNumber = 0) => Add(new SeriesGame { SeasonMatch = this, RecordedGame = game, GameNumber = gameNumber });

    /// <summary>A game of an imported series (<see cref="Imported"/>): a Waypoint game by its ID, else its recorded game.</summary>
    public SeriesGame AddImportedGame(Guid? matchID, RecordedGame? recorded, int gameNumber) => Add(new SeriesGame { SeasonMatch = this, MatchID = matchID, RecordedGame = recorded, GameNumber = gameNumber });

    private SeriesGame Add(SeriesGame game)
    {
        _seriesGames.Add(game);
        return game;
    }

    /// <summary>A game's number in the series: its order, game 1, game 2, ... (a negative one only on the way there).</summary>
    public void NumberGame(SeriesGame game, int gameNumber)
    {
        EnsureOwn(game);
        game.GameNumber = gameNumber;
    }

    /// <summary>Takes a game out of the series (unreported); the series is scored again after. Only an undecided series:
    /// a decided one is reopened first.</summary>
    public void RemoveGame(SeriesGame game)
    {
        EnsureOwn(game);
        if (IsDecided)
            throw new InvalidOperationException($"Season match {SeasonMatchID} is decided ({DecidedBy}): reopen it before changing its games");
        _seriesGames.Remove(game);
    }

    /// <summary>
    /// Takes a game out of an imported series (<see cref="Imported"/>) when a re-import no longer has it there, and works the
    /// series out again from the games left, by <see cref="Imported"/>'s rules: the game's win comes off its side's score
    /// (<paramref name="homeWon"/>, <paramref name="awayWon"/>: as the import counted it), more wins win, an even count is
    /// undecided, and the best-of is what the games left need (<see cref="ImportedBestOf"/>). A decided series is worked
    /// out again too, as its result was its games'; a forfeit or a BYE was not, so it stays. Returns whether the series has
    /// games left: one with none is the import's to delete.
    /// </summary>
    public bool RemoveImportedGame(SeriesGame game, bool homeWon, bool awayWon)
    {
        EnsureOwn(game);
        _seriesGames.Remove(game);
        var homeWins = Math.Max(0, (HomeTeamScore ?? 0) - (homeWon ? 1 : 0));
        var awayWins = Math.Max(0, (AwayTeamScore ?? 0) - (awayWon ? 1 : 0));
        (HomeTeamScore, AwayTeamScore) = (homeWins, awayWins);
        if (DecidedBy is null or SeasonMatchDecision.Games)
            (HomeTeamResult, AwayTeamResult) = (ImportedResult(homeWins, awayWins), ImportedResult(awayWins, homeWins));
        BestOf = ImportedBestOf(_seriesGames.Count, homeWins, awayWins);
        CloseRescheduleOnceDecided();
        return _seriesGames.Count > 0;
    }

    /// <summary>
    /// Takes all its games out, and their score: back to before any were reported. Only an undecided series (a result
    /// is taken back first, <see cref="Reopen"/>). Returns the games taken out, for the caller to delete.
    /// </summary>
    public IReadOnlyList<SeriesGame> ClearSeries()
    {
        if (IsDecided)
            throw new InvalidOperationException($"Season match {SeasonMatchID} is decided ({DecidedBy}): reopen it before clearing its games");
        var games = _seriesGames.ToList();
        _seriesGames.Clear();
        HomeTeamScore = null;
        AwayTeamScore = null;
        return games;
    }

    private void EnsureOwn(SeriesGame game)
    {
        if (!_seriesGames.Contains(game))
            throw new InvalidOperationException($"Series game {game.SeriesGameID} is not a game of season match {SeasonMatchID}");
    }

    /// <summary>
    /// A team takes a side: seeded, or sent on by the match before it. Never a BYE side (no team ever plays there), nor
    /// against itself. Returns whether the side changed.
    /// </summary>
    public bool PlaceTeam(SeasonMatchSide side, int teamID)
    {
        if (IsBye(side))
            throw new InvalidOperationException($"Season match {SeasonMatchID}'s {side} side is a BYE: no team plays there");
        if (TeamOf(Other(side)) == teamID)
            throw new InvalidOperationException($"Team {teamID} can't play itself in season match {SeasonMatchID}");
        if (TeamOf(side) == teamID)
            return false;
        if (side is SeasonMatchSide.Home)
            HomeTeamID = teamID;
        else
            AwayTeamID = teamID;
        return true;
    }

    /// <summary>
    /// A side becomes a BYE: no team will play there (its seed is past the bracket's teams, or the match before it had no
    /// team to send on). Only an empty side of a match its games or a forfeit have not decided. The team facing it wins
    /// once it is there (<see cref="AwardByeWin"/>). Returns whether the side changed.
    /// </summary>
    public bool PlaceBye(SeasonMatchSide side)
    {
        if (IsBye(side))
            return false;
        if (TeamOf(side) is not null)
            throw new InvalidOperationException($"Season match {SeasonMatchID}'s {side} side has team {TeamOf(side)}: it can't be a BYE");
        if (DecidedBy is SeasonMatchDecision.Games or SeasonMatchDecision.Forfeit)
            throw new InvalidOperationException($"Season match {SeasonMatchID} is decided ({DecidedBy}): it can't take a BYE");
        if (side is SeasonMatchSide.Home)
            HomeTeamResult = SeasonMatchResult.Bye;
        else
            AwayTeamResult = SeasonMatchResult.Bye;
        CloseRescheduleOnceDecided();
        return true;
    }

    /// <summary>
    /// The team facing a BYE wins the match, as brackets-manager does (so it counts in the standings), and goes straight
    /// on. Only a match with a BYE on one side and a team on the other. Returns whether it changed (false once it has
    /// that win).
    /// </summary>
    public bool AwardByeWin()
    {
        var bye = IsBye(SeasonMatchSide.Home) ? SeasonMatchSide.Home : IsBye(SeasonMatchSide.Away) ? SeasonMatchSide.Away
            : throw new InvalidOperationException($"Season match {SeasonMatchID} has no BYE");
        var team = Other(bye);
        if (IsBye(team) || TeamOf(team) is null)
            throw new InvalidOperationException($"Season match {SeasonMatchID} has no team facing its BYE");
        if (ResultOf(team) is SeasonMatchResult.Won)
            return false;
        if (team is SeasonMatchSide.Home)
            HomeTeamResult = SeasonMatchResult.Won;
        else
            AwayTeamResult = SeasonMatchResult.Won;
        CloseRescheduleOnceDecided();
        return true;
    }

    /// <summary>
    /// Empties a side an earlier match filled (its result taken back): no team, no BYE. The other side keeps a BYE, but
    /// loses a win it only had because of this side. Never a match its games or a forfeit decided: that is undone first.
    /// </summary>
    public void ClearSide(SeasonMatchSide side)
    {
        if (DecidedBy is SeasonMatchDecision.Games or SeasonMatchDecision.Forfeit)
            throw new InvalidOperationException($"Season match {SeasonMatchID} is decided ({DecidedBy}): undo it first");
        if (side is SeasonMatchSide.Home)
        {
            HomeTeamID = null;
            HomeTeam = null;
            HomeTeamResult = null;
            if (AwayTeamResult is SeasonMatchResult.Won)
                AwayTeamResult = null;
        }
        else
        {
            AwayTeamID = null;
            AwayTeam = null;
            AwayTeamResult = null;
            if (HomeTeamResult is SeasonMatchResult.Won)
                HomeTeamResult = null;
        }
    }
}

/// <summary>A side's result in a <see cref="SeasonMatch"/>, paired with the other side's only in the legal ways.</summary>
public enum SeasonMatchResult
{
    Won = 0,
    Loss = 1,
    Forfeit = 2,
    Bye = 3,
}

/// <summary>A side of a <see cref="SeasonMatch"/>.</summary>
public enum SeasonMatchSide
{
    Home,
    Away,
}

/// <summary>What decided a <see cref="SeasonMatch"/> (<see cref="SeasonMatch.DecidedBy"/>).</summary>
public enum SeasonMatchDecision
{
    /// <summary>Its games: a team won the series (Won and Loss), or the archive's record of them.</summary>
    Games,
    /// <summary>A forfeit, by one side (the other won) or both (nobody did).</summary>
    Forfeit,
    /// <summary>A BYE: no team will play on that side, and the team facing it, once there, wins.</summary>
    Bye,
}

/// <summary>Who forfeits a <see cref="SeasonMatch"/> (<see cref="SeasonMatch.Forfeit"/>).</summary>
public enum Forfeiter
{
    Home,
    Away,
    Both,
}

/// <summary>What <see cref="SeasonMatch.Forfeit"/> did.</summary>
public enum ForfeitResult
{
    Forfeited,
    /// <summary>Refused: a team is not known yet.</summary>
    TeamsMissing,
    /// <summary>Refused: it has a result already (or a BYE).</summary>
    AlreadyDecided,
    /// <summary>Refused: a grand final's games decide the bracket.</summary>
    GrandFinal,
}

/// <summary>Why a new game has no room in a series (<see cref="SeasonMatch.WhyNoNewGame"/>).</summary>
public enum NewGameRefusal
{
    /// <summary>The series is decided.</summary>
    Decided,
    /// <summary>It has all its games (only inconsistent data: a full series is always decided).</summary>
    Full,
}
