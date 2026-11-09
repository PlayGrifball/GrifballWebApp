namespace GrifballWebApp.Domain.Models;

/// <summary>
/// A matchmaking rank: the reference data the seeder writes once (<see cref="Create"/>), which the queue reads to show a
/// player's rank by their MMR. Nothing changes a rank after it's made: nothing outside it sets its fields, and EF uses the
/// private setters. Its threshold is never negative (CK_Matchmaking_Ranks_ThresholdNotNegative).
/// </summary>
[AggregateRoot]
public class Rank : AuditableEntity
{
    /// <summary>For EF. A rank begins through <see cref="Create"/>.</summary>
    private Rank() { }

    /// <summary>A rank the seeder writes: reached at <paramref name="mmrThreshold"/> MMR.</summary>
    public static Rank Create(string name, int mmrThreshold, string color, string description, byte[] icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(mmrThreshold);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(icon);
        return new()
        {
            Name = name,
            MmrThreshold = mmrThreshold,
            Color = color,
            Description = description,
            Icon = icon,
        };
    }

    // The key is internal: the database makes it, and tests may give one
    public int Id { get; internal set; }
    public string Name { get; private set; } = null!;
    public int MmrThreshold { get; private set; }
    public string Color { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public byte[] Icon { get; private set; } = null!;
}
