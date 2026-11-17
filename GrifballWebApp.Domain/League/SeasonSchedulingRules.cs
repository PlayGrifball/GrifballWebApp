using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.League;

/// <summary>
/// A season's scheduling rules, which its matches' times are checked against (the season scheduler, a time set by hand, a
/// reschedule): how many of a team's players make one of its timeslots playable, and how many matches a team plays a night
/// and a week, and how many may run at once. The league's defaults (<see cref="Defaults"/>) until staff change them
/// (<see cref="Change"/>). Its own row a season.
/// </summary>
[AggregateRoot]
public class SeasonSchedulingRules
{
    /// <summary>The defaults: four players make a slot; one match a team a night, two a week; no cap on concurrent matches.</summary>
    public const int DefaultMinPlayersAvailable = 4, DefaultMaxMatchesPerTeamPerNight = 1, DefaultMaxMatchesPerTeamPerWeek = 2;

    /// <summary>For EF. Rules begin as the defaults (<see cref="Defaults"/>).</summary>
    private SeasonSchedulingRules() { }

    /// <summary><paramref name="seasonID"/>'s rules, the league's defaults.</summary>
    public static SeasonSchedulingRules Defaults(int seasonID) => new()
    {
        SeasonID = seasonID,
        MinPlayersAvailable = DefaultMinPlayersAvailable,
        MaxMatchesPerTeamPerNight = DefaultMaxMatchesPerTeamPerNight,
        MaxMatchesPerTeamPerWeek = DefaultMaxMatchesPerTeamPerWeek,
    };

    public int SeasonID { get; private set; }

    /// <summary>How many of a team's rostered players must be able to play a timeslot for it to be the team's (at least 1).</summary>
    public int MinPlayersAvailable { get; private set; }

    /// <summary>The most matches a team plays in one night (league time; at least 1).</summary>
    public int MaxMatchesPerTeamPerNight { get; private set; }

    /// <summary>The most matches a team plays in one week (Monday to Sunday, league time; at least a night's).</summary>
    public int MaxMatchesPerTeamPerWeek { get; private set; }

    /// <summary>The most matches at once across the season (at least 1); null: no cap.</summary>
    public int? MaxConcurrentMatches { get; private set; }

    /// <summary>Staff changed the rules. Refuses (unchanged) a value below 1, or fewer a week than a night.</summary>
    public void Change(int minPlayersAvailable, int maxMatchesPerTeamPerNight, int maxMatchesPerTeamPerWeek, int? maxConcurrentMatches)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minPlayersAvailable, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMatchesPerTeamPerNight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMatchesPerTeamPerWeek, maxMatchesPerTeamPerNight);
        if (maxConcurrentMatches is { } concurrent)
            ArgumentOutOfRangeException.ThrowIfLessThan(concurrent, 1, nameof(maxConcurrentMatches));
        MinPlayersAvailable = minPlayersAvailable;
        MaxMatchesPerTeamPerNight = maxMatchesPerTeamPerNight;
        MaxMatchesPerTeamPerWeek = maxMatchesPerTeamPerWeek;
        MaxConcurrentMatches = maxConcurrentMatches;
    }
}
