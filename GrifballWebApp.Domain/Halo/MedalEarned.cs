using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Halo;

/// <summary>A medal a player earned in a Halo Infinite match. Part of its match: made with its player
/// (<see cref="Match.AddParticipant"/>).</summary>
[AggregatePart(typeof(Match))]
public class MedalEarned : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MedalEarned() { }

    internal MedalEarned(MatchParticipant participant, MedalCount medal)
    {
        MatchParticipant = participant;
        MatchID = participant.MatchID;
        XboxUserID = participant.XboxUserID;
        MedalID = medal.MedalID;
        Count = medal.Count;
        TotalPersonalScoreAwarded = medal.TotalPersonalScoreAwarded;
    }

    public long MedalID { get; internal set; }
    // Composite FK MatchID + XboxUserID
    public Guid MatchID { get; internal set; }
    public long XboxUserID { get; internal set; }
    public int Count { get; private set; }
    public int TotalPersonalScoreAwarded { get; private set; }

    public Medal Medal { get; private set; } = null!;
    public MatchParticipant MatchParticipant { get; private set; } = null!;
}
