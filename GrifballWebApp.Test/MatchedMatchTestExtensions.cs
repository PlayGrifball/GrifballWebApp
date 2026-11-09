using GrifballWebApp.Domain.Models;

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

    /// <summary>An archive game of the season, these users on its sides, as the import makes one; its recorded game a bare one
    /// unless given (an archive game always has one).</summary>
    public static MatchedMatch ArchiveGame(int matchmakingSeasonID, DateTime startedAt, IEnumerable<int> home, IEnumerable<int> away, RecordedGame? game = null)
    {
        var match = MatchedMatch.Archive(game ?? RecordedGame.Seed($"test/{Guid.NewGuid():N}"), matchmakingSeasonID, startedAt);
        match.SetArchivedRoster(home, away);
        return match;
    }

    /// <summary>A player kicked out of its match before the test, as a majority's vote would (<see cref="MatchedPlayer.Kick"/>).</summary>
    public static MatchedPlayer AlreadyKicked(this MatchedPlayer player)
    {
        player.Kick();
        return player;
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
