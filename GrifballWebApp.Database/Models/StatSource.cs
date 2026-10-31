namespace GrifballWebApp.Database.Models;

/// <summary>Where a <see cref="PlayerSeasonStat"/> came from.</summary>
public enum StatSource
{
    /// <summary>Season totals typed into a stats sheet, imported from the historical archive.</summary>
    Manual = 1,
    /// <summary>Derived from Halo Infinite matches pulled from Waypoint (Matches / MatchParticipants). Replaces the
    /// season's Manual totals for that player.</summary>
    Waypoint = 2,
}
