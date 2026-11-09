using GrifballWebApp.Domain.Shared;
using System.Linq.Expressions;
using System.Security.Cryptography;

namespace GrifballWebApp.Domain.Archive;

/// <summary>
/// A source's downloaded content, kept only when it differs from the previous snapshot (by SHA-256). The import reads
/// each source's latest snapshot, so a source that is gone or private still imports from its last one. In the database
/// rather than on a volume: it rides the normal database backups and every pod can read it. Kept in its own table so
/// queries of the sources never load the bytes. Not temporal: rows are only added (and removed with their source).
/// Part of its source: made by <see cref="HistoricalSource.Fetched"/>, read through <see cref="HistoricalSource.Snapshots"/>.
/// </summary>
[AggregatePart(typeof(HistoricalSource))]
public class HistoricalSourceSnapshot
{
    public const int MaxContentTypeLength = 100;

    /// <summary>For EF.</summary>
    private HistoricalSourceSnapshot() { }

    internal HistoricalSourceSnapshot(HistoricalSource source, DateTime fetchedAt, string sha256, byte[] content, string contentType)
    {
        Source = source;
        HistoricalSourceID = source.HistoricalSourceID;
        FetchedAt = fetchedAt;
        Sha256 = sha256;
        Size = content.LongLength;
        ContentType = HistoricalSource.Truncate(contentType, MaxContentTypeLength);
        Content = content;
    }

    public int HistoricalSourceSnapshotID { get; internal set; }
    public string HistoricalSourceID { get; private set; } = null!;
    public DateTime FetchedAt { get; private set; }
    /// <summary>Lower-case hex.</summary>
    public string Sha256 { get; private set; } = null!;
    public long Size { get; private set; }
    public string? ContentType { get; private set; }
    public byte[] Content { get; private set; } = null!;

    public virtual HistoricalSource Source { get; private set; } = null!;

    /// <summary>Its source's latest: no snapshot of the source was fetched later (or at once, with a higher ID). For queries.</summary>
    public static readonly Expression<Func<HistoricalSourceSnapshot, bool>> Latest = s => !s.Source.Snapshots.Any(o =>
        o.FetchedAt > s.FetchedAt || (o.FetchedAt == s.FetchedAt && o.HistoricalSourceSnapshotID > s.HistoricalSourceSnapshotID));

    /// <summary>A content's SHA-256, as lower-case hex: two snapshots of the same content have the same.</summary>
    public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
