using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>A test's matched match in a state, reached through the match's own methods (its state has private setters).</summary>
internal static class MatchedMatchTestExtensions
{
    public static MatchedMatch Reported(this MatchedMatch match, Guid matchID)
    {
        match.Report(matchID);
        return match;
    }

    public static MatchedMatch Canceled(this MatchedMatch match)
    {
        match.Cancel();
        return match;
    }

    public static MatchedMatch Archived(this MatchedMatch match, int matchmakingSeasonID, DateTime startedAt)
    {
        match.RecordArchived(matchmakingSeasonID, startedAt);
        return match;
    }

    /// <summary>Its thread made and, when given, its vote message posted in it, as the outbox does.</summary>
    public static MatchedMatch Announced(this MatchedMatch match, ulong? threadID, ulong? voteMessageID = null)
    {
        if (threadID is { } thread)
            match.AttachThread(thread);
        if (voteMessageID is { } vote)
            match.PostVoteMessage(vote);
        return match;
    }
}
