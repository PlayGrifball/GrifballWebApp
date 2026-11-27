using System.Globalization;
using DiscordInterface.Generated;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Scheduling;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// A season match's best times on Discord (<see cref="SchedulingService.SuggestAsync"/>): /reschedule suggest lists them, and
/// /reschedule request offers them as new_time's choices (<see cref="RescheduleTimeAutocomplete"/>).
/// </summary>
public class RescheduleSuggestions(CommandLookups lookups, SchedulingService scheduling)
{
    /// <summary>/reschedule suggest: the match's best times, each with how many of each team's players can play and its warnings.</summary>
    public async Task Suggest(IDiscordInteractionContext context, string match)
    {
        await context.Defer(ephemeral: true);
        if (await lookups.SeasonMatchFor(context, match) is not int seasonMatchID)
            return;
        var suggestions = await scheduling.SuggestAsync(seasonMatchID) ?? [];
        if (suggestions.Count == 0)
        {
            await context.Reply("I have no time to suggest for that match: its season has no timeslots left, or it's missing a team.");
            return;
        }
        var lines = suggestions.Select((s, i) => $"{i + 1}. {s.StartsAt.DiscordTimeEmbed()}: {Players(s)}"
            + string.Concat(s.Conflicts.Select(c => $"\n   ⚠ {c.Message}")));
        await context.Reply($"The best times for that match, best first (in your time zone):\n{string.Join('\n', lines)}\n"
            + "Ask for one with /reschedule request: its new_time lists them.");
    }

    /// <summary>
    /// new_time's choices for <paramref name="match"/> (the match option's value): its best times, each named in league time with
    /// its players and a ⚠ for each warning, its value the UTC time the request reads; those with <paramref name="typed"/> in them.
    /// None until a match is picked.
    /// </summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> TimeChoices(string? match, string? typed)
    {
        if (string.IsNullOrWhiteSpace(match) || await lookups.SeasonMatch(match) is not int seasonMatchID)
            return [];
        var league = ScheduleService.LeagueTimeZone();
        var text = (typed ?? "").Trim();
        return (await scheduling.SuggestAsync(seasonMatchID, count: 25) ?? [])
            .Select(s => new ApplicationCommandOptionChoiceProperties(CommandLookups.ChoiceName(
                    TimeZoneInfo.ConvertTimeFromUtc(s.StartsAt, league).ToString("ddd d MMM, h:mm tt", CultureInfo.InvariantCulture) + " ET · " + Players(s)
                    + string.Concat(s.Conflicts.Select(_ => " ⚠"))),
                s.StartsAt.ToString(CommandLookups.UtcFormat, CultureInfo.InvariantCulture)))
            .Where(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || c.StringValue!.Contains(text, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>The match's best time in the UTC form a request reads (what the request form starts with); null when there is none.</summary>
    public async Task<string?> BestTime(int seasonMatchID)
        => await scheduling.SuggestAsync(seasonMatchID, count: 1) is [var best, ..] ? best.StartsAt.ToString(CommandLookups.UtcFormat, CultureInfo.InvariantCulture) : null;

    private static string Players(TimeSuggestion s) => $"{s.HomePlayersAvailable} and {s.AwayPlayersAvailable} players can play";
}

/// <summary>new_time's choices on /reschedule request: <see cref="RescheduleSuggestions.TimeChoices"/> for the match picked.</summary>
public class RescheduleTimeAutocomplete(RescheduleSuggestions suggestions) : IAutocompleteProvider<AutocompleteInteractionContext>
{
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await suggestions.TimeChoices(MatchPicked(context.Interaction.Data.Options), option.Value);

    /// <summary>The match option's value among the command's options (a subcommand's too); null before one is picked.</summary>
    public static string? MatchPicked(IEnumerable<ApplicationCommandInteractionDataOption> options) => Find(options, "match")?.Value;

    private static ApplicationCommandInteractionDataOption? Find(IEnumerable<ApplicationCommandInteractionDataOption> options, string name)
        => options.Select(o => o.Name == name ? o : Find(o.Options ?? [], name)).FirstOrDefault(o => o is not null);
}
