using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Commands;
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
/// The season signup button, and the signup modal its reply opens. <see cref="ButtonInteractions"/> and
/// <see cref="ModalInteractions"/> are the NetCord modules; they only wrap their context and call this, so it takes our
/// contexts and tests can pass any implementation.
/// </summary>
public class SignupButtonHandler
{
    /// <summary>The most timeslot boxes the modal shows: Discord allows 5 components, and a box group holds 10 options.</summary>
    public const int MaxTimeslotGroups = 3;
    public const int TimeslotsPerGroup = 10;
    public const string TeamNameInput = "team_name";
    public const string ChoicesInput = "choices";
    public const string WillCaptainChoice = "captain";
    public const string DraftHelpChoice = "draft_help";
    /// <summary>A time group's custom ID: this, then the IDs of the timeslots it shows, so a submit changes only those.</summary>
    public const string TimesInputPrefix = "times:";
    public const string SignupsClosed = "Signups for this season are closed.";

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
            await discordContext.EphemeralResponse(SignupsClosed);
            return;
        }

        await WithDetailsButton(discordContext, seasonId, $"You have signed up for the season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
    }

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

    /// <summary>
    /// Opens the signup modal, filled in with the player's signup as it is: team name, captain and draft help, and the
    /// times they can play (UTC). While signups are closed it says so instead.
    /// </summary>
    public async Task OpenSignupDetails(IDiscordButtonInteractionContext discordContext, int seasonId)
    {
        var user = await UserGuard(discordContext, discordContext.User.Id);
        if (user is null) return;
        if (!await _signupsService.SignupsAreOpen(seasonId))
        {
            await discordContext.EphemeralResponse(SignupsClosed);
            return;
        }

        var signup = await _context.SeasonSignups
            .Where(Domain.League.SeasonSignup.Of(seasonId, user.Id))
            .Select(x => new SignupResponseDto { TeamName = x.TeamName, WillCaptain = x.WillCaptain, RequiresAssistanceDrafting = x.RequiresAssistanceDrafting })
            .AsNoTracking()
            .FirstOrDefaultAsync();
        var timeslots = await _signupsService.GetTimeslots(seasonId, 0, user.Id);
        await discordContext.Interaction.SendResponseAsync(InteractionCallback.Modal(BuildModal(seasonId, signup, timeslots)));
    }

    public static ModalProperties BuildModal(int seasonId, SignupResponseDto? signup, IEnumerable<TimeslotDto> timeslots)
    {
        var teamName = signup?.TeamName;
        var modal = new ModalProperties(DiscordModalsContants.SignupWithParams(seasonId), "Season signup")
        {
            new LabelProperties("Team name (if you captain)", new TextInputProperties(TeamNameInput, TextInputStyle.Short)
            {
                Required = false,
                // A name kept from before the limit, or by a player who isn't captain, still fits: Discord refuses a value longer than this
                MaxLength = Math.Max(Domain.League.Team.TeamNameMaxLength, teamName?.Length ?? 0),
                Value = teamName,
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

        var groups = InWeekOrder(timeslots).Take(MaxTimeslotGroups * TimeslotsPerGroup).Chunk(TimeslotsPerGroup).ToList();
        for (var i = 0; i < groups.Count; i++)
        {
            var label = groups.Count == 1 ? "Times you can play (UTC)" : $"Times you can play (UTC) {i + 1}/{groups.Count}";
            var customId = TimesInputPrefix + string.Join(',', groups[i].Select(t => t.OptionID));
            modal.Add(new LabelProperties(label, new CheckboxGroupProperties(customId,
                groups[i].Select(t => new CheckboxGroupOptionProperties(TimeLabel(t), t.OptionID.ToString()) { Default = t.IsChecked }).ToList())
            {
                MinValues = 0,
                Required = false,
            }));
        }
        return modal;
    }

    private static IOrderedEnumerable<TimeslotDto> InWeekOrder(IEnumerable<TimeslotDto> timeslots)
        => timeslots.OrderBy(t => ((int)t.DayOfWeek + 6) % 7).ThenBy(t => t.Time);

    private static string TimeLabel(TimeslotDto timeslot) => $"{timeslot.DayOfWeek.ToString()[..3]} {timeslot.Time:HH\\:mm}";

    /// <summary>
    /// Saves what the signup modal sent. The times it showed become what was ticked; the player's other times keep their
    /// value, one the season no longer offers included.
    /// </summary>
    public async Task SaveSignupDetails(IDiscordModalInteractionContext context, int seasonId)
    {
        var user = await UserGuard(context, context.User.Id);
        if (user is null) return;
        await context.Defer(ephemeral: true);

        var form = SubmittedForm.Read(context.Components);
        var timeslots = await _signupsService.GetTimeslots(seasonId, 0, user.Id);
        foreach (var slot in timeslots.Where(t => form.Shown.Contains(t.OptionID)))
            slot.IsChecked = form.Ticked.Contains(slot.OptionID);
        var offered = timeslots.Select(t => t.OptionID).ToHashSet();
        var chosen = await _context.SeasonSignups
            .Where(Domain.League.SeasonSignup.Of(seasonId, user.Id))
            .SelectMany(x => x.SignupAvailability.Select(a => a.AvailabilityOptionID))
            .ToListAsync();
        var keptNotOffered = chosen.Where(id => !offered.Contains(id)).Select(id => new TimeslotDto { OptionID = id, IsChecked = true });

        try
        {
            await _signupsService.UpsertSignup(new SignupRequestDto
            {
                SeasonID = seasonId,
                UserID = user.Id,
                TeamName = form.TeamName,
                WillCaptain = form.WillCaptain,
                RequiresAssistanceDrafting = form.DraftHelp,
                Timeslots = [.. timeslots, .. keptNotOffered],
            });
        }
        catch (SignupsClosedException)
        {
            await context.Reply(SignupsClosed);
            return;
        }

        await context.Reply(SavedReply(seasonId, timeslots, form));
    }

    private string SavedReply(int seasonId, TimeslotDto[] timeslots, SubmittedForm form)
    {
        var picked = InWeekOrder(timeslots).Where(t => t.IsChecked).Select(TimeLabel).ToList();
        var summary = picked.Count > 0 ? $"Times (UTC): {string.Join(", ", picked)}." : "No times set yet.";
        var more = timeslots.Length > MaxTimeslotGroups * TimeslotsPerGroup
            ? $" This season has more times than fit here: set them {"on the signup form".LinkMarkdown(_urlService.SignupForm(seasonId))}."
            : "";
        var draftHelp = form.DraftHelp && !form.WillCaptain ? " Draft help is only for captains, so it is off." : "";
        return $"Your signup is saved. {summary}{more}{draftHelp}";
    }

    /// <summary>The signup modal's answers. Time IDs that aren't numbers are left out: only the modal's own IDs count.</summary>
    private sealed record SubmittedForm(string? TeamName, bool WillCaptain, bool DraftHelp, HashSet<int> Shown, HashSet<int> Ticked)
    {
        public static SubmittedForm Read(IEnumerable<IModalComponent> components)
        {
            string? teamName = null;
            var choices = new HashSet<string>();
            var shown = new HashSet<int>();
            var ticked = new HashSet<int>();
            foreach (var component in components.OfType<Label>().Select(l => l.Component))
            {
                switch (component)
                {
                    case TextInput { CustomId: TeamNameInput } input:
                        teamName = input.Value;
                        break;
                    case CheckboxGroup { CustomId: ChoicesInput } group:
                        choices.UnionWith(group.CheckedValues);
                        break;
                    case CheckboxGroup group when group.CustomId.StartsWith(TimesInputPrefix, StringComparison.Ordinal):
                        shown.UnionWith(Ids(group.CustomId[TimesInputPrefix.Length..].Split(',')));
                        ticked.UnionWith(Ids(group.CheckedValues));
                        break;
                }
            }
            return new(teamName, choices.Contains(WillCaptainChoice), choices.Contains(DraftHelpChoice), shown, ticked);
        }

        private static IEnumerable<int> Ids(IEnumerable<string> values)
            => values.Select(v => int.TryParse(v, out var id) ? id : (int?)null).OfType<int>();
    }
}
