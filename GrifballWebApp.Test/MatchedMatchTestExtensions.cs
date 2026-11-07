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

    public static MatchedMatch Archived(this MatchedMatch match)
    {
        match.RecordArchived();
        return match;
    }
}
