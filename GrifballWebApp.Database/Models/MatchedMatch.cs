﻿

namespace GrifballWebApp.Database.Models;
public class MatchedMatch
{
    public MatchedMatch()
    {
        MatchedWinnerVotes = new HashSet<MatchedWinnerVote>();
        MatchedKickVotes = new HashSet<MatchedKickVote>();
    }
    public int Id { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public MatchedTeam HomeTeam { get; set; } = null!;
    public MatchedTeam AwayTeam { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
    public Match? Match { get; set; }
    public Guid? MatchID { get; set; }
    public ulong? ThreadID { get; set; }
    public ulong? VoteMessageID { get; set; }
    public ICollection<MatchedWinnerVote> MatchedWinnerVotes { get; set; }
    public ICollection<MatchedKickVote> MatchedKickVotes { get; set; }
}
