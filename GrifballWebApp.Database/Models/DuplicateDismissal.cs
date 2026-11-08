using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A "possible duplicate" pair a sysadmin dismissed on the claims page: it isn't suggested again. A log of decisions: no row
/// history and no foreign keys (either user may be merged away later).
/// </summary>
/// <remarks>
/// A pair is stored once, in a fixed order: the lower user ID first (<see cref="InOrder"/>), whichever way round it was
/// dismissed. The key is the pair, and CK_Auth_DuplicateDismissals_Order refuses a row out of order (and a user paired with
/// itself). Begins through <see cref="Dismiss"/>, and never changes after.
/// </remarks>
[AggregateRoot]
public class DuplicateDismissal
{
    /// <summary>For EF. A dismissal begins through <see cref="Dismiss"/>.</summary>
    private DuplicateDismissal() { }

    /// <summary>
    /// The pair of <paramref name="userId"/> and <paramref name="otherUserId"/>, either way round, dismissed at
    /// <paramref name="dismissedAt"/> by <paramref name="dismissedByUserId"/> (null when no one is signed in). Refuses a user
    /// paired with itself (<see cref="CheckPair"/>): ask first.
    /// </summary>
    public static DuplicateDismissal Dismiss(int userId, int otherUserId, DateTime dismissedAt, int? dismissedByUserId)
    {
        if (CheckPair(userId, otherUserId) != DuplicatePair.Ok)
            throw new ArgumentException("A user is no duplicate of itself", nameof(otherUserId));
        var (low, high) = InOrder(userId, otherUserId);
        return new()
        {
            LowUserID = low,
            HighUserID = high,
            DismissedAt = dismissedAt,
            DismissedByUserID = dismissedByUserId,
        };
    }

    // Nothing outside the dismissal sets its fields: only Dismiss, and EF (which uses the private setters)

    /// <summary>The lower of the two user IDs (always below <see cref="HighUserID"/>).</summary>
    public int LowUserID { get; private set; }
    /// <summary>The higher of the two user IDs.</summary>
    public int HighUserID { get; private set; }
    public DateTime DismissedAt { get; private set; }
    /// <summary>The sysadmin who dismissed it; null when no one was signed in.</summary>
    public int? DismissedByUserID { get; private set; }

    /// <summary>Whether the two can be a pair, or why not.</summary>
    public static DuplicatePair CheckPair(int userId, int otherUserId) => userId == otherUserId ? DuplicatePair.SameUser : DuplicatePair.Ok;

    /// <summary>The pair in the order it is stored: the lower user ID first.</summary>
    public static (int Low, int High) InOrder(int userId, int otherUserId) => (Math.Min(userId, otherUserId), Math.Max(userId, otherUserId));

    /// <summary>The dismissal of the pair <paramref name="userId"/> and <paramref name="otherUserId"/>, either way round (<see cref="IsOf"/> for a loaded one).</summary>
    public static Expression<Func<DuplicateDismissal, bool>> Of(int userId, int otherUserId)
    {
        var (low, high) = InOrder(userId, otherUserId);
        return d => d.LowUserID == low && d.HighUserID == high;
    }

    /// <summary>Whether this is the dismissal of the pair <paramref name="userId"/> and <paramref name="otherUserId"/>, either way round (<see cref="Of"/> for queries).</summary>
    public bool IsOf(int userId, int otherUserId) => (LowUserID, HighUserID) == InOrder(userId, otherUserId);
}

/// <summary>Whether two users can be a <see cref="DuplicateDismissal"/> pair, or why not.</summary>
public enum DuplicatePair
{
    Ok,
    /// <summary>A user is no duplicate of itself.</summary>
    SameUser,
}
