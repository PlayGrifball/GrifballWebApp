using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Server.Matchmaking;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// The caller's own things: /queue, /me, /signup, /availability and /reschedule request. Each needs what the site's
/// endpoint needs: an account (with the Set Gamertag button when there is none) and the endpoint's role, then the shared
/// service's rules. Replies about the caller are ephemeral. /queue status and /availability show are open to everyone,
/// as on the site. Their NetCord modules only wrap their context and call these.
/// </summary>
public class PersonalCommandsHandler
{
    public const string NoSuchRegion = "I found no such region.";

    private readonly DiscordCaller _caller;
    private readonly CommandLookups _lookups;
    private readonly QueueMembership _membership;
    private readonly IQueueRepository _queue;
    private readonly MatchmakingButtonHandler _queueButtons;
    private readonly RegionService _regions;
    private readonly UserGamertagClaimService _claims;
    private readonly SignupsService _signups;
    private readonly SeasonService _seasons;
    private readonly AvailabilityService _availability;
    private readonly RescheduleService _reschedules;
    private readonly UrlService _urls;

    public PersonalCommandsHandler(DiscordCaller caller, CommandLookups lookups, QueueMembership membership, IQueueRepository queue,
        MatchmakingButtonHandler queueButtons, RegionService regions, UserGamertagClaimService claims, SignupsService signups, SeasonService seasons,
        AvailabilityService availability, RescheduleService reschedules, UrlService urls)
    {
        _caller = caller;
        _lookups = lookups;
        _membership = membership;
        _queue = queue;
        _queueButtons = queueButtons;
        _regions = regions;
        _claims = claims;
        _signups = signups;
        _seasons = seasons;
        _availability = availability;
        _reschedules = reschedules;
        _urls = urls;
    }

    /// <summary>The queue's size, and the caller's place in it when they have an account.</summary>
    public async Task QueueStatus(IDiscordInteractionContext context)
    {
        await context.Defer(ephemeral: true);
        var player = await _queue.PlayerStateByDiscord((long)context.Interaction.User.Id);
        var count = player?.QueueCount ?? await _queue.Count();
        var you = player switch
        {
            null => "",
            { InMatch: true } => "\nYou are in a Late League match.",
            { InQueue: true } => "\nYou are in the queue.",
            _ => "\nYou are not in the queue.",
        };
        await context.Reply($"{count} {(count == 1 ? "player is" : "players are")} in the Late League queue.{you}");
    }

