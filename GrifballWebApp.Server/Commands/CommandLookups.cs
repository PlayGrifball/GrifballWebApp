using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// The season, team and player a command names. The options autocomplete to IDs (<see cref="SeasonAutocomplete"/> and the
/// rest), but Discord sends whatever was typed when no choice was picked, so a name is looked up too.
/// String options, not integer ones: Discord lets only digits be typed into an integer option, so names couldn't be searched.
/// </summary>
public class CommandLookups
{
    /// <summary>Discord shows at most 25 choices.</summary>
    private const int MaxChoices = 25;

    private readonly GrifballContext _context;
    private readonly SeasonService _seasons;

    public CommandLookups(GrifballContext context, SeasonService seasons)
    {
        _context = context;
        _seasons = seasons;
    }

    /// <summary>The season named or picked; with none given, the one running now, else the latest. Null when there is no such season.</summary>
    public async Task<int?> Season(string? input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            var current = await _seasons.GetCurrentSeasonID(ct);
            if (current != 0)
                return current;
            return await _context.Seasons.OrderByDescending(x => x.SeasonID).Select(x => (int?)x.SeasonID).FirstOrDefaultAsync(ct);
        }

        if (int.TryParse(input, out var id))
            return await _context.Seasons.Where(x => x.SeasonID == id).Select(x => (int?)x.SeasonID).FirstOrDefaultAsync(ct);

        var name = input.Trim().ToLower();
        return await _context.Seasons
            .Where(x => x.SeasonName.ToLower().Contains(name))
            .OrderByDescending(x => x.SeasonName.ToLower() == name)
            .ThenByDescending(x => x.SeasonID)
            .Select(x => (int?)x.SeasonID)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>The team named or picked, latest season first for a name. Null when there is no such team.</summary>
    public async Task<(int SeasonID, int TeamID)?> Team(string input, CancellationToken ct = default)
    {
        var teams = _context.Teams.AsNoTracking();
        if (int.TryParse(input, out var id))
            teams = teams.Where(x => x.TeamID == id);
        else
        {
            var name = input.Trim().ToLower();
            teams = teams.Where(x => x.TeamName.ToLower().Contains(name))
                .OrderByDescending(x => x.TeamName.ToLower() == name)
                .ThenByDescending(x => x.SeasonID);
        }

        var team = await teams.Select(x => new { x.SeasonID, x.TeamID }).FirstOrDefaultAsync(ct);
        return team is null ? null : (team.SeasonID, team.TeamID);
    }

    /// <summary>The player named (by gamertag) or picked. Null when there is no such player.</summary>
    public async Task<int?> Player(string input, CancellationToken ct = default)
    {
        var users = _context.Users.AsNoTracking().Where(x => x.XboxUser != null);
        if (int.TryParse(input, out var id))
            return await users.Where(x => x.Id == id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        var gamertag = input.Trim().ToLower();
        return await users
            .Where(x => x.XboxUser!.Gamertag.ToLower().Contains(gamertag))
            .OrderByDescending(x => x.XboxUser!.Gamertag.ToLower() == gamertag)
            .ThenBy(x => x.XboxUser!.Gamertag)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Seasons whose name has <paramref name="input"/>, newest first: a season option's choices, valued by ID.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> SeasonChoices(string? input, CancellationToken ct = default)
    {
        var name = (input ?? "").Trim().ToLower();
        var seasons = await _context.Seasons
            .Where(x => x.SeasonName.ToLower().Contains(name))
            .OrderByDescending(x => x.SeasonID)
            .Take(MaxChoices)
            .Select(x => new { x.SeasonID, x.SeasonName })
            .ToListAsync(ct);
        return seasons.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName(x.SeasonName), x.SeasonID.ToString())).ToList();
    }

    /// <summary>Teams whose name has <paramref name="input"/>, labelled with their season, newest season first; valued by ID.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> TeamChoices(string? input, CancellationToken ct = default)
    {
        var name = (input ?? "").Trim().ToLower();
        var teams = await _context.Teams
            .Where(x => x.TeamName.ToLower().Contains(name))
            .OrderByDescending(x => x.SeasonID)
            .ThenBy(x => x.TeamName)
            .Take(MaxChoices)
            .Select(x => new { x.TeamID, x.TeamName, x.Season.SeasonName })
            .ToListAsync(ct);
        return teams.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName($"{x.TeamName} ({x.SeasonName})"), x.TeamID.ToString())).ToList();
    }

    /// <summary>Players (users with a gamertag) whose gamertag has <paramref name="input"/>; valued by user ID.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> PlayerChoices(string? input, CancellationToken ct = default)
    {
        var gamertag = (input ?? "").Trim().ToLower();
        var players = await _context.Users
            .Where(x => x.XboxUser != null && x.XboxUser.Gamertag.ToLower().Contains(gamertag))
            .OrderBy(x => x.XboxUser!.Gamertag)
            .Take(MaxChoices)
            .Select(x => new { x.Id, x.XboxUser!.Gamertag })
            .ToListAsync(ct);
        return players.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName(x.Gamertag), x.Id.ToString())).ToList();
    }

    /// <summary>
    /// The season match picked, or found by its teams' names (latest season first, then soonest). Null when there is no such match.
    /// </summary>
    public async Task<int?> SeasonMatch(string input, CancellationToken ct = default)
    {
        var matches = _context.SeasonMatches.AsNoTracking();
        if (int.TryParse(input, out var id))
            return await matches.Where(x => x.SeasonMatchID == id).Select(x => (int?)x.SeasonMatchID).FirstOrDefaultAsync(ct);

        var name = input.Trim().ToLower();
        return await matches
            .Where(x => x.HomeTeam!.TeamName.ToLower().Contains(name) || x.AwayTeam!.TeamName.ToLower().Contains(name))
            .OrderByDescending(x => x.SeasonID)
            .ThenBy(x => x.ScheduledTime)
            .Select(x => (int?)x.SeasonMatchID)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Season matches with a team whose name has <paramref name="input"/>, latest season first; valued by ID.</summary>
    public async Task<List<ApplicationCommandOptionChoiceProperties>> SeasonMatchChoices(string? input, CancellationToken ct = default)
    {
        var name = (input ?? "").Trim().ToLower();
        var matches = await _context.SeasonMatches
            .Where(x => x.HomeTeam!.TeamName.ToLower().Contains(name) || x.AwayTeam!.TeamName.ToLower().Contains(name))
            .OrderByDescending(x => x.SeasonID)
            .ThenBy(x => x.ScheduledTime)
            .Take(MaxChoices)
            .Select(x => new { x.SeasonMatchID, Home = x.HomeTeam!.TeamName, Away = x.AwayTeam!.TeamName, x.Season.SeasonName })
            .ToListAsync(ct);
        return matches.Select(x => new ApplicationCommandOptionChoiceProperties(ChoiceName($"{x.Home ?? "TBD"} vs {x.Away ?? "TBD"} ({x.SeasonName})"), x.SeasonMatchID.ToString())).ToList();
    }

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
