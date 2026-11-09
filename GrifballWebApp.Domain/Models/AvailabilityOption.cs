namespace GrifballWebApp.Domain.Models;

/// <summary>
/// A weekly timeslot (a day and a time) that seasons and teams pick from: made the first time a season picks it
/// (<see cref="At"/>, AvailabilityService) and kept after, as other seasons and teams may pick it too. Nothing changes it
/// after: nothing outside it sets its fields, and EF uses the private setters. One row per day and time.
/// </summary>
[AggregateRoot]
public class AvailabilityOption : AuditableEntity
{
    /// <summary>For EF. A timeslot begins through <see cref="At"/>.</summary>
    private AvailabilityOption() { }

    /// <summary>The timeslot at <paramref name="time"/> on <paramref name="dayOfWeek"/>s: refused for a day no week has
    /// (<see cref="IsDay"/>), which JSON can carry as any number.</summary>
    public static AvailabilityOption At(DayOfWeek dayOfWeek, TimeOnly time)
    {
        if (!IsDay(dayOfWeek))
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, "Not a day of the week");
        return new() { DayOfWeek = dayOfWeek, Time = time };
    }

    /// <summary>Whether <paramref name="dayOfWeek"/> is one of the week's seven days.</summary>
    public static bool IsDay(DayOfWeek dayOfWeek) => Enum.IsDefined(dayOfWeek);

    /// <summary>A timeslot of any shape, for tests: with a key, and the teams that can play in it (seeded by their team).</summary>
    internal static AvailabilityOption Seed(int availabilityOptionID = 0, DayOfWeek dayOfWeek = DayOfWeek.Monday, TimeOnly time = default,
        IEnumerable<TeamAvailability>? teamAvailability = null)
    {
        var option = new AvailabilityOption { AvailabilityOptionID = availabilityOptionID, DayOfWeek = dayOfWeek, Time = time };
        foreach (var team in teamAvailability ?? [])
            option._teamAvailability.Add(team);
        return option;
    }

    // The key is internal: the database makes it, and tests may give one
    public int AvailabilityOptionID { get; internal set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly Time { get; private set; }
    // Read-only: a team sets its timeslots (EF uses the field)
    public IReadOnlyCollection<TeamAvailability> TeamAvailability => _teamAvailability;
    private readonly HashSet<TeamAvailability> _teamAvailability = [];
    // Read-only: a season sets its timeslots (Season.SetAvailability; EF uses the field)
    public IReadOnlyCollection<SeasonAvailability> SeasonAvailability => _seasonAvailability;
    private readonly HashSet<SeasonAvailability> _seasonAvailability = [];
}
