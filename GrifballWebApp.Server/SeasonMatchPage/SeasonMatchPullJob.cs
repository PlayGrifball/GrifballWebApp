using GrifballWebApp.Database;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GrifballWebApp.Server.SeasonMatchPage;

/// <param name="RequestedByUserID">Who queued it (it runs as them); null for none.</param>
/// <param name="BackToSeasonStart">Page back through each player's history to the start of the match's season (up to
/// <see cref="DataPullService.DefaultMaxPagesBack"/> pages), rather than the most recent 11 pages.</param>
public sealed record SeasonMatchPull(int SeasonMatchID, int? RequestedByUserID = null, bool BackToSeasonStart = false) : IRequestedByUser;

/// <summary>
/// Pulls the recent custom games of a season match's players from Halo Infinite (up to 11 pages of 25 each; or, asked to,
/// back to the start of the match's season: <see cref="SeasonMatchPull.BackToSeasonStart"/>), so the page's possible matches
/// include them: started from the season match page (<see cref="OnDemandJobs"/>). One run at
/// a time across season matches, which keeps the Halo calls in step.
/// </summary>
[DisallowConcurrentExecution]
// Also for a firing that names no group (Quartz reads it for manual runs): the Halo pulls stay one at a time
[ExecutionGroup(JobsServiceCollectionExtensions.HaloMatchPulls)]
public sealed class SeasonMatchPullJob : IJob<SeasonMatchPull>
{
    public const string Description = "Pulls a season match's players' recent games from Halo Infinite (from its page)";

    /// <summary>A season match's run: one queued or running at a time.</summary>
    public static string RunName(int seasonMatchID) => $"season-match-{seasonMatchID}";

    private readonly SeasonMatchService _seasonMatches;
    private readonly IDataPullService _dataPull;

    public SeasonMatchPullJob(SeasonMatchService seasonMatches, IDataPullService dataPull)
    {
        _seasonMatches = seasonMatches;
        _dataPull = dataPull;
    }

    /// <summary>
    /// A pull's run: succeeded when everything found was fetched; failed when Halo didn't give some of it (what it gave
    /// is saved all the same), so the Jobs page shows the games missing rather than a success.
    /// </summary>
    /// <param name="scope">What the pull reached back to, before its summary; none for the recent games.</param>
    public static JobRunReport Report(MatchPullResult result, string? scope = null) =>
        (result.Complete ? JobRunReport.Succeeded(scope + result.Summary) : JobRunReport.Failed(scope + result.Summary))
            .With("players", result.Players)
            .With("matchesFound", result.MatchesFound)
            .With("matchesFetched", result.MatchesFetched)
            .With("matchesFailed", result.FailedMatches.Count)
            .With("historiesFailed", result.FailedHistories.Count)
            .With("historiesCapped", result.CappedHistories?.Count ?? 0);

    public async ValueTask Execute(IJobExecutionContext context, SeasonMatchPull input, CancellationToken ct = default)
    {
        var players = await _seasonMatches.PlayerXboxIDs(input.SeasonMatchID, ct);
        if (input.BackToSeasonStart && await _seasonMatches.SeasonWindow(input.SeasonMatchID, ct) is { } window)
        {
            var backTo = window.From;
            context.ReportProgress(0, $"Looking through {players.Count} players' games back to {backTo:d MMM yyyy}");
            var back = await _dataPull.DownloadMatchesBackTo(players, backTo, ct: ct);
            context.Result = Report(back, $"Back to {backTo:yyyy-MM-dd}: ");
            return;
        }
        context.ReportProgress(0, $"Looking through {players.Count} players' recent games");
        var result = await _dataPull.DownloadRecentMatchesForPlayers(players, ct: ct);
        context.Result = Report(result);
    }
}

/// <summary>
/// Pulls the latest custom games (one page of 25) of every player on a team in a season under way, every 30 minutes by
/// default, so their season matches' possible matches are there when the page opens. The season match page's pull
/// (<see cref="SeasonMatchPullJob"/>) looks further back.
/// </summary>
// Also for "run now" on the Jobs page, whatever its stored triggers carry
[ExecutionGroup(JobsServiceCollectionExtensions.HaloMatchPulls)]
public sealed class ActiveSeasonPullJob : IJob
{
    public static readonly JobKey Key = new("active-season-pulls", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Pulls the latest games of the players in seasons under way from Halo Infinite",
        // Missed while no worker was up: once, when one is
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(5))
            .WithExecutionGroup(JobsServiceCollectionExtensions.HaloMatchPulls)
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(30)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    private readonly GrifballContext _context;
    private readonly IDataPullService _dataPull;
    private readonly TimeProvider _time;

    public ActiveSeasonPullJob(GrifballContext context, IDataPullService dataPull, TimeProvider time)
    {
        _context = context;
        _dataPull = dataPull;
        _time = time;
    }

    /// <summary>The players with an Xbox account on a team in a season under way (started, not ended).</summary>
    public Task<List<long>> PlayersInActiveSeasons(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        return _context.TeamPlayers
            .Where(tp => tp.Team.Season.SeasonStart <= now && now < tp.Team.Season.SeasonEnd)
            .Where(tp => tp.User.XboxUserID != null)
            .Select(tp => tp.User.XboxUserID!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default)
    {
        var players = await PlayersInActiveSeasons(ct);
        if (players.Count == 0)
        {
            context.Result = JobRunReport.Skipped("No season under way");
            return;
        }
        var result = await _dataPull.DownloadRecentMatchesForPlayers(players, startPage: 0, endPage: 0, ct: ct);
        context.Result = SeasonMatchPullJob.Report(result);
    }
}
