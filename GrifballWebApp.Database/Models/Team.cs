#nullable disable
using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A season's team: the root of its roster (<see cref="TeamPlayers"/>, with its captain among them) and its timeslots
/// (<see cref="TeamAvailability"/>). It begins one of two ways: the draft's (<see cref="Drafted"/>), a captain's team the
/// draft fills (<see cref="Draft"/>, <see cref="MovePick"/>, <see cref="TradePick"/>), or the historical archive's
/// (<see cref="Imported"/>), whose roster and record the import keeps as the archive gives them (<see cref="AddPlayer"/>,
/// <see cref="RecordArchive"/>). Nothing outside it sets its fields: only its own methods, and EF (which uses the private
/// setters).
/// </summary>
[AggregateRoot]
public class Team : AuditableEntity
{
    /// <summary>The longest a team name can be (its column's length).</summary>
    public const int TeamNameMaxLength = 30;
    /// <summary>The team name column's length: longer than the app lets a name be (<see cref="TeamNameMaxLength"/>), for
    /// the names of teams imported from the historical archive, which are kept as the sheets wrote them.</summary>
    public const int TeamNameColumnLength = 50;

    /// <summary>For EF. A team begins through <see cref="Drafted"/> or <see cref="Imported"/>.</summary>
    private Team() { }

