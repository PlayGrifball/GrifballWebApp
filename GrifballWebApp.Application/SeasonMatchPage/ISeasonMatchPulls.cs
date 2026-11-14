using GrifballWebApp.Application.Jobs;

namespace GrifballWebApp.Application.SeasonMatchPage;

/// <summary>
/// The pull of a season match's players' recent games from Halo Infinite, run in the background (Infrastructure's
/// SeasonMatchPulls, over its on-demand job): queued from the season match page and the agents' tools.
/// </summary>
public interface ISeasonMatchPulls
{
    /// <summary>Queues a pull for the match, unless one is queued or running already. Returns where it is.</summary>
    /// <param name="requestedByUserID">Who asked: the pull runs as them.</param>
    /// <param name="backToSeasonStart">Back through each player's history to the season's start, not just their recent games.</param>
    Task<OnDemandJobStatusDto> Queue(int seasonMatchID, int? requestedByUserID, bool backToSeasonStart, CancellationToken ct = default);

    /// <summary>Where the match's pull is: queued, running, or idle with its last run.</summary>
    Task<OnDemandJobStatusDto> Status(int seasonMatchID, CancellationToken ct = default);
}
