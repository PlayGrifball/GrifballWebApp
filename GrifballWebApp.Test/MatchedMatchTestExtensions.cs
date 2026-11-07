using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>A test's matched match in a state, reached through the match's own methods (its state has private setters).</summary>
internal static class MatchedMatchTestExtensions
{
    /// <summary>A key, as the database would give (tests that never save it).</summary>
    public static MatchedMatch WithId(this MatchedMatch match, int id)
    {
        match.Id = id;
        return match;
    }

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

    /// <summary>An archive game of the season; its recorded game a bare one unless given (an archive game always has one).</summary>
    public static MatchedMatch Archived(this MatchedMatch match, int matchmakingSeasonID, DateTime startedAt, RecordedGame? game = null)
    {
        match.RecordArchived(game ?? new RecordedGame { SourceKey = $"test/{Guid.NewGuid():N}", GameVersionID = 1 }, matchmakingSeasonID, startedAt);
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
