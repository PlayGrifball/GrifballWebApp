using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A game that is not a Halo Infinite match from Waypoint: an MCC (or older) game recorded from a carnage report or a
/// stats sheet. In a season through its <see cref="SeriesGame"/>, or a matchmaking (Late League) game through its
/// <see cref="MatchedMatch"/>. The root of its sides (<see cref="RecordedGameTeam"/>) and its players' lines
/// (<see cref="RecordedGameParticipant"/>): they are made, changed and removed only through its methods.
/// <list type="bullet">
/// <item>imported (<see cref="Import"/>), and imported again by its key (<see cref="Reimport"/>);</item>
/// <item>entered by hand on the match page (<see cref="EnterByHand"/>), and edited there (<see cref="EditByHand"/>): the
/// import never touches it, and it counts for its season only through its series game, never a season of its own
/// (CK_Recorded_Games_ManualHasNoSeason).</item>
/// </list>
/// </summary>
[AggregateRoot]
public class RecordedGame : AuditableEntity
{
    public const int SourceKeyMaxLength = 200;
    public const int MapMaxLength = 100;
    public const int GametypeMaxLength = 100;

    /// <summary>The <see cref="SourceKey"/> prefix of a game entered by hand, which needs a unique key like any recorded
    /// game: never read, <see cref="Origin"/> is what says a game was entered by hand.</summary>
    public const string ManualSourceKeyPrefix = "manual/";

    /// <summary>For EF. A game begins through <see cref="Import"/> or <see cref="EnterByHand"/>.</summary>
    private RecordedGame() { }

