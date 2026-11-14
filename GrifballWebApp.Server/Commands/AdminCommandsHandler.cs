using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Scheduler;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using Quartz;

namespace GrifballWebApp.Server.Commands;

/// <summary>How <c>/admin match forfeit</c> forfeits: which side, or both.</summary>
public enum ForfeitSide
{
    Home,
    Away,
    Both,
}

/// <summary>
/// /admin and /reschedule pending|decide: the commissioner's and sysadmin's actions, each held to the role its endpoint
/// names (some are the commissioner's alone, as on the site) and then to the shared service's rules. Usable in any channel:
/// every reply is ephemeral, seen only by the caller. <see cref="AdminCommands"/> only wraps its context and calls these.
/// </summary>
public class AdminCommandsHandler
{
    public const string Done = "Done.";

    private readonly DiscordCaller _caller;
    private readonly CommandLookups _lookups;
    private readonly RescheduleService _reschedules;
    private readonly SeasonMatchService _seasonMatches;
    private readonly MatchPlannerService _matchPlanner;
    private readonly IBracketService _brackets;
    private readonly TeamService _teams;
    private readonly ScheduleRecommendations _recommendations;
    private readonly OnDemandJobs _onDemand;
    private readonly JobsService _jobs;
    private readonly GamertagClaimService _claims;
    private readonly IDataPullService _dataPull;
    private readonly UrlService _urls;

    public AdminCommandsHandler(DiscordCaller caller, CommandLookups lookups, RescheduleService reschedules, SeasonMatchService seasonMatches,
        MatchPlannerService matchPlanner, IBracketService brackets, TeamService teams, ScheduleRecommendations recommendations,
        OnDemandJobs onDemand, JobsService jobs, GamertagClaimService claims, IDataPullService dataPull, UrlService urls)
    {
        _caller = caller;
        _lookups = lookups;
        _reschedules = reschedules;
        _seasonMatches = seasonMatches;
        _matchPlanner = matchPlanner;
        _brackets = brackets;
        _teams = teams;
        _recommendations = recommendations;
        _onDemand = onDemand;
        _jobs = jobs;
        _claims = claims;
        _dataPull = dataPull;
        _urls = urls;
    }

    /// <summary>The reply deferred ephemerally, then the caller when they have one of <paramref name="roles"/>; null otherwise, told why.</summary>
    private async Task<CallerUser?> Staff(IDiscordInteractionContext context, IReadOnlyCollection<string> roles)
    {
        await context.Defer(ephemeral: true);
        return await _caller.Require(context, roles);
    }

    /// <summary>Runs a season match action, then says it is done; its refusal (SeasonMatchActionNotAllowedException) is the reply
    /// instead, as the site's 400 is (DiscordInteractionPipeline).</summary>
    private static async Task Act(IDiscordInteractionContext context, Func<Task> action, string done = Done)
    {
        await action();
        await context.Reply(done);
    }

