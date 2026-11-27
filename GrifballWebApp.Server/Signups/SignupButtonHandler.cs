using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Infrastructure.Discord;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Signups;

/// <summary>
/// The season signup button. <see cref="ButtonInteractions"/> is the NetCord module; it only wraps its context and calls this,
/// so it takes our <see cref="IDiscordButtonInteractionContext"/> and tests can pass any implementation.
/// </summary>
public class SignupButtonHandler
{
    private readonly GrifballContext _context;
    private readonly SignupsService _signupsService;
    private readonly UrlService _urlService;
    public SignupButtonHandler(GrifballContext context, SignupsService signupsService, UrlService urlService)
    {
        _context = context;
        _signupsService = signupsService;
        _urlService = urlService;
    }

    private async Task<Domain.Players.User?> UserGuard(IDiscordInteractionContext discordContext, ulong discordUserId)
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)discordUserId)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await discordContext.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    public async Task Signup(IDiscordButtonInteractionContext discordContext, int seasonId)
    {
        var user = await UserGuard(discordContext, discordContext.User.Id);
        if (user is null) return;

        var isSignedUp = await _context.SeasonSignups
            .AnyAsync(Domain.League.SeasonSignup.Of(seasonId, user.Id));
        var signupUrl = _urlService.SignupForm(seasonId);
        if (isSignedUp)
        {
            await WithDetailsButton(discordContext, seasonId, $"You are already signed up for this season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
            return;
        }

        var dto = new SignupRequestDto()
        {
            SeasonID = seasonId,
            UserID = user.Id,
        };
        try
        {
            await _signupsService.UpsertSignup(dto);
        }
        catch (SignupsClosedException)
        {
            await discordContext.EphemeralResponse("Signups for this season are closed.");
            return;
        }

        await WithDetailsButton(discordContext, seasonId, $"You have signed up for the season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
    }
    /// <summary>The most timeslot boxes the modal shows: Discord allows 5 components, and a box group holds 10 options.</summary>
    public const int MaxTimeslotGroups = 3;
    public const int TimeslotsPerGroup = 10;
    public const string TeamNameInput = "team_name";
    public const string ChoicesInput = "choices";
    public const string WillCaptainChoice = "captain";
    public const string DraftHelpChoice = "draft_help";
    public const string TimesInputPrefix = "times_";

    private static async Task WithDetailsButton(IDiscordInteractionContext context, int seasonId, string message)
    {
        var button = new ButtonProperties(DiscordButtonContants.SignupDetailsWithParams(seasonId), "Set my details and times", ButtonStyle.Secondary);
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = message,
            Flags = MessageFlags.Ephemeral,
            Components = [new ActionRowProperties { button }],
        }));
    }

    /// <summary>Opens the signup modal, filled in with the player's signup as it is: team name, captain and draft help, and the times they can play (UTC).</summary>
    public async Task SignupDetails(IDiscordButtonInteractionContext discordContext, int seasonId)
    {
        var user = await UserGuard(discordContext, discordContext.User.Id);
        if (user is null) return;

        var signup = await _signupsService.GetSignup(seasonId, 0, user.Id);
        var timeslots = signup?.Timeslots ?? await _signupsService.GetTimeslots(seasonId, 0, user.Id);
        await discordContext.Interaction.SendResponseAsync(InteractionCallback.Modal(BuildModal(seasonId, signup, timeslots)));
    }

    public static ModalProperties BuildModal(int seasonId, SignupResponseDto? signup, IEnumerable<TimeslotDto> timeslots)
    {
        var modal = new ModalProperties(DiscordModalsContants.SignupWithParams(seasonId), "Season signup")
        {
            new LabelProperties("Team name (if you captain)", new TextInputProperties(TeamNameInput, TextInputStyle.Short)
            {
                Required = false,
                MaxLength = Domain.League.Team.TeamNameMaxLength,
                Value = signup?.TeamName,
            }),
            new LabelProperties("About you", new CheckboxGroupProperties(ChoicesInput,
            [
                new CheckboxGroupOptionProperties("I would captain a team", WillCaptainChoice) { Default = signup?.WillCaptain ?? false },
                new CheckboxGroupOptionProperties("I would like help drafting (captains only)", DraftHelpChoice) { Default = signup?.RequiresAssistanceDrafting ?? false },
            ])
            {
                MinValues = 0,
                Required = false,
            }),
        };

        var shown = Shown(timeslots).ToList();
        var groups = shown.Chunk(TimeslotsPerGroup).ToList();
        for (var i = 0; i < groups.Count; i++)
        {
            var label = groups.Count == 1 ? "Times you can play (UTC)" : $"Times you can play (UTC) {i + 1}/{groups.Count}";
            modal.Add(new LabelProperties(label, new CheckboxGroupProperties(TimesInputPrefix + i,
                groups[i].Select(t => new CheckboxGroupOptionProperties($"{t.DayOfWeek.ToString()[..3]} {t.Time:HH\\:mm}", t.OptionID.ToString()) { Default = t.IsChecked }))
            {
                MinValues = 0,
                Required = false,
            }));
        }
        return modal;
    }

    /// <summary>The timeslots the modal has boxes for: Monday first, earliest first, as many as fit.</summary>
    private static IEnumerable<TimeslotDto> Shown(IEnumerable<TimeslotDto> timeslots)
        => timeslots
            .OrderBy(t => ((int)t.DayOfWeek + 6) % 7)
            .ThenBy(t => t.Time)
            .Take(MaxTimeslotGroups * TimeslotsPerGroup);

    /// <summary>Saves what the signup modal sent. Times the modal had no box for keep what they were.</summary>
    public async Task SignupDetails(IDiscordModalInteractionContext context, int seasonId)
    {
        var user = await UserGuard(context, context.User.Id);
        if (user is null) return;

        await context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        string? teamName = null;
        var choices = new HashSet<string>();
        var times = new HashSet<int>();
        foreach (var label in context.Components.OfType<Label>())
        {
            switch (label.Component)
            {
                case TextInput { CustomId: TeamNameInput } input:
                    teamName = string.IsNullOrWhiteSpace(input.Value) ? null : input.Value.Trim();
                    break;
                case CheckboxGroup { CustomId: ChoicesInput } group:
                    choices.UnionWith(group.CheckedValues);
                    break;
                case CheckboxGroup group when group.CustomId.StartsWith(TimesInputPrefix, StringComparison.Ordinal):
                    times.UnionWith(group.CheckedValues.Select(int.Parse));
                    break;
            }
        }

        var timeslots = await _signupsService.GetTimeslots(seasonId, 0, user.Id);
        var shown = Shown(timeslots).Select(t => t.OptionID).ToHashSet();
        foreach (var slot in timeslots.Where(t => shown.Contains(t.OptionID)))
            slot.IsChecked = times.Contains(slot.OptionID);

        try
        {
            await _signupsService.UpsertSignup(new SignupRequestDto
            {
                SeasonID = seasonId,
                UserID = user.Id,
                TeamName = teamName,
                WillCaptain = choices.Contains(WillCaptainChoice),
                RequiresAssistanceDrafting = choices.Contains(DraftHelpChoice),
                Timeslots = timeslots,
            });
        }
        catch (SignupsClosedException)
        {
            await Reply(context, "Signups for this season are closed.");
            return;
        }

        var picked = Shown(timeslots).Concat(timeslots.Where(t => !shown.Contains(t.OptionID))).Where(t => t.IsChecked)
            .Select(t => $"{t.DayOfWeek.ToString()[..3]} {t.Time:HH\\:mm}");
        var summary = picked.Any() ? $"Times (UTC): {string.Join(", ", picked)}." : "No times set yet.";
        var more = timeslots.Length > MaxTimeslotGroups * TimeslotsPerGroup
            ? $" This season has more times than fit here: set them {"on the signup form".LinkMarkdown(_urlService.SignupForm(seasonId))}."
            : "";
        var draftHelp = choices.Contains(DraftHelpChoice) && !choices.Contains(WillCaptainChoice) ? " Draft help is only for captains, so it is off." : "";
        await Reply(context, $"Your signup is saved. {summary}{more}{draftHelp}");
    }

    private static Task<IDiscordRestMessage> Reply(IDiscordInteractionContext context, string message)
        => context.Interaction.ModifyResponseAsync(x => x.WithContent(message).WithFlags(MessageFlags.Ephemeral));
}
