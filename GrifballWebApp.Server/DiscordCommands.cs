using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server;

public class DiscordCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly GrifballContext _context;
    private readonly HaloInfiniteClientFactory _clientFactory;
    private readonly ILogger<DiscordCommands> _logger;
    private readonly DataPullService _dataPullService;
    public DiscordCommands(GrifballContext context, HaloInfiniteClientFactory clientFactory, ILogger<DiscordCommands> logger, DataPullService dataPullService)
    {
        _context = context;
        _clientFactory = clientFactory;
        _logger = logger;
        _dataPullService = dataPullService;
    }

    [SlashCommand("test", "A test command")]
    public async Task<string> Test()
    {
        await Task.Delay(3000);
        return "foo";
    }

    [SlashCommand("matches", "Get your recent matches")]
    public async Task<InteractionCallback> RecentMatches()
    {
        var xboxUser = await _context.DiscordUsers
            .Where(x => x.DiscordUserID == (long)Context.User.Id)
            .Select(x => x.XboxUser)
            .FirstOrDefaultAsync();

        if (xboxUser == null)
        {
            return InteractionCallback.Message("You must set your gamertag first");
        }

        await _dataPullService.DownloadRecentMatchesForPlayers([xboxUser.XboxUserID], startPage: 0, endPage: 0, 5);

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

        var last = matches.FirstOrDefault()?.StartTime?.ToString() ?? "No idea";

        var prop = new InteractionMessageProperties();

        foreach(var match in matches)
        {
            var outcome = match.MatchTeams.Where(x => x.MatchParticipants.Any(x => x.XboxUserID == xboxUser.XboxUserID)).Select(x => x.Outcome).FirstOrDefault();
            var color = outcome switch
            {
                Outcomes.Won => new Color(0, 255, 0), // Green for win
                Outcomes.Lost => new Color(255, 0, 0), // Red for loss
                Outcomes.Tie => new Color(255, 255, 0), // Yellow for tie
                _ => new Color(255, 255, 255) // Default to white for unknown
            };

            var emded = new EmbedProperties()
            {
                Title = "Halo Infinite Match",
                Fields = [
                    new EmbedFieldProperties().WithName("Match ID").WithValue(match.MatchID.ToString()).WithInline(true),
                    new EmbedFieldProperties().WithName("Start Time").WithValue(match.StartTime?.DiscordTimeEmbed()).WithInline(true),
                    new EmbedFieldProperties().WithName("Duration").WithValue(match.Duration.ToString()).WithInline(true),
                    ],
                Color = color,
            };
            foreach (var team in match.MatchTeams)
            {
                // Add team information to the embed
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Team").WithValue(team.TeamID.ToString()).WithInline(true));
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Score").WithValue(team.Score.ToString()).WithInline(true));
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Outcome").WithValue(team.Outcome.ToString()).WithInline(true));
                var gts = string.Join(", ", team.MatchParticipants.Select(x => x.XboxUser.Gamertag));
                emded.Fields = emded.Fields.Append(new EmbedFieldProperties().WithName("Players").WithValue(gts).WithInline(false));
            }

            prop = prop.AddEmbeds([emded]);
        }

        return InteractionCallback.Message(prop);
    }

    [SlashCommand("setgamertag", "Set your gamertag")]
    public async Task<string> SetGamertag(string gamertag)
    {
        var discordUser = await _context.DiscordUsers
            .Where(x => x.DiscordUserID == (long)Context.User.Id)
            .FirstOrDefaultAsync();

        if (discordUser == null)
        {
            discordUser = new DiscordUser()
            {
                DiscordUserID = (long)Context.User.Id,
                DiscordUsername = Context.User.Username
            };
            _context.DiscordUsers.Add(discordUser);
        }

        var xboxUser = await _context.XboxUsers
            .Where(x => x.Gamertag == gamertag)
            .FirstOrDefaultAsync();

        if (xboxUser is not null)
        {
            discordUser.XboxUser = xboxUser;
            await _context.SaveChangesAsync();
            return "Set Gamertag Successfully";
        }

        HaloInfiniteClient? client = await _clientFactory.CreateAsync();
        try
        {
            client = await _clientFactory.CreateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to create Halo Infinite Client");
            return "Fatal Error creating Infinite Client, contact sysadmin";
        }


        var user = await client.UserByGamertag(gamertag);
        if (user.Result is null)
            return "Gamertag not found";

        xboxUser = new XboxUser()
        {
            XboxUserID = long.Parse(user.Result.xuid),
            Gamertag = user.Result.gamertag,
        };
        _context.XboxUsers.Add(xboxUser);

        discordUser.XboxUser = xboxUser;

        await _context.SaveChangesAsync();

        return "Set Gamertag Successfully";
    }
}

public static class Ext
{
    public static long ToUnix(this DateTime toUnix)
    {
        return new DateTimeOffset(toUnix, TimeSpan.FromTicks(0)).ToUnixTimeSeconds();
    }

    public static string DiscordTimeEmbed(this DateTime t)
    {
        return $"<t:{t.ToUnix()}>";
    }

    public static string LinkMarkdown(this string display, string url)
    {
        if (url is null)
            return display;
        else
            return $"[{display}]({url})";
    }
}