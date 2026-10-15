using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GrifballWebApp.Database.Conversion;

/// <summary>
/// A table's rows, whatever their order and whichever server read them: the row count and a hash of
/// every row's values in a canonical form. The servers' drivers give a column's values back as different
/// .NET types or precisions (SQL Server's datetime2 unspecified and to 100ns, Postgres's timestamptz UTC
/// and to the microsecond; SQL Server's datetimeoffset as Postgres's timestamptz), so each value is
/// written as what both can hold: instants and durations to the microsecond, the end and beginning of
/// time (Postgres's 'infinity' and '-infinity') as themselves.
/// </summary>
internal readonly record struct TableFingerprint(int Rows, string Hash)
{
    public static TableFingerprint Of(IReadOnlyList<object?[]> rows)
    {
        var lines = rows.Select(row => string.Join("\u001e", row.Select(Canonical))).ToList();
        lines.Sort(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var line in lines)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(line));
            hash.AppendData("\n"u8);
        }
        return new TableFingerprint(rows.Count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static long Microseconds(long ticks) => ticks - ticks % TimeSpan.TicksPerMicrosecond;

    public static string Canonical(object? value) => value switch
    {
        null => "null",
        DateTime d when d == DateTime.MaxValue => "+infinity",
        DateTime d when d == DateTime.MinValue => "-infinity",
        DateTime d => $"t{Microseconds(d.Ticks)}",
        DateTimeOffset o => Canonical(o == DateTimeOffset.MaxValue ? DateTime.MaxValue : o == DateTimeOffset.MinValue ? DateTime.MinValue : o.UtcDateTime),
        TimeSpan t => $"d{Microseconds(t.Ticks)}",
        TimeOnly t => Canonical(t.ToTimeSpan()),
        string s => $"s{s.Length}:{s}",
        bool b => b ? "true" : "false",
        byte[] bytes => $"x{Convert.ToHexString(bytes)}",
        Guid g => $"g{g:D}",
        decimal m => $"n{m.ToString("0.#############################", CultureInfo.InvariantCulture)}",
        float f => $"r{f.ToString("R", CultureInfo.InvariantCulture)}",
        double f => $"r{f.ToString("R", CultureInfo.InvariantCulture)}",
        byte or short or int or long or sbyte or ushort or uint or ulong => $"n{Convert.ToString(value, CultureInfo.InvariantCulture)}",
        _ => throw new ConversionException($"The conversion doesn't know how to compare a {value.GetType()}."),
    };
}
