using DiscordInterface.Generated;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// The season, team, player and season match a command names. A choice picked from the autocomplete is sent as its ID
/// (<see cref="ChoiceValue(int)"/>: "id:12"); anything else is what was typed, looked up by name: the exact name (a team's or
/// season's newest), else the only one whose name has it, else, for digits that name nothing, the ID they are. A typed name
/// that fits several is none: the caller is told to pick one, so an action never lands on a guess.
/// String options, not integer ones: Discord lets only digits be typed into an integer option, so names couldn't be searched.
/// Names match whatever their case: the database compares text case-insensitively (CaseInsensitiveTextTests).
/// Seasons, teams and season matches are those the <see cref="SeasonViewer"/> may see: a season not announced yet, its teams
/// and matches are no such thing to anyone but staff (#435). A reply the channel reads is <see cref="SeasonViewer.Public"/>.
/// </summary>
public class CommandLookups
{
    public const string NoSuchSeason = "I found no single season for that: pick one from the list.";
    public const string NoSuchMatch = "I found no single match for that: pick one from the list.";

    /// <summary>What a picked choice's value starts with.</summary>
    public const string ChoicePrefix = "id:";

    /// <summary>Discord shows at most 25 choices.</summary>
    private const int MaxChoices = 25;

    public const string NoSuchXboxAccount = "I found no Xbox account with that gamertag: pick one from the list (past gamertags are on it too).";

    /// <summary>What a past gamertag's choice says after it (<see cref="XboxAccountChoices"/>).</summary>
    public const string PreviousGamertag = "(Previous Gamertag)";

    private readonly GrifballContext _context;
    private readonly SeasonService _seasons;
    private readonly XboxAccountService _accounts;
    private readonly SeasonVisibility _visibility;

    public CommandLookups(GrifballContext context, SeasonService seasons, XboxAccountService accounts, SeasonVisibility visibility)
    {
        _visibility = visibility;
        _context = context;
        _seasons = seasons;
        _accounts = accounts;
    }

    /// <summary>A choice's value: the ID, marked as picked so that a name of digits is still a name.</summary>
    public static string ChoiceValue(int id) => ChoicePrefix + id;

    /// <summary>An Xbox account choice's value: its XUID, marked as picked.</summary>
    public static string ChoiceValue(long xuid) => ChoicePrefix + xuid;

    private static int? Picked(string input)
        => input.StartsWith(ChoicePrefix, StringComparison.Ordinal) && int.TryParse(input[ChoicePrefix.Length..], out var id) ? id : null;

    /// <summary>
    /// The Xbox account picked (its XUID, which may have been listed by a past gamertag), else the one whose current gamertag
    /// was typed: a typed past gamertag finds nothing, so an account is never found by an old name it isn't shown under.
    /// Null when there is none.
    /// </summary>
    public async Task<(long XboxUserID, string Gamertag)?> XboxAccount(string input, CancellationToken ct = default)
    {
        var typed = input.Trim();
        if (typed.StartsWith(ChoicePrefix, StringComparison.Ordinal) && long.TryParse(typed[ChoicePrefix.Length..], out var xuid))
        {
            var gamertag = await _context.XboxUsers.AsNoTracking().Where(x => x.XboxUserID == xuid).Select(x => x.Gamertag).FirstOrDefaultAsync(ct);
            return gamertag is null ? null : (xuid, gamertag);
        }
        return await _accounts.FindByCurrentGamertagAsync(typed, ct);
    }

    /// <summary>The Xbox account of the user linked to this Discord account; null when none is linked, or it has no Xbox account.</summary>
    public async Task<(long XboxUserID, string Gamertag)?> OwnXboxAccount(ulong discordUserID, CancellationToken ct = default)
    {
        var own = await _context.Users.AsNoTracking()
            .Where(x => x.DiscordUserID == (long)discordUserID && x.XboxUser != null)
            .Select(x => new { x.XboxUser!.XboxUserID, x.XboxUser.Gamertag })
            .FirstOrDefaultAsync(ct);
        return own is null ? null : (own.XboxUserID, own.Gamertag);
    }

