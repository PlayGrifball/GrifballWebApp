using GrifballWebApp.Application.Tools;
using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Profile;
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
    private readonly UrlService _urls;
    private readonly PullToolsFor _pulls;

    public PlayerCommandsHandler(CommandLookups lookups, DiscordCaller caller, ProfileStatsService profiles, ProfileCommendationsService commendations,
        DeploymentHistoryService history, VodService vods, UrlService urls, PullToolsFor pulls)
    {
        _pulls = pulls;
        _lookups = lookups;
        _caller = caller;
        _profiles = profiles;
        _commendations = commendations;
        _history = history;
        _vods = vods;
        _urls = urls;
    }

    /// <summary>
    /// The reply deferred, then the player named, else the caller. Null when there is none, the caller told why (no such
    /// player, or the Set Gamertag button for their own).
    /// </summary>
    private async Task<ProfileStatsDto?> PlayerFor(IDiscordInteractionContext context, string? player)
    {
        await context.Defer(ephemeral: false);
        return await UserFor(context, player) is int userID ? (await _profiles.GetAsync(userID))! : null;
    }

    /// <summary>The user named, else the caller (after deferring). Null when there is none, the caller told why.</summary>
    private async Task<int?> UserFor(IDiscordInteractionContext context, string? player)
    {
        if (player is null)
            return (await _caller.Require(context, DiscordCaller.Roles.Anyone))?.UserID;
        if (await _lookups.Player(player) is int found)
            return found;
        await context.Reply(NoSuchPlayer);
        return null;
    }

    /// <summary>The player's gamertag: every player found here has one (<see cref="CommandLookups.Player"/>, <see cref="DiscordCaller"/>).</summary>
    private static string Name(ProfileStatsDto profile) => profile.Gamertag!;

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
        var description = CommandReplies.Lines(lines, "No medals yet.", head: $"{commendations.TotalMedals} medals, {commendations.DistinctMedals} kinds\n\n");
        await context.Reply(CommandReplies.Embed($"{Name(p)} commendations", _urls.Profile(p.UserID), description));
    }

    public async Task History(IDiscordInteractionContext context, string? player)
    {
        if (await PlayerFor(context, player) is not { } p)
            return;
        var history = (await _history.GetAsync(p.UserID))!;
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

    /// <summary>
    /// Queues the pull of an Xbox account's recent games from Halo, as its page's button does: the agents' pull_xbox_account_games,
    /// as the caller (players and staff). The account picked or typed by its current gamertag (<see cref="CommandLookups.XboxAccount"/>),
    /// else the caller's own.
    /// </summary>
    public async Task Pull(IDiscordInteractionContext context, string? gamertag)
    {
        await context.Defer(ephemeral: true);
        if (await _caller.Require(context, DiscordCaller.Roles.Pullers) is not { } caller)
            return;
        var account = string.IsNullOrWhiteSpace(gamertag)
            ? await _lookups.OwnXboxAccount(context.Interaction.User.Id)
            : await _lookups.XboxAccount(gamertag);
        if (account is not { } found)
        {
            await context.Reply(CommandLookups.NoSuchXboxAccount);
            return;
        }
        try
        {
            var pulled = await _pulls.As(caller).PullXboxAccountGamesOf(found.XboxUserID, found.Gamertag);
            await context.Reply($"The pull of {pulled.Gamertag}'s recent games {PullReplies.State(pulled.Pull)}. /matches and their Xbox page show them: {_urls.XboxAccount(pulled.XboxUserID)}");
        }
        catch (ToolException e)
        {
            await context.Reply(e.Message);
        }
    }
}
