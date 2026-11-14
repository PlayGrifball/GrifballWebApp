using GrifballWebApp.Application.Jobs;

namespace GrifballWebApp.Application.XboxAccounts;

/// <summary>
/// The pull of an Xbox account's recent games from Halo Infinite, run in the background (Infrastructure's XboxAccountPulls,
/// over its on-demand job): queued from the account's page.
/// </summary>
public interface IXboxAccountPulls
{
    /// <summary>Queues a pull for the account, unless one is queued or running already. Returns where it is.</summary>
    /// <param name="xboxUserID">The account whose games are pulled.</param>
    /// <param name="requestedByUserID">Who asked: the pull runs as them.</param>
    /// <param name="ct">Stops the queueing, not a pull already queued.</param>
    Task<OnDemandJobStatusDto> Queue(long xboxUserID, int? requestedByUserID, CancellationToken ct = default);

    /// <summary>Where the account's pull is: queued, running, or idle with its last run.</summary>
    Task<OnDemandJobStatusDto> Status(long xboxUserID, CancellationToken ct = default);
}