    /// <summary>A game the historical import brings in, found again by <paramref name="sourceKey"/> on a re-import; in
    /// <paramref name="seasonID"/>'s season, or in none (a matchmaking game). Its sides and players come next
    /// (<see cref="SetSides"/>, <see cref="SetPlayers"/>).</summary>
    public static RecordedGame Import(string sourceKey, RecordedGameFacts facts, int? seasonID)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        if (sourceKey.Length > SourceKeyMaxLength)
            throw new ArgumentException($"A source key is at most {SourceKeyMaxLength} characters", nameof(sourceKey));
        var game = new RecordedGame { SourceKey = sourceKey, Origin = RecordedGameOrigin.HistoricalImport };
        game.Record(facts);
        game.SeasonID = seasonID;
        return game;
    }

    /// <summary>
    /// A game a commissioner enters by hand (manual match entry), for a season match's series: it counts for its season
    /// through its series game only, so it has no season of its own and stops counting if its season match goes (the
    /// stats count a recorded game with a season even outside any series). Its key is one of its own, never read.
    /// </summary>
    public static RecordedGame EnterByHand(RecordedGameFacts facts)
    {
        var game = new RecordedGame { SourceKey = ManualSourceKeyPrefix + Guid.NewGuid().ToString("N"), Origin = RecordedGameOrigin.Manual };
        game.Record(facts);
        return game;
    }

    /// <summary>
    /// A game in any state, past the rules: for tests, which seed games as the import and the match page leave them (and
    /// old rows for the migration tests).
    /// </summary>
    internal static RecordedGame Seed(string sourceKey, int gameVersionID = 1, int? seasonID = null, RecordedGameOrigin origin = RecordedGameOrigin.HistoricalImport,
        bool isPlayoffs = false, DateTime? playedAt = null, string? map = null, string? gametype = null) => new()
    {
        SourceKey = sourceKey,
        GameVersionID = gameVersionID,
        SeasonID = seasonID,
        Origin = origin,
        IsPlayoffs = isPlayoffs,
        PlayedAt = playedAt,
        Map = map,
        Gametype = gametype,
    };

    /// <summary>A side added past the rules, for tests (<see cref="Seed"/>).</summary>
    internal RecordedGameTeam SeedSide(int side, int? teamID = null, Outcomes outcome = Outcomes.Won, int? score = null, string? name = null)
    {
        var row = new RecordedGameTeam(side);
        row.Record(new RecordedSideFacts(side, teamID, name, outcome, score));
        _teams.Add(row);
        return row;
    }

    /// <summary>A player's line added past the rules, for tests (<see cref="Seed"/>): on <paramref name="team"/>'s side.</summary>
    internal RecordedGameParticipant SeedPlayer(RecordedPlayerLine line, RecordedGameTeam? team = null)
    {
        var row = new RecordedGameParticipant(line.UserID);
        row.Record(line, team);
        _participants.Add(row);
        return row;
    }

    // Nothing outside the game sets its fields, its foreign keys or its navigations: only its own methods, and EF (which
    // uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int RecordedGameID { get; internal set; }
    public int GameVersionID { get; private set; }
    public GameVersion GameVersion { get; private set; } = null!;
    /// <summary>The season the game counts for, what the import finds a season's games by; null for a matchmaking game,
    /// and for a game entered by hand (which counts through its series game).</summary>
    public int? SeasonID { get; private set; }
    public Season? Season { get; private set; }
    public string? Map { get; private set; }
    public string? Gametype { get; private set; }
    public DateTime? PlayedAt { get; private set; }
    /// <summary>The import's key for the game (its source file and game), what a re-import finds it by. Unique. A game
    /// entered by hand has a key of its own that nothing reads: <see cref="Origin"/> says where a game came from.</summary>
    public string SourceKey { get; private set; } = null!;
    /// <summary>Where the game came from: the historical import, or entered by hand on the match page.</summary>
    public RecordedGameOrigin Origin { get; private set; } = RecordedGameOrigin.HistoricalImport;
    /// <summary>A playoff game, as its source says; a game in a series is one when its series is in the bracket.</summary>
    public bool IsPlayoffs { get; private set; }

    /// <summary>Its game of a season match's series: set from the match's side only (<see cref="SeasonMatch.AddGame(RecordedGame, int)"/>), which owns the link.</summary>
    public SeriesGame? SeriesGame { get; private set; }
    /// <summary>Set from the match's side only (<see cref="MatchedMatch.RecordArchived"/>), which owns the link.</summary>
    public MatchedMatch? MatchedMatch { get; private set; }
    // Read-only: only the game's own methods add, change or remove a side or a line (EF uses the fields)
    public IReadOnlyCollection<RecordedGameTeam> Teams => _teams;
    private readonly HashSet<RecordedGameTeam> _teams = [];
    public IReadOnlyCollection<RecordedGameParticipant> Participants => _participants;
    private readonly HashSet<RecordedGameParticipant> _participants = [];

    /// <summary>Entered by hand on the match page (<see cref="IsManual"/> for a loaded game).</summary>
    public static readonly Expression<Func<RecordedGame, bool>> Manual = g => g.Origin == RecordedGameOrigin.Manual;

    /// <summary>The import's, which it may change or remove: never a game entered by hand, whatever its key
    /// (<see cref="IsManual"/> for a loaded game).</summary>
    public static readonly Expression<Func<RecordedGame, bool>> Imported = g => g.Origin != RecordedGameOrigin.Manual;

    /// <summary>Entered by hand (<see cref="Manual"/> for queries): what makes a game editable on the match page, and what
    /// is deleted with its series game.</summary>
    public bool IsManual => Origin is RecordedGameOrigin.Manual;

    /// <summary>Whether a side of <paramref name="teamID"/>'s team won the game.</summary>
    public bool WonBy(int? teamID) => _teams.Any(t => t.TeamID == teamID && t.Outcome is Outcomes.Won);

    /// <summary>What a re-import of the game's source says of it now, in <paramref name="seasonID"/>'s season (or none).
    /// Never a game entered by hand: the import leaves those alone.</summary>
    public void Reimport(RecordedGameFacts facts, int? seasonID)
    {
        if (IsManual)
            throw new InvalidOperationException($"Recorded game {RecordedGameID} was entered by hand: the import never changes it");
        Record(facts);
        SeasonID = seasonID;
    }

    /// <summary>What a commissioner entered now, for a game entered by hand. Never an imported game: its archive is what
    /// corrects it.</summary>
    public void EditByHand(RecordedGameFacts facts)
    {
        if (!IsManual)
            throw new InvalidOperationException($"Recorded game {RecordedGameID} was imported: only a game entered by hand is edited on the match page");
        Record(facts);
    }

    /// <summary>
    /// Its sides become <paramref name="sides"/>, found by their number: a side kept is updated in place, one gone is
    /// removed (EF deletes the orphan) and a new one added. A side numbered twice is refused.
    /// </summary>
    public void SetSides(IReadOnlyCollection<RecordedSideFacts> sides)
    {
        if (sides.GroupBy(s => s.Side).Any(g => g.Count() > 1))
            throw new ArgumentException("A side is listed once", nameof(sides));
        if (sides.FirstOrDefault(s => !Enum.IsDefined(s.Outcome)) is { } unknown)
            throw new ArgumentException($"Side {unknown.Side} has no outcome a game knows ({(int)unknown.Outcome})", nameof(sides));

        _teams.RemoveWhere(t => sides.All(s => s.Side != t.Side));
        foreach (var side in sides)
        {
            var row = _teams.FirstOrDefault(t => t.Side == side.Side);
            if (row is null)
                _teams.Add(row = new RecordedGameTeam(side.Side));
            row.Record(side);
        }
    }

    /// <summary>
    /// Its players' lines become <paramref name="lines"/>, found by their user: a line kept is updated in place, one gone
    /// is removed (EF deletes the orphan) and a new one added. Each is on its side's team, by number (on none when the game
    /// has no such side). A player listed twice is refused. A user with more than one line (a merge before
    /// <see cref="ReassignPlayer"/> folded them) is left with one, the oldest: the others go.
    /// </summary>
    public void SetPlayers(IReadOnlyCollection<RecordedPlayerLine> lines)
    {
        if (lines.GroupBy(l => l.UserID).Any(g => g.Count() > 1))
            throw new ArgumentException("A player has one line in a game", nameof(lines));

        foreach (var extra in _participants.GroupBy(p => p.UserID).SelectMany(g => g.OrderBy(p => p.RecordedGameParticipantID).Skip(1)).ToList())
            _participants.Remove(extra);
        _participants.RemoveWhere(p => lines.All(l => l.UserID != p.UserID));
        foreach (var line in lines)
        {
            var row = _participants.FirstOrDefault(p => p.UserID == line.UserID);
            if (row is null)
                _participants.Add(row = new RecordedGameParticipant(line.UserID));
            row.Record(line, line.Side is int side ? _teams.FirstOrDefault(t => t.Side == side) : null);
        }
    }

    /// <summary>
    /// A merge: <paramref name="fromUserID"/>'s line becomes <paramref name="toUserID"/>'s (what the player played,
    /// whichever account). A user has one line in a game: when <paramref name="toUserID"/> has one already (one person
    /// twice), theirs stays and the source's goes, as <see cref="MatchedMatch.ReassignPlayer"/> does, so the stats count
    /// the player once. Returns how many lines moved (not the ones that went).
    /// </summary>
    public int ReassignPlayer(int fromUserID, int toUserID)
    {
        if (fromUserID == toUserID)
            return 0;
        var moved = 0;
        var kept = _participants.Any(p => p.UserID == toUserID);
        foreach (var line in _participants.Where(p => p.UserID == fromUserID).OrderBy(p => p.RecordedGameParticipantID).ToList())
        {
            if (kept)
            {
                _participants.Remove(line);
                continue;
            }
            line.MoveTo(toUserID);
            kept = true;
            moved++;
        }
        return moved;
    }

    /// <summary>The sides of these teams are left without a team (the teams are going): they keep their recorded name.</summary>
    public void UnlinkTeams(IReadOnlyCollection<int> teamIDs)
    {
        foreach (var side in _teams.Where(t => t.TeamID is int id && teamIDs.Contains(id)))
            side.ClearTeam();
    }

    private void Record(RecordedGameFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.GameVersionID <= 0)
            throw new ArgumentException("A recorded game is of a game version", nameof(facts));
        if (facts.Map?.Length > MapMaxLength || facts.Gametype?.Length > GametypeMaxLength)
            throw new ArgumentException($"The map and gametype are at most {MapMaxLength} characters", nameof(facts));
        GameVersionID = facts.GameVersionID;
        Map = facts.Map;
        Gametype = facts.Gametype;
        PlayedAt = facts.PlayedAt;
        IsPlayoffs = facts.IsPlayoffs;
    }
}

/// <summary>What a recorded game's source (or a commissioner) says of it: its game, map, gametype, when it was played, and
/// whether it was a playoff game.</summary>
public sealed record RecordedGameFacts(int GameVersionID, string? Map = null, string? Gametype = null, DateTime? PlayedAt = null, bool IsPlayoffs = false);

/// <summary>A side of a recorded game, as its source numbers it (1, 2): the season's team it was, when known, its name as
/// recorded, and its result.</summary>
public sealed record RecordedSideFacts(int Side, int? TeamID, string? Name, Outcomes Outcome, int? Score);

/// <summary>A player's line of a recorded game: the user who played it, the side they were on (by number), the gamertag as
/// recorded, and what the source records (null when it doesn't).</summary>
public sealed record RecordedPlayerLine(int UserID, int? Side, string Gamertag, int Kills, int Deaths, int Assists)
{
    public int? Score { get; init; }
    public int? Goals { get; init; }
    public int? CarrierKills { get; init; }
    public int? Betrayals { get; init; }
    public int? Suicides { get; init; }
    public int? HammerKills { get; init; }
    public int? SwordKills { get; init; }
    public int? BallKills { get; init; }
    public int? Punches { get; init; }
}