    /// <summary>
    /// The Xbox accounts a gamertag option lists (<see cref="XboxAccountService.SearchAsync"/>), each sent as its XUID: by
    /// current gamertag, then by a past one, shown as "Old (Previous Gamertag) · now Current".
    /// </summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> XboxAccountChoices(string? input, CancellationToken ct = default)
        => (await _accounts.SearchAsync(input, MaxChoices, ct))
            .Select(x => new ApplicationCommandOptionChoiceProperties(
                ChoiceName(x.PastGamertag is null ? x.Gamertag : $"{x.PastGamertag} {PreviousGamertag} · now {x.Gamertag}"), ChoiceValue(x.XboxUserID)))
            .ToList();

    /// <summary>The only one of <paramref name="ids"/>, or null when there are none or several.</summary>
    private static async Task<int?> OnlyOne(IQueryable<int> ids, CancellationToken ct)
    {
        var two = await ids.Take(2).ToListAsync(ct);
        return two.Count == 1 ? two[0] : null;
    }

    /// <summary>The seasons <paramref name="viewer"/> may see (<see cref="SeasonVisibility.Visible"/>).</summary>
    private IQueryable<GrifballWebApp.Domain.League.Season> Visible(SeasonViewer viewer) => _visibility.Visible(viewer);

    /// <summary>
    /// The season named or picked, of those <paramref name="viewer"/> may see; with none given, the one <paramref name="purpose"/>
    /// means (<see cref="SeasonService.GetDefaultSeasonID"/>, the agents' tools' default too). Null when there is no single such season.
    /// </summary>
    public async Task<int?> Season(string? input, SeasonPurpose purpose = SeasonPurpose.Current, SeasonViewer viewer = SeasonViewer.Public,
        CancellationToken ct = default)
    {
        var seasons = Visible(viewer);
        if (string.IsNullOrWhiteSpace(input))
        {
            var meant = await _seasons.GetDefaultSeasonID(purpose, viewer, ct);
            return meant != 0 ? meant : null;
        }

        var name = input.Trim();
        var id = Picked(name);
        if (id is null)
        {
            id = await seasons.Where(x => x.SeasonName == name).OrderByDescending(x => x.SeasonID).Select(x => (int?)x.SeasonID).FirstOrDefaultAsync(ct)
                ?? await OnlyOne(seasons.Where(x => x.SeasonName.Contains(name)).Select(x => x.SeasonID), ct);
            if (id is not null)
                return id;
            if (!int.TryParse(name, out var typed))
                return null;
            id = typed;
        }
        return await seasons.Where(x => x.SeasonID == id).Select(x => (int?)x.SeasonID).FirstOrDefaultAsync(ct);
    }

    /// <summary>The season named or picked, or null with the caller told there is no single one (call after deferring).</summary>
    public async Task<int?> SeasonFor(IDiscordInteractionContext context, string? input, SeasonPurpose purpose = SeasonPurpose.Current,
        SeasonViewer viewer = SeasonViewer.Public)
    {
        if (await Season(input, purpose, viewer) is int id)
            return id;
        await context.Reply(NoSuchSeason);
        return null;
    }

    /// <summary>The team named (its newest season's) or picked. Null when there is no single such team.</summary>
    public async Task<(int SeasonID, int TeamID)?> Team(string input, SeasonViewer viewer = SeasonViewer.Public, CancellationToken ct = default)
    {
        var seasons = Visible(viewer);
        var teams = _context.Teams.AsNoTracking().Where(t => seasons.Any(s => s.SeasonID == t.SeasonID));
        var name = input.Trim();
        var id = Picked(name)
            ?? await teams.Where(x => x.TeamName == name).OrderByDescending(x => x.SeasonID).Select(x => (int?)x.TeamID).FirstOrDefaultAsync(ct)
            ?? await OnlyOne(teams.Where(x => x.TeamName.Contains(name)).Select(x => x.TeamID), ct)
            ?? (int.TryParse(name, out var typed) ? (int?)typed : null);
        var team = id is null ? null : await teams.Where(x => x.TeamID == id).Select(x => new { x.SeasonID, x.TeamID }).FirstOrDefaultAsync(ct);
        return team is null ? null : (team.SeasonID, team.TeamID);
    }