    /// <summary>
    /// A captain's team of the draft: its roster is the captain alone, placed <paramref name="captainOrder"/> among the
    /// season's captains. A captain can't be set until the team and its roster are saved (each points at the other): save
    /// it, then <see cref="SetCaptain"/>.
    /// </summary>
    public static Team Drafted(int seasonID, string teamName, int captainUserID, int captainOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);
        var team = new Team { SeasonID = seasonID, TeamName = teamName };
        team._teamPlayers.Add(TeamPlayer.On(team, captainUserID, draftCaptainOrder: captainOrder));
        return team;
    }

    /// <summary>
    /// A team of a season imported from the historical archive, named as its sheets name it: the import fills its roster
    /// (<see cref="AddPlayer"/>), names its captain (<see cref="SetCaptain"/>) and records its record (<see cref="RecordArchive"/>).
    /// </summary>
    public static Team Imported(int seasonID, string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);
        return new Team { SeasonID = seasonID, TeamName = teamName };
    }

    /// <summary>
    /// A team of any kind, for tests and the seeder, which seed teams of their own (with a key, a season not saved yet, or
    /// an archive's record) and then change them through its methods.
    /// </summary>
    internal static Team Seed(string teamName = null, int seasonID = 0, int teamID = 0, Season season = null, int? recordedWins = null, int? recordedLosses = null) => new()
    {
        TeamID = teamID,
        TeamName = teamName,
        SeasonID = season is { SeasonID: not 0 } ? season.SeasonID : seasonID,
        Season = season,
        RecordedWins = recordedWins,
        RecordedLosses = recordedLosses,
    };

    // The key is internal: the database makes it, and tests may give one
    public int TeamID { get; internal set; }
    public int SeasonID { get; private set; }
    public Season Season { get; private set; }
    public string TeamName { get; private set; }
    /// <summary>The roster row of the team's captain, one of its players (<see cref="SetCaptain"/>); null: no captain.</summary>
    public int? CaptainID { get; private set; }
    public TeamPlayer Captain { get; private set; }
    /// <summary>The team's regular-season record as its source spreadsheet gives it, for a season whose matches are not in
    /// the app (imported from the historical archive, <see cref="RecordArchive"/>). Null for a team whose record comes from
    /// its matches. Both halves or neither (CK_Event_Teams_RecordBothOrNeither).</summary>
    public int? RecordedWins { get; private set; }
    /// <inheritdoc cref="RecordedWins"/>
    public int? RecordedLosses { get; private set; }

    // Read-only collections: the roster and timeslots change through the team's methods alone; a match takes its teams
    // through its own methods; stat rows and titles are their own code's and the season's (EF uses the fields)
    public IReadOnlyCollection<TeamPlayer> TeamPlayers => _teamPlayers;
    private readonly HashSet<TeamPlayer> _teamPlayers = [];
    public IReadOnlyCollection<SeasonMatch> HomeMatches => _homeMatches;
    private readonly HashSet<SeasonMatch> _homeMatches = [];
    public IReadOnlyCollection<SeasonMatch> AwayMatches => _awayMatches;
    private readonly HashSet<SeasonMatch> _awayMatches = [];
    public IReadOnlyCollection<TeamAvailability> TeamAvailability => _teamAvailability;
    private readonly HashSet<TeamAvailability> _teamAvailability = [];
    public IReadOnlyCollection<PlayerSeasonStat> PlayerSeasonStats => _playerSeasonStats;
    private readonly HashSet<PlayerSeasonStat> _playerSeasonStats = [];
    public IReadOnlyCollection<PlayerChampionship> PlayerChampionships => _playerChampionships;
    private readonly HashSet<PlayerChampionship> _playerChampionships = [];

    /// <summary>A match on its home or away side, fixed up as EF does when both are loaded (SeasonMatch.Seed, for tests).</summary>
    internal void Plays(SeasonMatch match, bool home) => (home ? _homeMatches : _awayMatches).Add(match);

    // The rules that read a team's state: an expression for queries, and the same for a loaded team

    /// <summary>The teams whose record is their source spreadsheet's (<see cref="RecordArchive"/>), not their matches'
    /// (<see cref="IsRecordArchived"/> for a loaded team). Both halves or neither (CK_Event_Teams_RecordBothOrNeither), so
    /// either half set is the sheet's record.</summary>
    public static readonly Expression<Func<Team, bool>> HasArchivedRecord = t => t.RecordedWins != null || t.RecordedLosses != null;

    /// <summary>The team's record is its source spreadsheet's, not its matches' (<see cref="HasArchivedRecord"/> for queries).</summary>
    public bool IsRecordArchived => RecordedWins is not null || RecordedLosses is not null;

    /// <summary>The teams <paramref name="userID"/> captains (<see cref="IsCaptainedBy"/> for a loaded team).</summary>
    public static Expression<Func<Team, bool>> CaptainedBy(int userID) => t => t.Captain.UserID == userID;

    /// <summary><paramref name="userID"/> captains the team: its captain's row must be loaded (with the roster, or <see cref="Captain"/>).</summary>
    public bool IsCaptainedBy(int userID) => CaptainPlayer?.UserID == userID;

    /// <summary>The captain's roster row: the loaded <see cref="Captain"/>, else the roster's row <see cref="CaptainID"/> names.</summary>
    private TeamPlayer CaptainPlayer => Captain ?? (CaptainID is int id ? _teamPlayers.FirstOrDefault(p => p.TeamPlayerID == id) : null);

    private bool IsCaptainRow(TeamPlayer player) => ReferenceEquals(player, Captain) || (CaptainID is int id && player.TeamPlayerID == id);

    /// <summary>The roster row of <paramref name="userID"/>; null when not on the team (the roster must be loaded).</summary>
    public TeamPlayer PlayerOf(int userID) => _teamPlayers.FirstOrDefault(p => p.UserID == userID);

    /// <summary>The draft's picks, for queries: the players but the captain (<see cref="Picks"/> for a loaded team, which
    /// also orders them by round).</summary>
    public static readonly Expression<Func<Team, IEnumerable<TeamPlayer>>> PicksOf = t => t.TeamPlayers.Where(p => p.TeamPlayerID != t.CaptainID);

    /// <summary>The draft's picks: the players but the captain, in their draft rounds' order (<see cref="PicksOf"/> for queries).</summary>
    public IReadOnlyList<TeamPlayer> Picks => _teamPlayers.Where(p => !IsCaptainRow(p)).OrderBy(p => p.DraftRound).ToList();

    /// <summary>Renames the team; refused, nothing changed, for a blank name or one longer than its column.</summary>
    public TeamRenameResult Rename(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return TeamRenameResult.Blank;
        if (teamName.Length > TeamNameColumnLength)
            return TeamRenameResult.TooLong;
        TeamName = teamName;
        return TeamRenameResult.Renamed;
    }

    /// <summary>
    /// Makes <paramref name="userID"/> the team's captain; refused, nothing changed, when not on its roster (the roster
    /// must be loaded). The roster row must be saved first (see <see cref="Drafted"/>).
    /// </summary>
    public TeamCaptainResult SetCaptain(int userID)
    {
        var player = PlayerOf(userID);
        if (player is null)
            return TeamCaptainResult.NotOnRoster;
        Captain = player;
        CaptainID = player.TeamPlayerID is 0 ? null : player.TeamPlayerID;
        return TeamCaptainResult.Appointed;
    }

    /// <summary>The team has no captain now (its players stay).</summary>
    public void ClearCaptain()
    {
        Captain = null;
        CaptainID = null;
    }

    /// <summary>Places the team's captain <paramref name="order"/> among the season's captains; refused without a captain.</summary>
    public TeamCaptainResult OrderCaptain(int order)
    {
        if (CaptainPlayer is not { } captain)
            return TeamCaptainResult.NoCaptain;
        captain.OrderedAsCaptain(order);
        return TeamCaptainResult.Appointed;
    }

    /// <summary>
    /// Puts <paramref name="userID"/> on the roster (the import's roster, a merge); refused, nothing changed, when already on it.
    /// </summary>
    public TeamRosterResult AddPlayer(int userID)
    {
        if (PlayerOf(userID) is not null)
            return TeamRosterResult.AlreadyOnTeam;
        _teamPlayers.Add(TeamPlayer.On(this, userID));
        return TeamRosterResult.Done;
    }

    /// <summary>
    /// The draft's pick of <paramref name="userID"/>: on the roster in the round after its last pick (the first, for the
    /// first pick). Refused, nothing changed, when already on it.
    /// </summary>
    public TeamRosterResult Draft(int userID)
    {
        if (PlayerOf(userID) is not null)
            return TeamRosterResult.AlreadyOnTeam;
        var round = Picks.Select(p => p.DraftRound).Max() + 1 ?? 1;
        _teamPlayers.Add(TeamPlayer.On(this, userID, draftRound: round));
        return TeamRosterResult.Done;
    }

    /// <summary>
    /// Takes <paramref name="userID"/> off the roster, the captain too (who then is no captain). The other players' draft
    /// rounds stay as they are: the draft's removal is <see cref="Undraft"/>.
    /// </summary>
    public TeamRosterResult RemovePlayer(int userID)
    {
        var player = PlayerOf(userID);
        if (player is null)
            return TeamRosterResult.NotOnTeam;
        if (IsCaptainRow(player))
            ClearCaptain();
        _teamPlayers.Remove(player);
        return TeamRosterResult.Done;
    }

    /// <summary>
    /// The draft takes a pick off the roster: the picks after it move up a round. Refused, nothing changed, for a player
    /// not on the team or its captain (<see cref="TeamRosterResult.Captain"/>: the captain goes with the team).
    /// </summary>
    public TeamRosterResult Undraft(int userID)
    {
        var player = PlayerOf(userID);
        if (player is null)
            return TeamRosterResult.NotOnTeam;
        if (IsCaptainRow(player))
            return TeamRosterResult.Captain;
        var picks = Picks.ToList();
        picks.Remove(player);
        _teamPlayers.Remove(player);
        Renumber(picks);
        return TeamRosterResult.Done;
    }

    /// <summary>
    /// The draft moves a pick to <paramref name="round"/> (1 is the first) of the team's picks, the others shifting round
    /// it. Refused, nothing changed, for a player not among the picks, or a round past the picks.
    /// </summary>
    public TeamRosterResult MovePick(int userID, int round)
    {
        var picks = Picks.ToList();
        var player = picks.FirstOrDefault(p => p.UserID == userID);
        if (player is null)
            return PlayerOf(userID) is null ? TeamRosterResult.NotOnTeam : TeamRosterResult.Captain;
        if (round < 1 || round > picks.Count)
            return TeamRosterResult.RoundOutOfRange;
        picks.Remove(player);
        picks.Insert(round - 1, player);
        Renumber(picks);
        return TeamRosterResult.Done;
    }

    /// <summary>
    /// A trade in the draft: the pick <paramref name="userID"/> moves to <paramref name="to"/> (another team of the season,
    /// saved), at <paramref name="round"/> of its picks; the picks after it here move up a round. Refused, nothing changed,
    /// for a player not among this team's picks, one <paramref name="to"/> has already, or a round past its picks and the new one.
    /// </summary>
    public TeamRosterResult TradePick(int userID, Team to, int round)
    {
        ArgumentNullException.ThrowIfNull(to);
        if (ReferenceEquals(to, this))
            return MovePick(userID, round);
        var picks = Picks.ToList();
        var player = picks.FirstOrDefault(p => p.UserID == userID);
        if (player is null)
            return PlayerOf(userID) is null ? TeamRosterResult.NotOnTeam : TeamRosterResult.Captain;
        if (to.PlayerOf(userID) is not null)
            return TeamRosterResult.AlreadyOnTeam;
        var theirs = to.Picks.ToList();
        if (round < 1 || round > theirs.Count + 1)
            return TeamRosterResult.RoundOutOfRange;

        picks.Remove(player);
        _teamPlayers.Remove(player);
        Renumber(picks);
        player.MoveTo(to);
        to._teamPlayers.Add(player);
        theirs.Insert(round - 1, player);
        Renumber(theirs);
        return TeamRosterResult.Done;
    }

    private static void Renumber(List<TeamPlayer> picks)
    {
        foreach (var (pick, index) in picks.Select((p, i) => (p, i)))
            pick.InRound(index + 1);
    }

    /// <summary>
    /// A user merge: <paramref name="fromUserID"/>'s place on the roster becomes <paramref name="toUserID"/>'s. Where both
    /// are on it the target keeps its own row, which becomes the captain's if the source's was (with its captain order when
    /// it has none), and the source's goes. False: the source isn't on the team.
    /// </summary>
    public bool MergePlayer(int fromUserID, int toUserID)
    {
        var from = PlayerOf(fromUserID);
        if (from is null)
            return false;
        var to = PlayerOf(toUserID);
        if (to is null)
        {
            from.ReassignUser(toUserID);
            return true;
        }
        if (IsCaptainRow(from))
        {
            Captain = to;
            CaptainID = to.TeamPlayerID is 0 ? null : to.TeamPlayerID;
            if (to.DraftCaptainOrder is null)
                to.OrderedAsCaptain(from.DraftCaptainOrder);
        }
        _teamPlayers.Remove(from);
        return true;
    }

    /// <summary>The team's record as its source spreadsheet gives it (a season whose matches are not in the app).</summary>
    public void RecordArchive(int wins, int losses)
    {
        RecordedWins = wins;
        RecordedLosses = losses;
    }

    /// <summary>The team has no record of the spreadsheet's: its record is its matches'.</summary>
    public void ClearArchivedRecord()
    {
        RecordedWins = null;
        RecordedLosses = null;
    }

    /// <summary>
    /// A roster row as the draft or the archive left it (its draft numbers given), for tests, which seed rosters; the user
    /// is <paramref name="user"/> when given (a user not saved yet).
    /// </summary>
    internal TeamPlayer SeedPlayer(int userID = 0, User user = null, int? draftCaptainOrder = null, int? draftRound = null, int? draftPick = null)
    {
        var player = TeamPlayer.Seed(TeamID, userID, this, user, draftCaptainOrder, draftRound, draftPick);
        _teamPlayers.Add(player);
        return player;
    }

    /// <summary>
    /// A copy of the team, its roster and its timeslots, for another season. A captain can't be set until the copy is
    /// saved: <paramref name="captainUserID"/> is the captain's user (null: none), for the caller to <see cref="SetCaptain"/> then.
    /// </summary>
    public Team Copy(out int? captainUserID)
    {
        captainUserID = CaptainPlayer?.UserID;
        var copy = new Team { TeamName = TeamName };
        foreach (var player in _teamPlayers)
            copy._teamPlayers.Add(player.Copy(copy));
        foreach (var availability in _teamAvailability)
            copy._teamAvailability.Add(availability.Copy(copy));
        return copy;
    }
}

/// <summary>What <see cref="Team.Rename"/> did.</summary>
public enum TeamRenameResult
{
    Renamed,
    Blank,
    /// <summary>Longer than the name's column (<see cref="Team.TeamNameColumnLength"/>).</summary>
    TooLong,
}

/// <summary>What <see cref="Team.SetCaptain"/> or <see cref="Team.OrderCaptain"/> did.</summary>
public enum TeamCaptainResult
{
    Appointed,
    /// <summary>The player isn't on the team's roster: a captain is one of its players.</summary>
    NotOnRoster,
    /// <summary>The team has no captain to place.</summary>
    NoCaptain,
}

/// <summary>What a change of a team's roster did, or why it refused.</summary>
public enum TeamRosterResult
{
    Done,
    AlreadyOnTeam,
    NotOnTeam,
    /// <summary>The player is the team's captain, not one of its picks.</summary>
    Captain,
    /// <summary>The round is past the team's picks (or before the first).</summary>
    RoundOutOfRange,
}
