using GrifballWebApp.Domain.Shared;
#nullable disable

namespace GrifballWebApp.Domain.League;

/// <summary>A weekly timeslot a season's matches can be played in: part of its <see cref="Season"/>, which sets
/// them (<see cref="Season.SetAvailability"/>, <see cref="Season.CopyAvailabilityFrom"/>).</summary>
[AggregatePart(typeof(Season))]
public class SeasonAvailability : AuditableEntity
{
    /// <summary>For EF. A timeslot is the season's through <see cref="Of(Season, Shared.AvailabilityOption)"/>.</summary>
    private SeasonAvailability() { }

    /// <summary>The season's timeslot at <paramref name="option"/>, which may be new (not saved yet).</summary>
    internal static SeasonAvailability Of(Season season, AvailabilityOption option) => new()
    {
        Season = season,
        SeasonID = season.SeasonID,
        AvailabilityOption = option,
        AvailabilityOptionID = option.AvailabilityOptionID,
    };

    /// <summary>The season's timeslot at the saved option <paramref name="availabilityOptionID"/>.</summary>
    internal static SeasonAvailability Of(Season season, int availabilityOptionID) => new()
    {
        Season = season,
        SeasonID = season.SeasonID,
        AvailabilityOptionID = availabilityOptionID,
    };

    /// <summary>The same, of saved rows by their keys: for tests, which seed a season's timeslots.</summary>
    internal static SeasonAvailability Seed(int seasonID, int availabilityOptionID) => new()
    {
        SeasonID = seasonID,
        AvailabilityOptionID = availabilityOptionID,
    };

    /// <summary>The same, of entities not saved yet: for tests.</summary>
    internal static SeasonAvailability Seed(Season season, AvailabilityOption option) => Of(season, option);

    public int SeasonID { get; private set; }
    public int AvailabilityOptionID { get; private set; }
    public Season Season { get; private set; }
    public AvailabilityOption AvailabilityOption { get; private set; }

    /// <summary>The timeslot is at <paramref name="option"/>: the same entity, or the same saved option.</summary>
    internal bool IsFor(AvailabilityOption option)
        => ReferenceEquals(AvailabilityOption, option) || (option.AvailabilityOptionID != 0 && AvailabilityOptionID == option.AvailabilityOptionID);
}