    /// <summary>The player named (by gamertag) or picked. Null when there is no single such player.</summary>
    public async Task<int?> Player(string input, CancellationToken ct = default)
    {
        var users = _context.Users.AsNoTracking().Where(x => x.XboxUser != null);
        var gamertag = input.Trim();
        var id = Picked(gamertag)
            ?? await users.Where(x => x.XboxUser!.Gamertag == gamertag).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            ?? await OnlyOne(users.Where(x => x.XboxUser!.Gamertag.Contains(gamertag)).Select(x => x.Id), ct)
            ?? (int.TryParse(gamertag, out var typed) ? (int?)typed : null);
        return id is null ? null : await users.Where(x => x.Id == id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// The season match picked, or the only one of a team typed by name (exactly, else in part). Null when there is no single
    /// such match: a team plays many, and a guess could forfeit the wrong one.
    /// </summary>
    public async Task<int?> SeasonMatch(string input, SeasonViewer viewer = SeasonViewer.Public, CancellationToken ct = default)
    {
        var seasons = Visible(viewer);
        var matches = _context.SeasonMatches.AsNoTracking().Where(m => seasons.Any(s => s.SeasonID == m.SeasonID));
        var name = input.Trim();
        var id = Picked(name)
            ?? await OnlyOne(matches.Where(x => x.HomeTeam!.TeamName == name || x.AwayTeam!.TeamName == name).Select(x => x.SeasonMatchID), ct)
            ?? await OnlyOne(matches.Where(x => x.HomeTeam!.TeamName.Contains(name) || x.AwayTeam!.TeamName.Contains(name)).Select(x => x.SeasonMatchID), ct)
            ?? (int.TryParse(name, out var typed) ? (int?)typed : null);
        return id is null ? null : await matches.Where(x => x.SeasonMatchID == id).Select(x => (int?)x.SeasonMatchID).FirstOrDefaultAsync(ct);
    }

    /// <summary>The season match named or picked, or null with the caller told there is no single one (call after deferring).</summary>
    public async Task<int?> SeasonMatchFor(IDiscordInteractionContext context, string input, SeasonViewer viewer = SeasonViewer.Public)
    {
        if (await SeasonMatch(input, viewer) is int id)
            return id;
        await context.Reply(NoSuchMatch);
        return null;
    }

    /// <summary>Seasons whose name has <paramref name="input"/>, newest first: a season option's choices.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> SeasonChoices(string? input, SeasonViewer viewer = SeasonViewer.Public,
        CancellationToken ct = default)
    {
        var name = (input ?? "").Trim();
        var seasons = await Visible(viewer)
            .Where(x => x.SeasonName.Contains(name))
            .OrderByDescending(x => x.SeasonID)
            .Take(MaxChoices)
            .Select(x => new { x.SeasonID, x.SeasonName })
            .ToListAsync(ct);
        return seasons.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName(x.SeasonName), ChoiceValue(x.SeasonID))).ToList();
    }

    /// <summary>Teams whose name has <paramref name="input"/>, labelled with their season, newest season first.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> TeamChoices(string? input, SeasonViewer viewer = SeasonViewer.Public,
        CancellationToken ct = default)
    {
        var name = (input ?? "").Trim();
        var seasons = Visible(viewer);
        var teams = await _context.Teams
            .Where(x => x.TeamName.Contains(name) && seasons.Any(s => s.SeasonID == x.SeasonID))
            .OrderByDescending(x => x.SeasonID)
            .ThenBy(x => x.TeamName)
            .Take(MaxChoices)
            .Select(x => new { x.TeamID, x.TeamName, x.Season.SeasonName })
            .ToListAsync(ct);
        return teams.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName($"{x.TeamName} ({x.SeasonName})"), ChoiceValue(x.TeamID))).ToList();
    }

    /// <summary>Players (users with a gamertag) whose gamertag has <paramref name="input"/>.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> PlayerChoices(string? input, CancellationToken ct = default)
    {
        var gamertag = (input ?? "").Trim();
        var players = await _context.Users
            .Where(x => x.XboxUser != null && x.XboxUser.Gamertag.Contains(gamertag))
            .OrderBy(x => x.XboxUser!.Gamertag)
            .Take(MaxChoices)
            .Select(x => new { x.Id, x.XboxUser!.Gamertag })
            .ToListAsync(ct);
        return players.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName(x.Gamertag), ChoiceValue(x.Id))).ToList();
    }

    /// <summary>Season matches with a team whose name has <paramref name="input"/>, latest season first.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> SeasonMatchChoices(string? input, SeasonViewer viewer = SeasonViewer.Public,
        CancellationToken ct = default)
    {
        var name = (input ?? "").Trim();
        var seasons = Visible(viewer);
        var matches = await _context.SeasonMatches
            .Where(x => (x.HomeTeam!.TeamName.Contains(name) || x.AwayTeam!.TeamName.Contains(name)) && seasons.Any(s => s.SeasonID == x.SeasonID))
            .OrderByDescending(x => x.SeasonID)
            .ThenBy(x => x.ScheduledTime)
            .Take(MaxChoices)
            .Select(x => new { x.SeasonMatchID, Home = x.HomeTeam!.TeamName, Away = x.AwayTeam!.TeamName, x.Season.SeasonName })
            .ToListAsync(ct);
        return matches.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName($"{x.Home ?? "TBD"} vs {x.Away ?? "TBD"} ({x.SeasonName})"), ChoiceValue(x.SeasonMatchID))).ToList();
    }

    /// <summary>A time typed in UTC (2026-11-20 20:00, or any invariant date and time): Discord has no date option.</summary>
    public static bool TryParseUtc(string input, out DateTime utc)
        => DateTime.TryParse(input, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out utc);

    /// <summary>A choice's label, cut to Discord's 100 characters.</summary>
    public static string ChoiceName(string name) => name.Length <= 100 ? name : name[..99] + "…";
}

