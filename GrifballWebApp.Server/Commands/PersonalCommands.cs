using DiscordInterfaces;
using GrifballWebApp.Application.Profile;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /queue: wraps the context and hands off to <see cref="PersonalCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("queue", "The Late League matchmaking queue")]
public class QueueCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly PersonalCommandsHandler _handler;

    public QueueCommands(PersonalCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("status", "How many are in the queue, and whether you are")]
    public Task Status() => _handler.QueueStatus(Context.ToDiscordContext());

    [SubSlashCommand("join", "Join the queue")]
    public Task Join() => _handler.QueueJoin(Context.ToDiscordContext());

    [SubSlashCommand("leave", "Leave the queue")]
    public Task Leave() => _handler.QueueLeave(Context.ToDiscordContext());
}

/// <summary>
/// NetCord entry point for /me: wraps the context and hands off to <see cref="PersonalCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("me", "Your account: region, gamertag claims and sign-ins")]
public class MeCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly PersonalCommandsHandler _handler;

    public MeCommands(PersonalCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("region", "Set your region")]
    public Task Region([SlashCommandParameter(Description = "Your region, or none to clear it", AutocompleteProviderType = typeof(RegionAutocomplete))] string region)
        => _handler.SetRegion(Context.ToDiscordContext(), region);

    [SubSlashCommand("claim", "Claim a gamertag you played under before")]
    public Task Claim([SlashCommandParameter(Description = "The past gamertag")] string gamertag)
        => _handler.Claim(Context.ToDiscordContext(), gamertag);

    [SubSlashCommand("claims", "Your gamertag claims")]
    public Task Claims() => _handler.Claims(Context.ToDiscordContext());

    [SubSlashCommand("logins", "Link or unlink your sign-ins")]
    public Task Logins() => _handler.Logins(Context.ToDiscordContext());
}

/// <summary>
/// NetCord entry point for /signup, /availability and /reschedule request: wraps the context and hands off to
/// <see cref="PersonalCommandsHandler"/>. Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class SignupCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly PersonalCommandsHandler _handler;

    public SignupCommands(PersonalCommandsHandler handler)
    {
        _handler = handler;
    }

    [SlashCommand("signup", "Sign up for a season, or change your signup")]
    public Task Signup(
        [SlashCommandParameter(Description = "The season (default: the next to start)", AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null,
        [SlashCommandParameter(Name = "team_name", Description = "The team name you'd use as captain (default: as it is)")] string? teamName = null,
        [SlashCommandParameter(Name = "will_captain", Description = "You'd captain a team (default: as it is)")] bool? willCaptain = null,
        [SlashCommandParameter(Name = "draft_help", Description = "You'd like help drafting (default: as it is)")] bool? needsDraftHelp = null)
        => _handler.Signup(Context.ToDiscordContext(), season, teamName, willCaptain, needsDraftHelp);
}

/// <summary>
/// NetCord entry point for /availability: wraps the context and hands off to <see cref="PersonalCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("availability", "When a season's matches can be played")]
public class AvailabilityCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly PersonalCommandsHandler _handler;

    public AvailabilityCommands(PersonalCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("show", "A season's match times")]
    public Task Show([SlashCommandParameter(Description = "The season (default: the next to start)", AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.AvailabilityShow(Context.ToDiscordContext(), season);
}

/// <summary>
/// NetCord entry point for /reschedule: wraps the context and hands off to <see cref="PersonalCommandsHandler"/> (request)
/// and <see cref="AdminCommandsHandler"/> (the staff's pending and decide).
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("reschedule", "Reschedule requests for season matches")]
public class RescheduleCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly PersonalCommandsHandler _handler;
    private readonly AdminCommandsHandler _admin;

    public RescheduleCommands(PersonalCommandsHandler handler, AdminCommandsHandler admin)
    {
        _handler = handler;
        _admin = admin;
    }

    [SubSlashCommand("request", "Ask to move one of your matches")]
    public Task Request(
        [SlashCommandParameter(Description = "The match (search by team)", AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match,
        [SlashCommandParameter(Description = "Why it needs to move")] string reason,
        [SlashCommandParameter(Name = "new_time", Description = "The new time in UTC, like 2026-11-20 20:00")] string? newTime = null)
        => _handler.RequestReschedule(Context.ToDiscordContext(), match, reason, newTime);

    [SubSlashCommand("pending", "Requests waiting for a decision (staff)")]
    public Task Pending() => _admin.PendingReschedules(Context.ToDiscordContext());

    [SubSlashCommand("decide", "Approve or reject a request (staff)")]
    public Task Decide(
        [SlashCommandParameter(Description = "The request's number, from /reschedule pending")] int request,
        [SlashCommandParameter(Description = "Approve it (false rejects it)")] bool approve,
        [SlashCommandParameter(Description = "Notes for the players")] string? notes = null)
        => _admin.DecideReschedule(Context.ToDiscordContext(), request, approve, notes);
}

/// <summary>The choices for a region option: the regions a player can be in.</summary>
public class RegionAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly RegionService _regions;
    public RegionAutocomplete(RegionService regions)
    {
        _regions = regions;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => (await _regions.List())
            .Where(x => x.RegionName.Contains(option.Value ?? "", StringComparison.OrdinalIgnoreCase))
            .Select(x => new ApplicationCommandOptionChoiceProperties(x.RegionName, x.RegionID.ToString()))
            .Append(new ApplicationCommandOptionChoiceProperties("None (clear it)", "none"))
            .Take(25);
}
