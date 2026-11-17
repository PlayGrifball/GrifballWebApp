using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Scheduling;

/// <summary>A season's scheduling rules (<see cref="SeasonSchedulingRules"/>), as staff see and change them.</summary>
public sealed class SeasonSchedulingRulesDto
{
    public required int MinPlayersAvailable { get; init; }
    public required int MaxMatchesPerTeamPerNight { get; init; }
    public required int MaxMatchesPerTeamPerWeek { get; init; }
    /// <summary>Null: no cap.</summary>
    public int? MaxConcurrentMatches { get; init; }
}

/// <summary>A season's scheduling rules: the league's defaults until staff change them.</summary>
public sealed class SeasonSchedulingRulesService(IApplicationDbContext context)
{
    public const string NoSuchSeason = "There's no such season.";
    public const string NoPlayers = "At least one player must be able to play a timeslot for it to be a team's.";
    public const string NoNight = "A team plays at least one match a night.";
    public const string WeekUnderNight = "A team plays at least as many matches a week as a night.";
    public const string NoConcurrency = "At least one match may run at a time (or leave it empty for no cap).";

    /// <summary><paramref name="seasonID"/>'s rules: its own, else the defaults (not saved).</summary>
    public async Task<SeasonSchedulingRules> ForAsync(int seasonID, CancellationToken ct = default)
        => await context.SeasonSchedulingRules.AsNoTracking().SingleOrDefaultAsync(r => r.SeasonID == seasonID, ct)
            ?? SeasonSchedulingRules.Defaults(seasonID);

    public async Task<SeasonSchedulingRulesDto> GetAsync(int seasonID, CancellationToken ct = default)
    {
        var rules = await ForAsync(seasonID, ct);
        return new SeasonSchedulingRulesDto
        {
            MinPlayersAvailable = rules.MinPlayersAvailable,
            MaxMatchesPerTeamPerNight = rules.MaxMatchesPerTeamPerNight,
            MaxMatchesPerTeamPerWeek = rules.MaxMatchesPerTeamPerWeek,
            MaxConcurrentMatches = rules.MaxConcurrentMatches,
        };
    }

    /// <summary>Staff changed <paramref name="seasonID"/>'s rules; why not (nothing changed), or null when they did.</summary>
    public async Task<string?> ChangeAsync(int seasonID, SeasonSchedulingRulesDto changed, CancellationToken ct = default)
    {
        if (!await context.Seasons.AnyAsync(s => s.SeasonID == seasonID, ct))
            return NoSuchSeason;
        var rules = await context.SeasonSchedulingRules.SingleOrDefaultAsync(r => r.SeasonID == seasonID, ct);
        var added = rules is null;
        rules ??= SeasonSchedulingRules.Defaults(seasonID);
        try
        {
            rules.Change(changed.MinPlayersAvailable, changed.MaxMatchesPerTeamPerNight, changed.MaxMatchesPerTeamPerWeek, changed.MaxConcurrentMatches);
        }
        catch (ArgumentOutOfRangeException e)
        {
            return e.ParamName switch
            {
                "minPlayersAvailable" => NoPlayers,
                "maxMatchesPerTeamPerNight" => NoNight,
                "maxMatchesPerTeamPerWeek" => WeekUnderNight,
                _ => NoConcurrency,
            };
        }
        if (added)
            context.SeasonSchedulingRules.Add(rules);
        await context.SaveChangesAsync(ct);
        return null;
    }
}