    public async Task Dashboard(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is null)
            return;
        var pending = await _reschedules.GetPendingReschedulesAsync();
        var overdue = await _reschedules.GetOverdueMatchesAsync();
        var embed = CommandReplies.Embed("Commissioner dashboard", _urls.Page("commissioner-dashboard"),
            $"{pending.Count} pending reschedules · {overdue.Count} overdue matches · {overdue.Count(m => m.HoursOverdue > 72)} over 3 days");
        await context.Reply(embed);
    }

    public async Task Overdue(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is null)
            return;
        var overdue = await _reschedules.GetOverdueMatchesAsync();
        var lines = overdue
            .OrderByDescending(x => x.HoursOverdue)
            .Select(x => $"{$"{x.HomeCaptain} vs {x.AwayCaptain}".LinkMarkdown(_urls.SeasonMatch(x.SeasonMatchID))} · {x.HoursOverdue} hours overdue")
            .ToList();
        await context.Reply(CommandReplies.Embed("Overdue matches", _urls.Page("commissioner-dashboard"), CommandReplies.Lines(lines, "No match is overdue.")));
    }

    public async Task PendingReschedules(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is null)
            return;
        var pending = await _reschedules.GetPendingReschedulesAsync();
        var lines = pending
            .Select(x => $"**#{x.MatchRescheduleID}** {$"{x.HomeCaptain} vs {x.AwayCaptain}".LinkMarkdown(_urls.SeasonMatch(x.SeasonMatchID))}"
                + $" · {(x.NewScheduledTime is { } time ? time.DiscordTimeEmbed() : "no time given")} · by {x.RequestedByGamertag}: {x.Reason}")
            .ToList();
        await context.Reply(CommandReplies.Embed("Pending reschedules", _urls.Page("commissioner-dashboard"), CommandReplies.Lines(lines, "No reschedule is pending.")));
    }

    /// <summary>Approves or rejects a reschedule request, as the dashboard does.</summary>
    public async Task DecideReschedule(IDiscordInteractionContext context, int rescheduleID, bool approve, string? notes)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is not { } caller)
            return;
        try
        {
            await _reschedules.ProcessRescheduleAsync(rescheduleID, new ProcessRescheduleDto { Approved = approve, CommissionerNotes = notes }, caller.UserID);
        }
        catch (ArgumentException ex)
        {
            await context.Reply(ex.Message);
            return;
        }
        await context.Reply(approve ? "Approved." : "Rejected.");
    }

    public async Task ReportGame(IDiscordInteractionContext context, string match, string gameID, int? replaces)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match) is not int id)
            return;
        if (!Guid.TryParse(gameID.Trim(), out var matchID) || matchID == Guid.Empty)
        {
            await context.Reply(MatchCommandsHandler.NoSuchGame);
            return;
        }
        await Act(context, () => _seasonMatches.ReportMatch(id, matchID, replaces), "Reported.");
    }

    public async Task Forfeit(IDiscordInteractionContext context, string match, ForfeitSide side)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match) is not int id)
            return;
        await Act(context, () => side switch
        {
            ForfeitSide.Home => _seasonMatches.HomeForfeit(id),
            ForfeitSide.Away => _seasonMatches.AwayForfeit(id),
            _ => _seasonMatches.DoubleForfeit(id),
        }, $"Forfeited ({side.ToString().ToLowerInvariant()}).");
    }

    public async Task UndoResult(IDiscordInteractionContext context, string match)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match) is not int id)
            return;
        await Act(context, () => _seasonMatches.UndoResult(id), "The result is undone.");
    }

    /// <summary>Queues a pull of the match's players' recent games, as the match page does.</summary>
    public async Task SetTime(IDiscordInteractionContext context, string match, string time)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match) is not int id)
            return;
        if (!CommandLookups.TryParseUtc(time, out var when))
        {
            await context.Reply("I couldn't read that time. Use UTC like 2026-11-20 20:00.");
            return;
        }
        await _matchPlanner.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = id, Time = when });
        await context.Reply($"The match is set for {when.DiscordTimeEmbed()}.");
    }

    public async Task GenerateMatches(IDiscordInteractionContext context, string? season, int homeMatchesPerTeam, int bestOf)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season) is not int id)
            return;
        if (homeMatchesPerTeam <= 0 || bestOf <= 0)
        {
            await context.Reply("Home matches per team and best of must be at least 1.");
            return;
        }
        await _matchPlanner.CreateSeasonMatches(id, homeMatchesPerTeam, bestOf);
        await context.Reply($"The matches are made. Schedule them on {"the season page".LinkMarkdown(_urls.Season(id))}.");
    }

    public async Task CreateBracket(IDiscordInteractionContext context, string? season, int participants, bool doubleElimination, int bestOf)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season) is not int id)
            return;
        if (participants <= 0)
        {
            await context.Reply("A bracket needs participants.");
            return;
        }
        await _brackets.CreateBracket(participants, id, doubleElimination, bestOf);
        await context.Reply("The bracket is made. Seed it with /admin bracket seed, or set custom seeds on the site.");
    }

    public async Task SeedBracket(IDiscordInteractionContext context, string? season)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season) is not int id)
            return;
        await _brackets.SetSeeds(id);
        await context.Reply("The bracket is seeded by the standings.");
    }

    public async Task LockCaptains(IDiscordInteractionContext context, string? season, bool @lock)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season, SeasonDefault.Upcoming) is not int id)
            return;
        await _teams.LockCaptains(id, @lock);
        await context.Reply(@lock ? "The captains are locked." : "The captains are unlocked.");
    }

    /// <summary>The season's latest schedule recommendation, and whether one is being worked out.</summary>
    public async Task ScheduleShow(IDiscordInteractionContext context, string? season)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is null || await _lookups.SeasonFor(context, season) is not int id)
            return;
        var run = await _onDemand.Status<ScheduleRecommendationJob>(ScheduleRecommendationJob.RunName(id));
        var latest = await _recommendations.Latest(id);
        var lines = latest?.Matches
            .OrderBy(x => x.ScheduledTime)
            .Select(x => $"{x.ScheduledTime.DiscordTimeEmbed()} · {x.HomeTeam} vs {x.AwayTeam}")
            .ToList() ?? [];
        var head = latest is null
            ? "No recommendation yet: /admin schedule run works one out."
            : $"Recommendation #{latest.ScheduleRecommendationID}" + (latest.AppliedAt is { } applied ? $", applied {applied.DiscordTimeEmbed()}" : ", not applied");
        await context.Reply(CommandReplies.Embed($"Schedule ({run.State.ToString().ToLowerInvariant()})", _urls.Season(id),
            CommandReplies.Lines(lines, "No matches.", head: head + "\n\n")));
    }

    public async Task ScheduleRun(IDiscordInteractionContext context, string? season)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is not { } caller || await _lookups.SeasonFor(context, season) is not int id)
            return;
        var status = await _onDemand.Enqueue<ScheduleRecommendationJob, RecommendSchedule>(ScheduleRecommendationJob.RunName(id),
            new RecommendSchedule(id, caller.Gamertag, caller.UserID), $"Season {id}");
        await context.Reply($"The recommendation is {status.State.ToString().ToLowerInvariant()}. See it with /admin schedule show.");
    }

    public async Task ScheduleApply(IDiscordInteractionContext context, int recommendationID)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is not { } caller)
            return;
        var applied = await _recommendations.Apply(recommendationID, caller.Gamertag);
        await context.Reply(applied is int count ? $"{count} matches are scheduled." : "That recommendation doesn't exist or was applied already.");
    }

    public async Task HaloPull(IDiscordInteractionContext context, string gameID)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        if (!Guid.TryParse(gameID.Trim(), out var matchID))
        {
            await context.Reply(MatchCommandsHandler.NoSuchGame);
            return;
        }
        await _dataPull.GetAndSaveMatch(matchID);
        await context.Reply($"Pulled: {"the game".LinkMarkdown(_urls.Match(matchID))}.");
    }

    public async Task RefreshMedals(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        await _dataPull.DownloadMedals();
        await context.Reply("The medals are refreshed.");
    }

    public async Task Jobs(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        var overview = await _jobs.Overview();
        var lines = overview.Jobs
            .Select(x => $"`{x.Group}/{x.Name}` · {x.State}" + (x.Enabled ? "" : " · paused"))
            .ToList();
        await context.Reply(CommandReplies.Embed("Jobs", _urls.Page("usermanagement/jobs"), CommandReplies.Lines(lines, "No jobs.")));
    }

    /// <summary>Runs or pauses a job named group/name, as the jobs page does.</summary>
    public async Task Job(IDiscordInteractionContext context, string job, bool? pause)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is not { } caller)
            return;
        var parts = job.Trim().Split('/', 2);
        if (parts.Length != 2)
        {
            await context.Reply("Name the job as group/name: /admin jobs list shows them.");
            return;
        }
        var key = new JobKey(parts[1], parts[0]);
        var change = pause is { } paused ? await _jobs.SetPaused(key, paused, caller.Gamertag) : await _jobs.RunNow(key, caller.UserID);
        await context.Reply(change switch
        {
            JobChange.NotFound => "There is no such job.",
            JobChange.Conflict => "The job can't do that now (it is running or paused).",
            _ => Done,
        });
    }

    public async Task Claims(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        var claims = await _claims.List(GamertagClaimStatus.Pending);
        var lines = claims
            .Select(x => $"**#{x.GamertagClaimID}** {x.Gamertag} · by {x.ClaimantName}" + (x.SeasonName is { } season ? $" · {season}" : ""))
            .ToList();
        await context.Reply(CommandReplies.Embed("Pending gamertag claims", _urls.Page("usermanagement/claims"), CommandReplies.Lines(lines, "No claim is pending.")));
    }

    /// <summary>Approves or rejects a gamertag claim; the service's refusal is the reply (<see cref="DiscordInteractionPipeline"/>).</summary>
    public async Task DecideClaim(IDiscordInteractionContext context, int claimID, bool approve, string? reason)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        if (approve)
            await _claims.Approve(claimID);
        else
            await _claims.Reject(claimID, reason);
        await context.Reply(approve ? "Approved." : "Rejected.");
    }
}
