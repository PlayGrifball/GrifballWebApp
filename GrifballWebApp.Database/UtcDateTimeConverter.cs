using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrifballWebApp.Database;

/// <summary>
/// Every DateTime the app stores is UTC, on both databases: read back with Kind Utc. A value with Kind
/// Unspecified is taken to be UTC already (as it is from the app's own UTC sources). Kind Local is refused:
/// which zone it came from can't be known, so it's a bug to be found rather than a value to guess at.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => ToUtc(v),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
{
    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new InvalidOperationException(
                $"A DateTime with Kind Local ({value:O}) can't be stored: the database holds UTC. Use DateTime.UtcNow or convert it first."),
        };
    }
}
