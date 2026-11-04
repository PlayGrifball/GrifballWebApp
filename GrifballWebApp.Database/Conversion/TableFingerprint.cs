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
    /// <summary>
    /// Each row hashed on its own as its values stream in, then the rows' hashes sorted and hashed together: the same rows
    /// in any order give the same hash. Nothing holds more than one value's text at a time: a table of archived
    /// spreadsheets (HistoricalSourceSnapshots, 131 MB) ran a 1 GiB pod out of memory when each row was one string.
    /// </summary>
    public static TableFingerprint Of(IReadOnlyList<object?[]> rows)
    {
        var hashes = new byte[rows.Count][];
        using (var row = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                foreach (var value in rows[i])
                {
                    Append(row, value);
                    row.AppendData("\u001e"u8);
                }
                hashes[i] = row.GetHashAndReset();
            }
        }
        Array.Sort(hashes, (x, y) => x.AsSpan().SequenceCompareTo(y));
        using var table = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var hash in hashes)
            table.AppendData(hash);
        return new TableFingerprint(rows.Count, Convert.ToHexString(table.GetHashAndReset()));
    }

    /// <summary>A value as <see cref="Canonical"/> writes it, text and bytes streamed into the hash (bytes as they are, not hex).</summary>
    private static void Append(IncrementalHash hash, object? value)
    {
        switch (value)
        {
            case string s:
                AppendText(hash, $"s{s.Length}:");
                AppendText(hash, s);
                break;
            case byte[] bytes:
                AppendText(hash, $"x{bytes.Length}:");
                hash.AppendData(bytes);
                break;
            default:
                AppendText(hash, Canonical(value));
                break;
        }
    }

    /// <summary>The text as UTF-8, a chunk at a time, never splitting a surrogate pair.</summary>
    private static void AppendText(IncrementalHash hash, string text)
    {
        const int chunk = 8192;
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(chunk));
        try
        {
            for (var start = 0; start < text.Length;)
            {
                var length = Math.Min(chunk, text.Length - start);
                if (start + length < text.Length && char.IsHighSurrogate(text[start + length - 1]))
                    length--;
                var written = Encoding.UTF8.GetBytes(text.AsSpan(start, length), buffer);
                hash.AppendData(buffer, 0, written);
                start += length;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
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