    /// <summary>Joins the queue on <see cref="QueueMembership"/>'s rules, as the button and the site do.</summary>
    public async Task QueueJoin(IDiscordInteractionContext context)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        var (result, _) = await _membership.Join(caller.UserID);
        if (result is QueueJoinResult.Joined)
            _ = _queueButtons.RefreshQueueDisplay();
        await context.Reply(QueueMessages.For(result));
    }

    /// <summary>Leaves the queue on <see cref="QueueMembership"/>'s rules, as the button and the site do.</summary>
    public async Task QueueLeave(IDiscordInteractionContext context)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        var (result, _) = await _membership.Leave(caller.UserID);
        if (result is QueueLeaveResult.Left)
            _ = _queueButtons.RefreshQueueDisplay();
        await context.Reply(QueueMessages.For(result));
    }

    /// <summary>Sets the caller's region, by name or ID; "none" clears it.</summary>
    public async Task SetRegion(IDiscordInteractionContext context, string region)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        int? regionID = null;
        if (!region.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            var regions = await _regions.List();
            regionID = regions.FirstOrDefault(x => x.RegionID.ToString() == region.Trim() || x.RegionName.Equals(region.Trim(), StringComparison.OrdinalIgnoreCase))?.RegionID;
            if (regionID is null)
            {
                await context.Reply($"{NoSuchRegion} The regions are {string.Join(", ", regions.Select(x => x.RegionName))}.");
                return;
            }
        }

        await context.Reply(await _regions.Set(caller.UserID, regionID) switch
        {
            SetRegionResult.Set => regionID is null ? "Your region is cleared." : "Your region is set.",
            SetRegionResult.NoSuchRegion => NoSuchRegion,
            _ => "I found no account to set it on.",
        });
    }

    /// <summary>Claims a past gamertag for a sysadmin to decide: refused as the site refuses it, when it can't be claimed.</summary>
    public async Task Claim(IDiscordInteractionContext context, string gamertag)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        var preview = await _claims.Preview(caller.UserID, gamertag);
        if (preview.Refusal is { } refusal)
        {
            await context.Reply(refusal);
            return;
        }

        var claim = await _claims.Submit(caller.UserID, new SubmitGamertagClaimRequest { Gamertag = gamertag });
        var seasons = preview.Seasons.Count > 0 ? $" It covers {string.Join(", ", preview.Seasons.Select(x => x.SeasonName))}." : "";
        await context.Reply($"Your claim of {claim.Gamertag} is {claim.Status.ToLowerInvariant()}: a sysadmin decides it.{seasons}");
    }

    public async Task Claims(IDiscordInteractionContext context)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        var claims = await _claims.ClaimsOf(caller.UserID);
        var lines = claims
            .Select(x => $"{x.Gamertag} · {x.Status}" + (x.RejectionReason is { } reason ? $" · {reason}" : ""))
            .ToList();
        await context.Reply(CommandReplies.Embed("Your gamertag claims", _urls.Profile(caller.UserID), CommandReplies.Lines(lines, "You have made no claims.")));
    }

    /// <summary>Linking a sign-in (Discord, Microsoft, ...) runs through the provider in a browser: a link to the profile page.</summary>
    public async Task Logins(IDiscordInteractionContext context)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        await context.Reply(CommandReplies.Embed("Your sign-ins", _urls.Profile(caller.UserID), "Link or unlink sign-ins on your profile page."));
    }

    /// <summary>
    /// Signs the caller up for a season, or changes their signup; the availability they picked on the site is kept (the
    /// signup form is where it is set).
    /// </summary>
    public async Task Signup(IDiscordInteractionContext context, string? season, string? teamName, bool willCaptain, bool needsDraftHelp)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.PlayerOrSysadmin) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        if (await _lookups.Season(season) is not int seasonID || await _seasons.GetSeasonName(seasonID) is not { } name)
        {
            await context.Reply(SeasonCommandsHandler.NoSuchSeason);
            return;
        }

        var existing = await _signups.GetSignup(seasonID, 0, caller.UserID);
        var dto = new SignupRequestDto
        {
            SeasonID = seasonID,
            UserID = caller.UserID,
            TeamName = teamName,
            WillCaptain = willCaptain,
            RequiresAssistanceDrafting = needsDraftHelp,
            Timeslots = existing?.Timeslots ?? [],
        };
        try
        {
            await _signups.UpsertSignup(dto);
        }
        catch (SignupsClosedException)
        {
            await context.Reply($"Signups for {name} are closed.");
            return;
        }
        catch (Exception ex) when (ex is SignupNeedsGamertagException or SignupTeamNameTooLongException)
        {
            await context.Reply(ex.Message);
            return;
        }

        var form = "the signup form".LinkMarkdown(_urls.SignupForm(seasonID));
        await context.Reply(existing is null
            ? $"You have signed up for {name}. Set the times you can play on {form}."
            : $"Your signup for {name} is changed. Your times are on {form}.");
    }

    /// <summary>The times a season's matches can be played, in UTC.</summary>
    public async Task AvailabilityShow(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await _lookups.Season(season) is not int seasonID || await _seasons.GetSeasonName(seasonID) is not { } name)
        {
            await context.Reply(SeasonCommandsHandler.NoSuchSeason);
            return;
        }

        var timeslots = await _availability.GetSeasonAvailability(seasonID, CancellationToken.None);
        var lines = timeslots
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Time)
            .GroupBy(x => x.DayOfWeek)
            .Select(day => $"**{day.Key}**: {string.Join(", ", day.Select(x => x.Time.ToString("HH:mm")))}")
            .ToList();
        var description = CommandReplies.Lines(lines, "No times are set yet.") + $"\n\nTimes are UTC. Pick yours on {"the signup form".LinkMarkdown(_urls.SignupForm(seasonID))}.";
        await context.Reply(CommandReplies.Embed($"{name} availability", _urls.Season(seasonID), description));
    }

    /// <summary>
    /// Asks to move a season match, as the site's request form does: the service decides who may (a player on either
    /// team, or a commissioner) and whether the match can be moved.
    /// </summary>
    public async Task RequestReschedule(IDiscordInteractionContext context, string match, string reason, string? newTime)
    {
        if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
            return;
        await context.Defer(ephemeral: true);
        if (await _lookups.SeasonMatch(match) is not int seasonMatchID)
        {
            await context.Reply(MatchCommandsHandler.NoSuchMatch);
            return;
        }

        DateTime? when = null;
        if (!string.IsNullOrWhiteSpace(newTime))
        {
            if (!CommandLookups.TryParseUtc(newTime, out var parsed))
            {
                await context.Reply("I couldn't read that time. Use UTC like 2026-11-20 20:00, or pick it on " + "the request form".LinkMarkdown(_urls.RescheduleRequest(seasonMatchID)) + ".");
                return;
            }
            when = parsed;
        }

        try
        {
            await _reschedules.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = seasonMatchID, NewScheduledTime = when, Reason = reason }, caller.UserID);
        }
        catch (Exception ex) when (ex is RescheduleNotAllowedException or RescheduleForbiddenException or RescheduleAlreadyActiveException or ArgumentException)
        {
            await context.Reply(ex.Message);
            return;
        }

        await context.Reply($"Your reschedule request is in{(when is { } w ? $", for {w.DiscordTimeEmbed()}" : "")}. A commissioner will decide it.");
    }
}
