using DiscordInterfaces;
using GrifballWebApp.Domain.Halo;
using NetCord;
using NetCord.Rest;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server;

/// <summary>
/// The slash commands' logic. <see cref="DiscordCommands"/> is the NetCord module; it only wraps its context and calls this,
/// so it takes our <see cref="IDiscordApplicationCommandContext"/> and tests can pass a substitute.
/// /setgamertag goes straight to <see cref="Matchmaking.DiscordSetGamertag"/>, which the modal shares.
/// </summary>
public class DiscordCommandsHandler
{
    /// <summary>Discord rejects an embed field with no value (400): a zero-width space stands in for "nothing".</summary>
    internal const string EmptyFieldValue = "\u200B";

    public const string NoGamertag = "You must set your gamertag first, or give someone else's gamertag.";

    private readonly CommandLookups _lookups;
    private readonly HaloTools _halo;

    public DiscordCommandsHandler(CommandLookups lookups, HaloTools halo)
    {
        _lookups = lookups;
        _halo = halo;
    }

    /// <summary>
    /// /matches: an Xbox account's saved games, as the site has them, through the agents' get_recent_games
    /// (<see cref="HaloTools.GetRecentGamesOf"/>). The account picked or typed by its current gamertag
    /// (<see cref="CommandLookups.XboxAccount"/>), else the caller's own. It pulls nothing from Halo: /player pull (or a
    /// page's button) does.
    /// </summary>
    public async Task RecentMatches(IDiscordApplicationCommandContext discordContext, string? gamertag)
    {
        await discordContext.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());
        var account = string.IsNullOrWhiteSpace(gamertag)
            ? await _lookups.OwnXboxAccount(discordContext.User.Id)
            : await _lookups.XboxAccount(gamertag);
        if (account is not { } found)
        {
            var reply = string.IsNullOrWhiteSpace(gamertag) ? NoGamertag : CommandLookups.NoSuchXboxAccount;
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent(reply));
            return;
        }

        var recent = await _halo.GetRecentGamesOf(found.XboxUserID, found.Gamertag, 5);

        var list = recent.Games.Select(game =>
        {
            var embed = new EmbedProperties
            {
                Title = "Halo Infinite Match",
                Color = game.Outcome switch
                {
                    Outcomes.Won => new Color(0, 255, 0), // Green for win
                    Outcomes.Lost => new Color(255, 0, 0), // Red for loss
                    Outcomes.Tie => new Color(255, 255, 0), // Yellow for tie
                    _ => new Color(255, 255, 255), // White for anything else
                },
                Fields =
                [
                    new EmbedFieldProperties().WithName("Match ID").WithValue(game.MatchID.ToString().LinkMarkdown(game.WaypointUrl)).WithInline(true),
                    new EmbedFieldProperties().WithName("Start Time").WithValue(game.StartTime?.DiscordTimeEmbed() ?? "Unknown").WithInline(true),
                ],
            };
            foreach (var (team, index) in game.Teams.Select((team, index) => (team, index)))
            {
                embed.Fields = embed.Fields.Append(new EmbedFieldProperties().WithName("Team").WithValue(team.TeamID.ToString()).WithInline(true));
                embed.Fields = embed.Fields.Append(new EmbedFieldProperties().WithName("Score").WithValue(team.Score.ToString()).WithInline(true));
                embed.Fields = embed.Fields.Append(new EmbedFieldProperties().WithName("Outcome").WithValue(team.Outcome.ToString()).WithInline(true));
                var gamertags = string.Join(", ", team.Players);
                embed.Fields = embed.Fields.Append(new EmbedFieldProperties().WithName("Players").WithValue(string.IsNullOrEmpty(gamertags) ? EmptyFieldValue : gamertags).WithInline(false));
                if (index != game.Teams.Count - 1)
                    embed.Fields = embed.Fields.Append(new EmbedFieldProperties().WithName("**-------------------------**").WithValue(EmptyFieldValue).WithInline(false));
            }
            return embed;
        }).ToList();

        if (list.Count > 0)
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithEmbeds(list));
        else
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent($"The site has no games saved for {recent.Gamertag}."));
    }
}
