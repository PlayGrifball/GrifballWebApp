#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A source's downloaded content, kept only when it differs from the previous snapshot (by SHA-256). The import reads
/// each source's latest snapshot, so a source that is gone or private still imports from its last one. In the database
/// rather than on a volume: it rides the normal database backups and every pod can read it. Kept in its own table so
/// queries of the sources never load the bytes. Not temporal: rows are only added (and removed with their source).
/// </summary>
public class HistoricalSourceSnapshot
{
    public int HistoricalSourceSnapshotID { get; set; }
    public string HistoricalSourceID { get; set; }
    public DateTime FetchedAt { get; set; }
    /// <summary>Lower-case hex.</summary>
    public string Sha256 { get; set; }
    public long Size { get; set; }
    public string ContentType { get; set; }
    public byte[] Content { get; set; }

    public virtual HistoricalSource Source { get; set; }
}
