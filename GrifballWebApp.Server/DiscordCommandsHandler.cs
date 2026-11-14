using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Tools;

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

    private readonly GrifballContext _context;
    private readonly HaloTools _halo;

    public DiscordCommandsHandler(GrifballContext context, HaloTools halo)
    {
        _context = context;
        _halo = halo;
    }

    /// <summary>The gamertag the command asks about: the one given, else the caller's own (their linked account's); null for none.</summary>
    private async Task<string?> GamertagFor(IDiscordApplicationCommandContext discordContext, string? gamertag)
        => !string.IsNullOrWhiteSpace(gamertag)
            ? gamertag
            : await _context.DiscordUsers
                .Where(x => x.DiscordUserID == (long)discordContext.User.Id)
                .Select(x => x.User!.XboxUser!.Gamertag)
                .FirstOrDefaultAsync();

    public async Task RecentMatches(IDiscordApplicationCommandContext discordContext, string? gamertag)
    {
        await discordContext.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());
        await RecentMatchesDeferred(discordContext, gamertag);
    }

    /// <summary>
    /// <see cref="RecentMatches"/> once the reply is deferred (/player matches defers before looking the player up): the player's
    /// saved games, as the site has them, through the agents' get_recent_games (<see cref="HaloTools.GetRecentGames"/>). It
    /// pulls nothing from Halo: /player pull (or a page's button) does.
    /// </summary>
    public async Task RecentMatchesDeferred(IDiscordApplicationCommandContext discordContext, string? gamertag)
    {
        if (await GamertagFor(discordContext, gamertag) is not { } tag)
        {
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent(NoGamertag));
            return;
        }

        RecentGamesDto recent;
        try
        {
            recent = await _halo.GetRecentGames(tag, 5);
        }
        catch (ToolException e)
        {
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent(e.Message));
            return;
        }

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
