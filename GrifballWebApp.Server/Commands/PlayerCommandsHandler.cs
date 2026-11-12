using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Server.Extensions;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /player: a player's profile, open to everyone as it is on the site, through the profile's services. With no player
/// named, the caller's own (which needs their gamertag set, as the queue does).
/// <see cref="PlayerCommands"/> is the NetCord module; it only wraps its context and calls these.
/// </summary>
public class PlayerCommandsHandler
{
    public const string NoSuchPlayer = "I found no such player.";

    private readonly CommandLookups _lookups;
    private readonly DiscordCaller _caller;
    private readonly ProfileStatsService _profiles;
    private readonly ProfileCommendationsService _commendations;
    private readonly DeploymentHistoryService _history;
    private readonly VodService _vods;
    private readonly DiscordCommandsHandler _recentMatches;
    private readonly UrlService _urls;

    public PlayerCommandsHandler(CommandLookups lookups, DiscordCaller caller, ProfileStatsService profiles, ProfileCommendationsService commendations,
        DeploymentHistoryService history, VodService vods, DiscordCommandsHandler recentMatches, UrlService urls)
    {
        _lookups = lookups;
        _caller = caller;
        _profiles = profiles;
        _commendations = commendations;
        _history = history;
        _vods = vods;
        _recentMatches = recentMatches;
        _urls = urls;
    }

    /// <summary>
    /// The player named, else the caller; then the reply deferred. Null when there is none, the caller told why (no such
    /// player, or the Set Gamertag button for their own).
    /// </summary>
    private async Task<ProfileStatsDto?> PlayerFor(IDiscordInteractionContext context, string? player)
    {
        int userID;
        if (player is null)
        {
            if (await _caller.Require(context, DiscordCaller.Roles.Anyone) is not { } caller)
                return null;
            userID = caller.UserID;
        }
        else if (await _lookups.Player(player) is int found)
            userID = found;
        else
        {
            await context.EphemeralResponse(NoSuchPlayer);
            return null;
        }

        await context.Defer(ephemeral: false);
        return (await _profiles.GetAsync(userID))!;
    }

    private static string Name(ProfileStatsDto profile) => profile.Gamertag ?? profile.DisplayName ?? $"Player {profile.UserID}";

    public async Task Profile(IDiscordInteractionContext context, string? player)
    {
        if (await PlayerFor(context, player) is not { } p)
            return;
        var embed = CommandReplies.Embed(Name(p), _urls.Profile(p.UserID));
        var career = p.OverallCareer;
        embed.Fields =
        [
            new EmbedFieldProperties().WithName("Region").WithValue(p.RegionName ?? "Not set").WithInline(true),
            new EmbedFieldProperties().WithName("Championships").WithValue(p.Championships.Count.ToString()).WithInline(true),
            new EmbedFieldProperties().WithName("Seasons").WithValue((career?.SeasonsCount ?? 0).ToString()).WithInline(true),
            .. career is null ? Array.Empty<EmbedFieldProperties>() :
            [
                new EmbedFieldProperties().WithName("Record").WithValue($"{career.Wins}-{career.Losses}" + (career.WinPercent is { } pct ? $" ({pct:0.#}%)" : "")).WithInline(true),
                new EmbedFieldProperties().WithName("Goals").WithValue((career.Goals ?? 0).ToString()).WithInline(true),
                new EmbedFieldProperties().WithName("K/D").WithValue(career.KdRatio is { } kd ? $"{kd:0.##}" : "-").WithInline(true),
            ],
            .. p.PastGamertags.Count > 0 ? [new EmbedFieldProperties().WithName("Also known as").WithValue(string.Join(", ", p.PastGamertags)).WithInline(false)] : Array.Empty<EmbedFieldProperties>(),
        ];
        await context.Reply(embed);
    }

    public async Task Stats(IDiscordInteractionContext context, string? player)
    {
        if (await PlayerFor(context, player) is not { } p)
            return;
        var lines = p.CareerByGame
            .Select(x => $"**{x.GameVersionName ?? "Other"}** · {x.SeasonsCount} seasons · {x.Wins}-{x.Losses} · {x.Goals ?? 0} goals · "
                + $"{x.Kills} kills · K/D {(x.KdRatio is { } kd ? $"{kd:0.##}" : "-")}")
            .ToList();
        await context.Reply(CommandReplies.Embed($"{Name(p)} stats", _urls.Profile(p.UserID), CommandReplies.Lines(lines, "No stats yet.")));
    }

    public async Task Commendations(IDiscordInteractionContext context, string? player)
    {
        if (await PlayerFor(context, player) is not { } p)
            return;
        var commendations = await _commendations.GetAsync(p.UserID);
        var lines = commendations.Commendations
            .Select(x => $"{x.MedalName} · {x.Count}" + (x.Tier > 0 ? $" · tier {x.Tier}" : ""))
            .ToList();
        var description = $"{commendations.TotalMedals} medals, {commendations.DistinctMedals} kinds\n\n" + CommandReplies.Lines(lines, "No medals yet.");
        await context.Reply(CommandReplies.Embed($"{Name(p)} commendations", _urls.Profile(p.UserID), description));
    }

    public async Task History(IDiscordInteractionContext context, string? player)
    {
        if (await PlayerFor(context, player) is not { } p)
            return;
        var history = await _history.GetAsync(p.UserID) ?? [];
        var lines = history
            .Select(x => $"{x.Season.SeasonName} · {x.TeamName.LinkMarkdown(_urls.Team(x.Season.SeasonID, x.TeamID))}{(x.IsCaptain ? " (captain)" : "")}"
                + $" · {x.Wins}-{x.Losses} · {x.Finish}" + (x.ChampionshipTiers.Count > 0 ? $" · {string.Join(", ", x.ChampionshipTiers)} champion" : ""))
            .ToList();
        await context.Reply(CommandReplies.Embed($"{Name(p)} deployment history", _urls.Profile(p.UserID), CommandReplies.Lines(lines, "No seasons yet.")));
    }

    public async Task Vods(IDiscordInteractionContext context, string? player)
    {
        if (await PlayerFor(context, player) is not { } p)
            return;
        var vods = await _vods.ForPlayer(p.UserID);
        await context.Reply(CommandReplies.Embed($"{Name(p)} VODs", _urls.Profile(p.UserID), TeamCommandsHandler.VodLines(vods)));
    }

    /// <summary>The player's recent Halo matches: /matches, by the player's gamertag.</summary>
    public async Task Matches(IDiscordApplicationCommandContext context, string? player)
    {
        string? gamertag = null;
        if (player is not null)
        {
            if (await _lookups.Player(player) is not int userID)
            {
                await context.EphemeralResponse(NoSuchPlayer);
                return;
            }
            gamertag = (await _profiles.GetAsync(userID))?.Gamertag;
        }
        await _recentMatches.RecentMatches(context, gamertag);
    }
}
