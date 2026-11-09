namespace GrifballWebApp.Domain.Models;

/// <summary>A team of a Halo Infinite match. Part of its match: made only by it (<see cref="Match.FromHalo"/>), with its
/// players added through it (<see cref="Match.AddParticipant"/>).</summary>
[AggregatePart(typeof(Match))]
public class MatchTeam : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MatchTeam() { }

    internal MatchTeam(Match match, int teamID, int score, Outcomes outcome)
    {
        Match = match;
        MatchID = match.MatchID;
        TeamID = teamID;
        Score = score;
        Outcome = outcome;
    }

    public Guid MatchID { get; internal set; }
    public Match Match { get; private set; } = null!;
    public int TeamID { get; internal set; }
    public int Score { get; private set; }
    public Outcomes Outcome { get; private set; }

    // Read-only: only the match adds a player (EF uses the field)
    public IReadOnlyCollection<MatchParticipant> MatchParticipants => _matchParticipants;
    private readonly List<MatchParticipant> _matchParticipants = [];

    /// <summary>A player who ended the match on this team (<see cref="Match.AddParticipant"/>, which checks them).</summary>
    internal MatchParticipant Add(long xboxUserID, MatchParticipantStats stats, IReadOnlyCollection<MedalCount> medals, XboxUser? xboxUser)
    {
        var participant = new MatchParticipant(this, xboxUserID, stats, medals, xboxUser);
        _matchParticipants.Add(participant);
        return participant;
    }
}

public enum Outcomes
{
    Tie = 1, // Nevermind now seeing 1 is tie... confused what happened before
    // Does not appear that there is a 0 or 1? Ties also show as won
    Won = 2,
    Lost = 3,
    DidNotFinish = 4,
}
