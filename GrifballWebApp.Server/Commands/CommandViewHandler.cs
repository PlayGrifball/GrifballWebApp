using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Server.Extensions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// A click on a list's ◀ ▶ or a pick in its menus (<see cref="CommandView"/>): the view re-rendered from the custom ID and the pick, in place
/// of the message. A season or team is checked again as anyone sees it (the message is the channel's), so one not there any more says so.
/// Its NetCord modules (<see cref="CommandViewButtonInteractions"/>, <see cref="CommandViewStringMenuInteractions"/>) only wrap their context and call these.
/// </summary>
public class CommandViewHandler
{
    public const string Gone = "That isn't there any more: run the command again.";

    private readonly CommandLookups _lookups;
    private readonly SeasonVisibility _visibility;
    private readonly SeasonCommandsHandler _seasons;
    private readonly TeamCommandsHandler _teams;
    private readonly PlayerCommandsHandler _players;

    public CommandViewHandler(CommandLookups lookups, SeasonVisibility visibility, SeasonCommandsHandler seasons, TeamCommandsHandler teams,
        PlayerCommandsHandler players)
    {
        _lookups = lookups;
        _visibility = visibility;
        _seasons = seasons;
        _teams = teams;
        _players = players;
    }

    /// <summary>Page <paramref name="page"/> of list <paramref name="view"/> of <paramref name="key"/> (the season's or the team's ID).</summary>
    public async Task Page(IDiscordInteractionContext context, string view, string key, int page)
    {
        CommandView? shown = null;
        if (int.TryParse(key, System.Globalization.CultureInfo.InvariantCulture, out var id))
            shown = view == TeamCommandsHandler.RosterView ? await RosterView(id, page) : await SeasonView(view, id, page);
        await ShowOrSay(context, shown);
    }

    /// <summary>The same season list for the season picked (its choice value, as the season menu gives it).</summary>
    public async Task PickSeason(IDiscordStringMenuInteractionContext context, string view)
        => await ShowOrSay(context, await _lookups.Season(context.SelectedValues[0]) is int id ? await _seasons.View(view, id, 0) : null);

    /// <summary>Season <paramref name="seasonID"/>'s players ranked by the stat picked.</summary>
    public async Task PickStat(IDiscordStringMenuInteractionContext context, int seasonID)
        => await ShowOrSay(context, SeasonCommandsHandler.ParseStat(context.SelectedValues[0]) is { } stat
            ? await SeasonView(SeasonCommandsHandler.StatsView(stat), seasonID, 0)
            : null);

    /// <summary>Player <paramref name="userID"/>'s view picked.</summary>
    public async Task PickPlayerView(IDiscordStringMenuInteractionContext context, int userID)
        => await ShowOrSay(context, await _players.View(context.SelectedValues[0], userID));

    private async Task<CommandView?> SeasonView(string view, int seasonID, int page)
        => await _visibility.CanSeeSeason(seasonID, SeasonViewer.Public) ? await _seasons.View(view, seasonID, page) : null;

    private async Task<CommandView?> RosterView(int teamID, int page)
        => await _visibility.CanSeeTeam(teamID, SeasonViewer.Public) ? await _teams.ViewRoster(teamID, page) : null;

    private static Task ShowOrSay(IDiscordInteractionContext context, CommandView? view)
        => view is null ? context.EphemeralResponse(Gone) : context.Show(view);
}
