namespace GrifballWebApp.Application.HaloInfinite;

/// <summary>What a run of the game variant backfill did (<see cref="IDataPullService.BackfillGameVariants"/>).</summary>
/// <param name="Missing">Matches without a game variant when it started.</param>
/// <param name="Attempted">Of those, the ones it looked up this run.</param>
/// <param name="Filled">The ones it gave their variant.</param>
/// <param name="NoVariant">The ones Halo gave without a variant: left as they were.</param>
/// <param name="Failed">The ones Halo didn't give: tried again on a later run.</param>
/// <param name="Versions">The variant versions it filled in.</param>
public sealed record VariantBackfillResult(int Missing, int Attempted, int Filled, int NoVariant, IReadOnlyList<Guid> Failed,
    IReadOnlyCollection<Guid>? Versions = null)
{
    /// <summary>Matches still without a variant after the run.</summary>
    public int Left => Missing - Filled;

    public string Summary
    {
        get
        {
            var text = $"{Filled} of {Attempted} matches given their game variant, {Left} left";
            if (Failed.Count > 0)
                text += $"; {Failed.Count} Halo didn't give (a later run tries them again)";
            if (NoVariant > 0)
                text += $"; {NoVariant} without a variant";
            return text;
        }
    }
}

/// <summary>
/// Pulls players' games from Halo Infinite and saves them: the Halo client's port. Infrastructure's DataPullService
/// implements it; what takes the client's own types (a match's stats) is that class's, not the port's.
/// </summary>
public interface IDataPullService
{
    /// <summary>The most Halo calls a pull between dates makes for one player: finding the pages, then reading them.</summary>
    const int DefaultMaxCallsBack = 100;

    Task<MatchPullResult> DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default);
    Task<MatchPullResult> DownloadMatchesBetween(List<long> xboxIDs, DateTime from, DateTime to, int maxCalls = DefaultMaxCallsBack, CancellationToken ct = default);
    Task GetAndSaveMatch(Guid matchID);
    Task DownloadMedals();
    Task<VariantBackfillResult> BackfillGameVariants(int batchSize, Action<int, int>? progress = null, CancellationToken ct = default);
}

/// <summary>What a pull of players' recent games did (<see cref="IDataPullService.DownloadRecentMatchesForPlayers"/>).</summary>
/// <param name="Players">The players looked through.</param>
/// <param name="FailedHistories">The players a page of whose match history Halo didn't give: their games there are missing.</param>
/// <param name="MatchesFound">The Grifball games in their histories.</param>
/// <param name="MatchesAlreadySaved">Of those, the ones saved before.</param>
/// <param name="MatchesFetched">The new ones fetched and saved.</param>
/// <param name="FailedMatches">The new ones Halo didn't give (after the gateway client's retries, or no answer): not saved.</param>
/// <param name="MatchesNotTeamGames">The new ones fetched but not saved: not team games (a Grifball variant played free for all).</param>
/// <param name="CappedHistories">Pulled between dates: the players whose search took more than its most calls, so their games then may be missing.</param>
public sealed record MatchPullResult(int Players, IReadOnlyList<string> FailedHistories, int MatchesFound, int MatchesAlreadySaved, int MatchesFetched,
    IReadOnlyList<Guid> FailedMatches, int MatchesNotTeamGames = 0, IReadOnlyList<string>? CappedHistories = null)
{
    public static readonly MatchPullResult Empty = new(0, [], 0, 0, 0, []);

    /// <summary>Everything found was fetched: no history page or game missing.</summary>
    public bool Complete => FailedHistories.Count == 0 && FailedMatches.Count == 0;

    public string Summary
    {
        get
        {
            var text = $"{Players} players, {MatchesFound} Grifball games found, {MatchesAlreadySaved} saved before, {MatchesFetched} new saved";
            if (FailedMatches.Count > 0)
                text += $"; {FailedMatches.Count} games Halo didn't give, not saved (the next pull tries them again)";
            if (FailedHistories.Count > 0)
                text += $"; {FailedHistories.Count} players' match histories incomplete";
            if (MatchesNotTeamGames > 0)
                text += $"; {MatchesNotTeamGames} not team games, not saved";
            if (CappedHistories is { Count: > 0 } capped)
                text += $"; {capped.Count} players' histories too long to search back to the season";
            return text;
        }
    }
}
