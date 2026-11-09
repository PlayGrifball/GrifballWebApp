#nullable disable

namespace GrifballWebApp.Domain.Models;

/// <summary>A player on a team's roster: part of its <see cref="Models.Team"/>, which makes and changes it (the draft,
/// the import's roster, a merge). Read through TeamQueries.Rosters.</summary>
[AggregatePart(typeof(Team))]
public class TeamPlayer : AuditableEntity
{
    /// <summary>For EF. A player is put on a roster through its team.</summary>
    private TeamPlayer() { }

    /// <summary><paramref name="userID"/> on <paramref name="team"/>'s roster, which may be new (not saved yet).</summary>
    internal static TeamPlayer On(Team team, int userID, int? draftRound = null, int? draftCaptainOrder = null) => new()
    {
        Team = team,
        TeamID = team.TeamID,
        UserID = userID,
        DraftRound = draftRound,
        DraftCaptainOrder = draftCaptainOrder,
    };

    /// <summary>A roster row of saved rows by their keys, or of entities not saved yet (the navigations): for tests, which
    /// seed rosters as the draft or the archive left them.</summary>
    internal static TeamPlayer Seed(int teamID = 0, int userID = 0, Team team = null, User user = null, int? draftCaptainOrder = null,
        int? draftRound = null, int? draftPick = null) => new()
    {
        TeamID = team is { TeamID: not 0 } ? team.TeamID : teamID,
        Team = team,
        UserID = user is { Id: not 0 } ? user.Id : userID,
        User = user,
        DraftCaptainOrder = draftCaptainOrder,
        DraftRound = draftRound,
        DraftPick = draftPick,
    };

    // The key is internal: the database makes it
    public int TeamPlayerID { get; internal set; }
    public int TeamID { get; private set; }
    public Team Team { get; private set; }
    public int UserID { get; private set; }
    public User User { get; private set; }
    /// <summary>The team this player captains (its own, <see cref="Team.SetCaptain"/>); null: not a captain.</summary>
    public Team CaptainTeam { get; private set; }
    /// <summary>Where the player's team is among the season's captains (<see cref="Team.OrderCaptain"/>), for a captain.</summary>
    public int? DraftCaptainOrder { get; private set; }
    /// <summary>The draft round the player was picked in (<see cref="Team.Draft"/>); null: not drafted (a captain, an import).</summary>
    public int? DraftRound { get; private set; }
    public int? DraftPick { get; private set; }

    internal void InRound(int round) => DraftRound = round;

    internal void OrderedAsCaptain(int? order) => DraftCaptainOrder = order;

    /// <summary>The player moves to <paramref name="team"/> (saved), keeping the row (a trade).</summary>
    internal void MoveTo(Team team)
    {
        Team = team;
        TeamID = team.TeamID;
    }

    /// <summary>A user merge: the row is <paramref name="userID"/>'s now.</summary>
    internal void ReassignUser(int userID) => UserID = userID;

    /// <summary>A copy of the row for <paramref name="newTeam"/> (<see cref="Team.Copy"/>): not its captain until the copy says so.</summary>
    internal TeamPlayer Copy(Team newTeam) => new()
    {
        Team = newTeam,
        User = User,
        UserID = UserID,
        DraftCaptainOrder = DraftCaptainOrder,
        DraftRound = DraftRound,
        DraftPick = DraftPick,
    };
}