/// <summary>The choices for a season option: <see cref="CommandLookups.SeasonChoices"/>.</summary>
public class SeasonAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    public SeasonAutocomplete(CommandLookups lookups)
    {
        _lookups = lookups;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.SeasonChoices(option.Value);
}

/// <summary>The choices for a team option: <see cref="CommandLookups.TeamChoices"/>.</summary>
public class TeamAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    public TeamAutocomplete(CommandLookups lookups)
    {
        _lookups = lookups;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.TeamChoices(option.Value);
}

/// <summary>The choices for a player option: <see cref="CommandLookups.PlayerChoices"/>.</summary>
public class PlayerAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    public PlayerAutocomplete(CommandLookups lookups)
    {
        _lookups = lookups;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.PlayerChoices(option.Value);
}

/// <summary>The choices for a season match option: <see cref="CommandLookups.SeasonMatchChoices"/>.</summary>
public class SeasonMatchAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    public SeasonMatchAutocomplete(CommandLookups lookups)
    {
        _lookups = lookups;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.SeasonMatchChoices(option.Value);
}

/// <summary>
/// The choices for a staff command's season option (/admin): <see cref="CommandLookups.SeasonChoices"/> as the caller sees them,
/// a season not announced yet among them for a commissioner or sysadmin alone (the reply is only theirs).
/// </summary>
public class StaffSeasonAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    private readonly DiscordCaller _caller;
    public StaffSeasonAutocomplete(CommandLookups lookups, DiscordCaller caller)
    {
        _lookups = lookups;
        _caller = caller;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.SeasonChoices(option.Value, await _caller.Viewer(context.User.Id));
}

/// <summary>The choices for a staff command's season match option (/admin): <see cref="CommandLookups.SeasonMatchChoices"/> as the caller sees them.</summary>
public class StaffSeasonMatchAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    private readonly DiscordCaller _caller;
    public StaffSeasonMatchAutocomplete(CommandLookups lookups, DiscordCaller caller)
    {
        _lookups = lookups;
        _caller = caller;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.SeasonMatchChoices(option.Value, await _caller.Viewer(context.User.Id));
}
