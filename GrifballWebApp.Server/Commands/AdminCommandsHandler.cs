using DiscordInterface.Generated;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Commissioner;
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
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using Quartz;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Server.Extensions;
using CheckboxProperties = NetCord.Rest.CheckboxProperties;
using InteractionCallback = NetCord.Rest.InteractionCallback;
using LabelProperties = NetCord.Rest.LabelProperties;
using ModalProperties = NetCord.Rest.ModalProperties;
using RadioGroupOptionProperties = NetCord.Rest.RadioGroupOptionProperties;
using RadioGroupProperties = NetCord.Rest.RadioGroupProperties;
using TextInputProperties = NetCord.Rest.TextInputProperties;
using TextInputStyle = NetCord.Rest.TextInputStyle;

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
    public const string JobNameHelp = "Name the job as group/name: /admin jobs list shows them.";
    public const string JobNameNotForTheForm = "That job name can't go in the form: give paused (true or false) with the command instead.";
    public const string NoRequestWaiting = "No request with that number waits for a decision: /reschedule pending lists the ones that do.";
    public const string BracketNeedsParticipants = "A bracket needs participants.";
    public const string DoubleEliminationInput = "double_elimination";
    public const string JobStateInput = "state";
    public const string DecisionInput = "decision";
    public const string NotesInput = "notes";
    public const string NoGameId = "Give the game's Halo match ID.";
    public const string NoTime = "Give the time in UTC, like 2026-11-20 20:00.";
    public const string ReplacesNotANumber = "The series game it replaces is a number, like 2 (leave it empty for none).";
    public const string NoClaimPending = "No pending claim has that number: /admin claims list shows them.";
    public const string GameIdInput = "game_id";
    public const string ReplacesInput = "replaces";
    public const string TimeInput = "time";
    public const string ReasonInput = "reason";
    /// <summary>The longest a Halo match ID or a time is typed (a GUID with braces is 38 characters).</summary>
    private const int ShortInputMaxLength = 100;
    /// <summary>Discord's limits on a component's custom ID and on a label's text.</summary>
    private const int CustomIdLimit = 100;
    private const int LabelLimit = 45;
    /// <summary>The longest notes a decision keeps (MatchRescheduleConfiguration's column).</summary>
    private const int NotesMaxLength = 1000;

    private readonly DiscordCaller _caller;
    private readonly CommandLookups _lookups;
    private readonly RescheduleService _reschedules;
    private readonly CommissionerDashboardService _dashboard;
    private readonly SeasonMatchService _seasonMatches;
    private readonly MatchPlannerService _matchPlanner;
    private readonly IBracketService _brackets;
    private readonly TeamService _teams;
    private readonly ScheduleRecommendations _recommendations;
    private readonly IScheduleRecommendationRuns _runs;
    private readonly JobsService _jobs;
    private readonly GamertagClaimService _claims;
    private readonly IDataPullService _dataPull;
    private readonly UrlService _urls;

    public AdminCommandsHandler(DiscordCaller caller, CommandLookups lookups, RescheduleService reschedules, CommissionerDashboardService dashboard,
        SeasonMatchService seasonMatches,
        MatchPlannerService matchPlanner, IBracketService brackets, TeamService teams, ScheduleRecommendations recommendations,
        IScheduleRecommendationRuns runs, JobsService jobs, GamertagClaimService claims, IDataPullService dataPull, UrlService urls)
    {
        _caller = caller;
        _lookups = lookups;
        _reschedules = reschedules;
        _dashboard = dashboard;
        _seasonMatches = seasonMatches;
        _matchPlanner = matchPlanner;
        _brackets = brackets;
        _teams = teams;
        _recommendations = recommendations;
        _runs = runs;
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
        var summary = (await _dashboard.GetAsync()).Summary;
        var embed = CommandReplies.Embed("Commissioner dashboard", _urls.Page("commissioner-dashboard"),
            $"{summary.PendingRescheduleCount} pending reschedules · {summary.OverdueMatchCount} overdue matches · {summary.CriticalOverdueCount} over 3 days");
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
        await context.Reply(new CommandView(CommandReplies.Embed("Pending reschedules", _urls.Page("commissioner-dashboard"), CommandReplies.Lines(lines, "No reschedule is pending.")),
            Decisions(pending.Select(x => x.MatchRescheduleID), DiscordButtonContants.RescheduleApproveWithParams, DiscordButtonContants.RescheduleRejectWithParams)));
    }

    /// <summary>The most items of a pending list that get Approve and Reject buttons: a message shows five rows.</summary>
    private const int DecisionRows = 5;

    /// <summary>Approve and Reject under the first items of a pending list (their IDs), one row each, in the list's order.</summary>
    private static IReadOnlyList<NetCord.Rest.IMessageComponentProperties> Decisions(IEnumerable<int> ids, Func<int, string> approve, Func<int, string> reject)
        => [.. ids.Take(DecisionRows).Select(id => new NetCord.Rest.ActionRowProperties(
        [
            new NetCord.Rest.ButtonProperties(approve(id),  Approve #{id}", NetCord.ButtonStyle.Success),
            new NetCord.Rest.ButtonProperties(reject(id),  Reject #{id}", NetCord.ButtonStyle.Danger),
        ]))];

    /// <summary>A pending request's Approve button: approved at once, as /reschedule decide approve:true does.</summary>
    public Task ApproveReschedule(IDiscordInteractionContext context, int rescheduleID) => DecideReschedule(context, rescheduleID, approve: true, notes: null);

    /// <summary>A pending request's Reject button: the decision modal with Reject picked, for the notes the players read.</summary>
    public Task RejectReschedule(IDiscordInteractionContext context, int rescheduleID) => OpenDecisionModal(context, rescheduleID, notes: null, rejecting: true);

    /// <summary>
    /// Approves or rejects a reschedule request, as the dashboard does. With neither given, a modal asks which, with the
    /// notes for the players.
    /// </summary>
    public async Task DecideReschedule(IDiscordInteractionContext context, int rescheduleID, bool? approve, string? notes)
    {
        if (approve is not { } approved)
        {
            await OpenDecisionModal(context, rescheduleID, notes);
            return;
        }
        if (await Staff(context, DiscordCaller.Roles.Staff) is not { } caller)
            return;
        try
        {
            await _reschedules.ProcessRescheduleAsync(rescheduleID, new ProcessRescheduleDto { Approved = approved, CommissionerNotes = notes }, caller.UserID);
        }
        catch (ArgumentException ex)
        {
            await context.Reply(ex.Message);
            return;
        }
        await context.Reply(approved ? "Approved." : "Rejected.");
    }

    private async Task OpenDecisionModal(IDiscordInteractionContext context, int rescheduleID, string? notes, bool rejecting = false)
    {
        if (await _caller.RequireBeforeResponding(context, DiscordCaller.Roles.Staff) is null)
            return;
        if (!await _reschedules.AwaitsDecisionAsync(rescheduleID))
        {
            await context.EphemeralResponse(NoRequestWaiting);
            return;
        }
        var modal = new ModalProperties(DiscordModalsContants.RescheduleDecideWithParams(rescheduleID), $"Reschedule request #{rescheduleID}")
        {
            new LabelProperties("Decision", new RadioGroupProperties(DecisionInput,
            [
                new RadioGroupOptionProperties("Approve", "approve"),
                new RadioGroupOptionProperties("Reject", "reject") { Default = rejecting },
            ])),
            new LabelProperties("Notes for the players", new TextInputProperties(NotesInput, TextInputStyle.Paragraph)
            {
                Required = false,
                MaxLength = NotesMaxLength,
                Value = notes?[..Math.Min(notes.Length, NotesMaxLength)],
            }),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The decision modal's answers, decided as the command decides them.</summary>
    public Task SaveDecision(IDiscordModalInteractionContext context, int rescheduleID)
        => DecideReschedule(context, rescheduleID, ModalAnswers.Picked(context.Components, DecisionInput) == "approve",
            ModalAnswers.Text(context.Components, NotesInput));

    /// <summary>Reports a Halo game as one of the match's. With no game ID given, a modal asks for it.</summary>
    public async Task ReportGame(IDiscordInteractionContext context, string match, string? gameID, int? replaces)
    {
        if (gameID is null)
        {
            await OpenReportModal(context, match, replaces);
            return;
        }
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match, SeasonViewer.Staff) is not int id)
            return;
        if (!Guid.TryParse(gameID.Trim(), out var matchID) || matchID == Guid.Empty)
        {
            await context.Reply(MatchCommandsHandler.NoSuchGame);
            return;
        }
        await Act(context, () => _seasonMatches.ReportMatch(id, matchID, replaces), "Reported.");
    }

    /// <summary>
    /// A commissioner's match, looked up before any response (a modal must be the first one), the role checked first; null with the
    /// caller told why. The match then travels in the modal's custom ID as its ID.
    /// </summary>
    private async Task<int?> MatchBeforeResponding(IDiscordInteractionContext context, string match)
    {
        return await _caller.RequireBeforeResponding(context, DiscordCaller.Roles.CommissionerOnly) is null
            ? null
            : await _lookups.SeasonMatchBeforeResponding(context, match, SeasonViewer.Staff);
    }

    private async Task OpenReportModal(IDiscordInteractionContext context, string match, int? replaces)
    {
        if (await MatchBeforeResponding(context, match) is not int id)
            return;
        var modal = new ModalProperties(DiscordModalsContants.MatchReportWithParams(id), "Report a game")
        {
            new LabelProperties("Halo match ID", new TextInputProperties(GameIdInput, TextInputStyle.Short)
            {
                Required = true,
                MaxLength = ShortInputMaxLength,
            }),
            new LabelProperties("Series game it replaces (none: leave empty)", new TextInputProperties(ReplacesInput, TextInputStyle.Short)
            {
                Required = false,
                MaxLength = ShortInputMaxLength,
                Value = replaces?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The report modal's answers, reported as the command reports them (its game ID is required there, so always given).</summary>
    public async Task SaveReport(IDiscordModalInteractionContext context, int seasonMatchID)
    {
        var replaces = ModalAnswers.Text(context.Components, ReplacesInput);
        int? replaced = null;
        if (replaces is not null)
        {
            if (!int.TryParse(replaces.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var game) || game < 0)
            {
                await context.EphemeralResponse(ReplacesNotANumber);
                return;
            }
            replaced = game > 0 ? game : null;
        }
        if (ModalAnswers.Text(context.Components, GameIdInput) is not { } gameID)
        {
            await context.EphemeralResponse(NoGameId);
            return;
        }
        await ReportGame(context, CommandLookups.ChoiceValue(seasonMatchID), gameID, replaced);
    }

    public async Task Forfeit(IDiscordInteractionContext context, string match, ForfeitSide side)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match, SeasonViewer.Staff) is not int id)
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
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match, SeasonViewer.Staff) is not int id)
            return;
        await Act(context, () => _seasonMatches.UndoResult(id), "The result is undone.");
    }

    /// <summary>Schedules the match. With no time given, a modal asks for it, starting from the match's time now.</summary>
    public async Task SetTime(IDiscordInteractionContext context, string match, string? time)
    {
        if (time is null)
        {
            await OpenTimeModal(context, match);
            return;
        }
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonMatchFor(context, match, SeasonViewer.Staff) is not int id)
            return;
        if (!CommandLookups.TryParseUtc(time, out var when))
        {
            await context.Reply("I couldn't read that time. Use UTC like 2026-11-20 20:00.");
            return;
        }
        var conflicts = await _matchPlanner.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = id, Time = when }) ?? [];
        await context.Reply($"The match is set for {when.DiscordTimeEmbed()}.{Warnings(conflicts)}");
    }

    private async Task OpenTimeModal(IDiscordInteractionContext context, string match)
    {
        if (await MatchBeforeResponding(context, match) is not int id)
            return;
        var modal = new ModalProperties(DiscordModalsContants.MatchTimeWithParams(id), "Schedule the match")
        {
            new LabelProperties("Time in UTC, like 2026-11-20 20:00", new TextInputProperties(TimeInput, TextInputStyle.Short)
            {
                Required = true,
                MaxLength = ShortInputMaxLength,
                Value = (await _lookups.ScheduledTime(id))?.ToString(CommandLookups.UtcFormat, System.Globalization.CultureInfo.InvariantCulture),
            }),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The time modal's answer, set as the command sets it (it is required there, so always given).</summary>
    public async Task SaveTime(IDiscordModalInteractionContext context, int seasonMatchID)
    {
        if (ModalAnswers.Text(context.Components, TimeInput) is not { } time)
        {
            await context.EphemeralResponse(NoTime);
            return;
        }
        await SetTime(context, CommandLookups.ChoiceValue(seasonMatchID), time);
    }

    /// <summary>What is wrong with a time just set (the season's rules), as a reply's last lines; nothing when nothing is.</summary>
    public static string Warnings(IReadOnlyList<GrifballWebApp.Application.Scheduling.ScheduleConflict> conflicts)
        => GrifballWebApp.Application.Scheduling.ScheduleConflict.Warnings(conflicts, "\n");

    public async Task GenerateMatches(IDiscordInteractionContext context, string? season, int homeMatchesPerTeam, int bestOf)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season, SeasonPurpose.Active, SeasonViewer.Staff) is not int id)
            return;
        if (homeMatchesPerTeam <= 0 || bestOf <= 0)
        {
            await context.Reply("Home matches per team and best of must be at least 1.");
            return;
        }
        await _matchPlanner.CreateSeasonMatches(id, homeMatchesPerTeam, bestOf);
        await context.Reply($"The matches are made. Schedule them on {"the season page".LinkMarkdown(_urls.Season(id))}.");
    }

    /// <summary>Makes the season's bracket. With double elimination not given, a modal asks for it.</summary>
    public async Task CreateBracket(IDiscordInteractionContext context, string? season, int participants, bool? doubleElimination, int bestOf)
    {
        if (doubleElimination is not { } doubleElim)
        {
            await OpenBracketModal(context, season, participants, bestOf);
            return;
        }
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season, SeasonPurpose.Active, SeasonViewer.Staff) is not int id)
            return;
        if (participants <= 0)
        {
            await context.Reply(BracketNeedsParticipants);
            return;
        }
        await _brackets.CreateBracket(participants, id, doubleElim, bestOf);
        await context.Reply("The bracket is made. Seed it with /admin bracket seed, or set custom seeds on the site.");
    }

    /// <summary>The bracket modal: the season is looked up first (a name may hold the custom ID's separator), and travels as its ID.</summary>
    private async Task OpenBracketModal(IDiscordInteractionContext context, string? season, int participants, int bestOf)
    {
        if (await _caller.RequireBeforeResponding(context, DiscordCaller.Roles.CommissionerOnly) is null)
            return;
        if (await _lookups.Season(season, SeasonPurpose.Active, SeasonViewer.Staff) is not int id)
        {
            await context.EphemeralResponse(CommandLookups.NoSuchSeason);
            return;
        }
        if (participants <= 0)
        {
            await context.EphemeralResponse(BracketNeedsParticipants);
            return;
        }
        var modal = new ModalProperties(DiscordModalsContants.BracketCreateWithParams(id, participants, bestOf), "Make the bracket")
        {
            new LabelProperties("Double elimination", new CheckboxProperties(DoubleEliminationInput)),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The bracket modal's answer, made as the command makes it.</summary>
    public Task SaveBracket(IDiscordModalInteractionContext context, int seasonID, int participants, int bestOf)
        => CreateBracket(context, CommandLookups.ChoicePrefix + seasonID, participants, ModalAnswers.Checked(context.Components, DoubleEliminationInput), bestOf);

    public async Task SeedBracket(IDiscordInteractionContext context, string? season)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season, SeasonPurpose.Current, SeasonViewer.Staff) is not int id)
            return;
        await _brackets.SetSeeds(id);
        await context.Reply("The bracket is seeded by the standings.");
    }

    public async Task LockCaptains(IDiscordInteractionContext context, string? season, bool @lock)
    {
        if (await Staff(context, DiscordCaller.Roles.CommissionerOnly) is null || await _lookups.SeasonFor(context, season, SeasonPurpose.Upcoming, SeasonViewer.Staff) is not int id)
            return;
        await _teams.LockCaptains(id, @lock);
        await context.Reply(@lock ? "The captains are locked." : "The captains are unlocked.");
    }

    /// <summary>The season's latest schedule recommendation, and whether one is being worked out.</summary>
    public async Task ScheduleShow(IDiscordInteractionContext context, string? season)
    {
        if (await Staff(context, DiscordCaller.Roles.Staff) is null || await _lookups.SeasonFor(context, season, SeasonPurpose.Active, SeasonViewer.Staff) is not int id)
            return;
        var run = await _runs.Status(id);
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
        if (await Staff(context, DiscordCaller.Roles.Staff) is not { } caller || await _lookups.SeasonFor(context, season, SeasonPurpose.Active, SeasonViewer.Staff) is not int id)
            return;
        var status = await _runs.Queue(id, caller.Gamertag, caller.UserID);
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
        await context.Reply(CommandReplies.Embed("Jobs", _urls.Page("sysadmin/jobs"), CommandReplies.Lines(lines, "No jobs.")));
    }

    /// <summary>Pauses or resumes a job named group/name. With neither given, a modal asks which.</summary>
    public async Task PauseJob(IDiscordInteractionContext context, string job, bool? paused)
    {
        if (paused is { } pause)
        {
            await Job(context, job, pause);
            return;
        }
        if (await _caller.RequireBeforeResponding(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        var name = job.Trim();
        if (ParseJob(name) is null)
        {
            await context.EphemeralResponse(JobNameHelp);
            return;
        }
        // The name travels in the modal's custom ID, which can't hold its separator or a long one
        if (name.Contains(':') || DiscordModalsContants.JobPauseWithParams(name).Length > CustomIdLimit)
        {
            await context.EphemeralResponse(JobNameNotForTheForm);
            return;
        }
        var modal = new ModalProperties(DiscordModalsContants.JobPauseWithParams(name), "Pause or resume the job")
        {
            new LabelProperties(name[..Math.Min(name.Length, LabelLimit)], new RadioGroupProperties(JobStateInput,
            [
                new RadioGroupOptionProperties("Pause it", "pause") { Default = true },
                new RadioGroupOptionProperties("Resume it", "resume"),
            ])),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The pause modal's answer, done as the command does it.</summary>
    public Task SavePause(IDiscordModalInteractionContext context, string job)
        => Job(context, job, ModalAnswers.Picked(context.Components, JobStateInput) != "resume");

    /// <summary>Runs or pauses a job named group/name, as the jobs page does.</summary>
    public async Task Job(IDiscordInteractionContext context, string job, bool? pause)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is not { } caller)
            return;
        if (ParseJob(job) is not { } key)
        {
            await context.Reply(JobNameHelp);
            return;
        }
        var change = pause is { } paused ? await _jobs.SetPaused(key, paused, caller.Gamertag) : await _jobs.RunNow(key, caller.UserID);
        await context.Reply(change switch
        {
            JobChange.NotFound => "There is no such job.",
            JobChange.Conflict => "The job can't do that now (it is running or paused).",
            _ => Done,
        });
    }

    /// <summary>The job named group/name; null when the name isn't one.</summary>
    private static JobKey? ParseJob(string job)
        => job.Trim().Split('/', 2) is [var group, var name] ? new JobKey(name, group) : null;

    public async Task Claims(IDiscordInteractionContext context)
    {
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        var claims = await _claims.List(GamertagClaimStatus.Pending);
        var lines = claims
            .Select(x => $"**#{x.GamertagClaimID}** {x.Gamertag} · by {x.ClaimantName}" + (x.SeasonName is { } season ? $" · {season}" : ""))
            .ToList();
        await context.Reply(new CommandView(CommandReplies.Embed("Pending gamertag claims", _urls.Page("usermanagement/claims"), CommandReplies.Lines(lines, "No claim is pending.")),
            Decisions(claims.Select(x => x.GamertagClaimID), DiscordButtonContants.ClaimApproveWithParams, DiscordButtonContants.ClaimRejectWithParams)));
    }

    /// <summary>A pending claim's Approve button: approved at once, as /admin claims approve does.</summary>
    public Task ApproveClaim(IDiscordInteractionContext context, int claimID) => DecideClaim(context, claimID, approve: true, reason: null);

    /// <summary>A pending claim's Reject button: the rejection modal, for the reason the claimant reads (<see cref="DecideClaim"/>).</summary>
    public Task RejectClaim(IDiscordInteractionContext context, int claimID) => DecideClaim(context, claimID, approve: false, reason: null);
    }

    /// <summary>
    /// Approves or rejects a gamertag claim; the service's refusal is the reply (<see cref="DiscordInteractionPipeline"/>). A rejection with
    /// no reason given opens a modal for it (left empty there, it is rejected without one).
    /// </summary>
    public async Task DecideClaim(IDiscordInteractionContext context, int claimID, bool approve, string? reason)
    {
        if (!approve && reason is null)
        {
            await OpenRejectModal(context, claimID);
            return;
        }
        if (await Staff(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        if (approve)
            await _claims.Approve(claimID);
        else
            await _claims.Reject(claimID, reason);
        await context.Reply(approve ? "Approved." : "Rejected.");
    }

    private async Task OpenRejectModal(IDiscordInteractionContext context, int claimID)
    {
        if (await _caller.RequireBeforeResponding(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;
        // Only a pending claim is rejected: said before a reason is written for nothing
        if (!(await _claims.List(GamertagClaimStatus.Pending)).Exists(c => c.GamertagClaimID == claimID))
        {
            await context.EphemeralResponse(NoClaimPending);
            return;
        }
        var modal = new ModalProperties(DiscordModalsContants.ClaimRejectWithParams(claimID), $"Reject claim #{claimID}")
        {
            new LabelProperties("Why, for the claimant (optional)", new TextInputProperties(ReasonInput, TextInputStyle.Paragraph)
            {
                Required = false,
                MaxLength = GamertagClaim.MaxRejectionReasonLength,
            }),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The rejection modal's answer: rejected as the command rejects, an empty reason meaning none.</summary>
    public Task SaveRejection(IDiscordModalInteractionContext context, int claimID)
        => DecideClaim(context, claimID, approve: false, ModalAnswers.Text(context.Components, ReasonInput) ?? "");
}
