using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

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

    private readonly GrifballContext _context;
    private readonly ILogger<DiscordCommandsHandler> _logger;
    private readonly IDataPullService _dataPullService;
    private readonly IGetsertXboxUserService _getsertXboxUserService;
    public DiscordCommandsHandler(GrifballContext context,
        ILogger<DiscordCommandsHandler> logger,
        IDataPullService dataPullService,
        IGetsertXboxUserService getsertXboxUserService)
    {
        _context = context;
        _logger = logger;
        _dataPullService = dataPullService;
        _getsertXboxUserService = getsertXboxUserService;
    }

    public async Task RecentMatches(IDiscordApplicationCommandContext discordContext, string? gamertag)
    {
        await discordContext.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        XboxUser? xboxUser = null;
        string? msg = null;

        if (!string.IsNullOrWhiteSpace(gamertag))
        {
            (xboxUser, msg) = await _getsertXboxUserService.GetsertXboxUserByGamertag(gamertag);
            if (xboxUser is null)
            {
                // NetCord takes a null content too; the generated IDiscordMessageOptions just lost the annotation
                await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent(msg!));
                return;
            }
        }
        else
        {
            xboxUser = await _context.DiscordUsers
                .Where(x => x.DiscordUserID == (long)discordContext.User.Id)
                .Select(x => x.User!.XboxUser)
                .FirstOrDefaultAsync();
            if (xboxUser == null)
            {
                await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent("You must set your gamertag first, or provide someone else's gamertag to search for"));
                return;
            }
        } 

        try
        {
            await _dataPullService.DownloadRecentMatchesForPlayers([xboxUser.XboxUserID], startPage: 0, endPage: 0, 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent matches for {Gamertag}", xboxUser.Gamertag);
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent("Failed to fetch recent matches, if this continues to occur contact sysadmin"));
            return;
        }

        var matches = await _context.Matches
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
                        .ThenInclude(x => x.User)
            .Where(x => x.MatchTeams.Any(x => x.MatchParticipants.Any(x => x.XboxUserID == xboxUser.XboxUserID)))
            .OrderByDescending(x => x.StartTime)
            .Take(5)
            .AsNoTracking()
            .ToListAsync();

        var list = new List<EmbedProperties>();

        foreach (var match in matches)
        {
            var outcome = match.MatchTeams.Where(x => x.MatchParticipants.Any(x => x.XboxUserID == xboxUser.XboxUserID)).Select(x => x.Outcome).FirstOrDefault();
            var color = outcome switch
            {
                Outcomes.Won => new Color(0, 255, 0), // Green for win
                Outcomes.Lost => new Color(255, 0, 0), // Red for loss
                Outcomes.Tie => new Color(255, 255, 0), // Yellow for tie
                _ => new Color(255, 255, 255) // Default to white for unknown
            };

            var url = $"https://www.halowaypoint.com/halo-infinite/players/{Uri.EscapeDataString(xboxUser.Gamertag)}/matches/{match.MatchID}";
            var emded = new EmbedProperties()
            {
                Title = "Halo Infinite Match",
                Fields = [
                    new EmbedFieldProperties().WithName("Match ID").WithValue(match.MatchID.ToString().LinkMarkdown(url)).WithInline(true),
                    new EmbedFieldProperties().WithName("Start Time").WithValue(match.StartTime?.DiscordTimeEmbed()).WithInline(true),
                    new EmbedFieldProperties().WithName("Duration").WithValue(match.Duration.ToString()).WithInline(true),
                    ],
                Color = color,
            };

            foreach (var (team, index) in match.MatchTeams.Select((team, index) => (team, index)))
            {
                // Add team information to the embed
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Team").WithValue(team.TeamID.ToString()).WithInline(true));
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Score").WithValue(team.Score.ToString()).WithInline(true));
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Outcome").WithValue(team.Outcome.ToString()).WithInline(true));
                var gts = string.Join(", ", team.MatchParticipants.Select(x => x.XboxUser.Gamertag));
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Players").WithValue(string.IsNullOrEmpty(gts) ? EmptyFieldValue : gts).WithInline(false));
                if (index != match.MatchTeams.Count - 1)
                {
                    emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("**-------------------------**").WithValue(EmptyFieldValue).WithInline(false));
                }
            }

            list.Add(emded);
        }

        if (list.Any())
        {
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithEmbeds(list));
        }
        else
        {
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent($"I found no matches for {gamertag}"));
        }
    }
}
