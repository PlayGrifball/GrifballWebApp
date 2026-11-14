using Quartz;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

/// <param name="RequestedByUserID">Who queued it (it runs as them); null for none.</param>
public sealed record XboxAccountPull(long XboxUserID, int? RequestedByUserID = null) : IRequestedByUser;

/// <summary>The Xbox account pulls (<see cref="IXboxAccountPulls"/>) as runs of <see cref="XboxAccountPullJob"/>, one per account at a time.</summary>
public sealed class XboxAccountPulls(OnDemandJobs jobs) : IXboxAccountPulls
{
    public Task<OnDemandJobStatusDto> Queue(long xboxUserID, int? requestedByUserID, CancellationToken ct = default)
        => jobs.Enqueue<XboxAccountPullJob, XboxAccountPull>(XboxAccountPullJob.RunName(xboxUserID), new XboxAccountPull(xboxUserID, requestedByUserID),
            $"Xbox account {xboxUserID}", JobsServiceCollectionExtensions.HaloMatchPulls, ct);

    public Task<OnDemandJobStatusDto> Status(long xboxUserID, CancellationToken ct = default)
        => jobs.Status<XboxAccountPullJob>(XboxAccountPullJob.RunName(xboxUserID), ct);
}

/// <summary>
/// Pulls an Xbox account's recent custom games from Halo Infinite (up to 11 pages of 25), as the season match page's pull does
/// for its players: started from the account's page (<see cref="OnDemandJobs"/>). One Halo pull at a time across them all.
/// </summary>
[DisallowConcurrentExecution]
// Also for a firing that names no group (Quartz reads it for manual runs): the Halo pulls stay one at a time
[ExecutionGroup(JobsServiceCollectionExtensions.HaloMatchPulls)]
public sealed class XboxAccountPullJob : IJob<XboxAccountPull>
{
    public const string Description = "Pulls an Xbox account's recent games from Halo Infinite (from its page)";

    /// <summary>An account's run: one queued or running at a time.</summary>
    public static string RunName(long xboxUserID) => $"xbox-account-{xboxUserID}";

    private readonly IDataPullService _dataPull;

    public XboxAccountPullJob(IDataPullService dataPull)
    {
        _dataPull = dataPull;
    }

    public async ValueTask Execute(IJobExecutionContext context, XboxAccountPull input, CancellationToken cancellationToken = default)
    {
        context.ReportProgress(0, "Looking through the account's recent games");
        var result = await _dataPull.DownloadRecentMatchesForPlayers([input.XboxUserID], ct: cancellationToken);
        context.Result = SeasonMatchPullJob.Report(result);
    }
}
