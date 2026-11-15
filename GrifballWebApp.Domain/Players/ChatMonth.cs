using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Players;

/// <summary>
/// A user's spend on the chat's paid models in a month (UTC): each paid reply's cost and tokens added to it, measured against the
/// monthly limit before each question. One row a user a month, with no row history (every paid reply changes it) and no foreign key
/// (a month's spend stays whatever becomes of its user). Begins through <see cref="Begin"/>; changes only through
/// <see cref="Record"/>, which moves <see cref="Version"/> on: two replies saved at once can't both add to the same total.
/// </summary>
[AggregateRoot]
public class ChatMonth
{
    /// <summary>For EF. A month begins through <see cref="Begin"/>.</summary>
    private ChatMonth() { }

    /// <summary>Nothing spent yet by <paramref name="userId"/> in the month <paramref name="day"/> is in.</summary>
    public static ChatMonth Begin(int userId, DateOnly day) => new() { UserID = userId, Month = new DateOnly(day.Year, day.Month, 1) };

    /// <summary>The month <paramref name="utc"/> is in, as <see cref="Month"/> keeps it: its first day.</summary>
    public static DateOnly MonthOf(DateTime utc) => new(utc.Year, utc.Month, 1);

    public int UserID { get; private set; }

    /// <summary>The month's first day.</summary>
    public DateOnly Month { get; private set; }

    /// <summary>What the month's replies cost, in millionths of a US dollar.</summary>
    public long CostMicros { get; private set; }

    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }

    /// <summary>How many paid replies it counts.</summary>
    public int Replies { get; private set; }

    /// <summary>Changed by every <see cref="Record"/>: the row saves only while no one else changed it since it was read.</summary>
    public int Version { get; private set; }

    /// <summary>What the month's replies cost, in US dollars.</summary>
    public decimal Spent => CostMicros / 1_000_000m;

    /// <summary>A paid reply's cost, in millionths of a US dollar, and its tokens added. Refuses a negative one.</summary>
    public void Record(long costMicros, long inputTokens, long outputTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(costMicros);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        CostMicros += costMicros;
        InputTokens += inputTokens;
        OutputTokens += outputTokens;
        Replies++;
        Version++;
    }

    /// <summary>Whether the month's spend is at <paramref name="limit"/> (US dollars) or over: no more paid replies this month.</summary>
    public bool Reached(decimal limit) => Spent >= limit;

    /// <summary>What is left of <paramref name="limit"/> (US dollars) this month: none once it is reached.</summary>
    public decimal Remaining(decimal limit) => Math.Max(0m, limit - Spent);
}
