using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Archive;

/// <summary>
/// Where the historical import got a row from: the entity (its type and key), the source's cells it read (the catalog's
/// source, its tab, the cells' A1 range and their text), the snapshot it read them from and the run and job that did, with
/// why when the import decided something (a placeholder made because no account had the gamertag). An entity has as many
/// as the cells that make it up (a match: its schedule cell, its result rows, its bracket cells).
/// <para>
/// <see cref="HistoricalProvenanceKind.Created"/> rows stay: they say which cells first made the entity.
/// <see cref="HistoricalProvenanceKind.Read"/> rows are the latest run's, replaced each time a run reads the entity again from the
/// same source (a run of some seasons leaves what the others' sources said).
/// <see cref="HistoricalProvenanceKind.Changed"/> rows (a Discord ID linked, two users merged) stay. Keyed by name, never by
/// a foreign key: a row of any table, and one that is gone still says where it came from.
/// </para>
/// Not temporal: written by the import only, its Read rows replaced by every run.
/// </summary>
[AggregateRoot]
public class HistoricalProvenance
{
    public const int MaxEntityLength = 100;
    public const int MaxEntityKeyLength = 100;
    public const int MaxSourceIDLength = 100;
    public const int MaxSheetLength = 200;
    public const int MaxCellsLength = 50;
    public const int MaxValueLength = 1000;
    public const int MaxDetailLength = 500;

    /// <summary>For EF. A row is made by <see cref="Record"/>.</summary>
    private HistoricalProvenance() { }

    /// <summary>
    /// A row read from <paramref name="sourceID"/> (and its <paramref name="sheet"/> and <paramref name="cells"/>, when the
    /// import knows them) for <paramref name="entity"/> <paramref name="entityKey"/>. Text longer than its column is cut.
    /// </summary>
    public static HistoricalProvenance Record(string entity, string entityKey, HistoricalProvenanceKind kind, Guid runID, DateTime recordedAt,
        string sourceID, string? sheet = null, string? cells = null, string? value = null, string? detail = null,
        int? historicalImportJobID = null, int? historicalSourceSnapshotID = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceID);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a provenance kind");
        return new HistoricalProvenance
        {
            Entity = Cut(entity, MaxEntityLength)!,
            EntityKey = Cut(entityKey, MaxEntityKeyLength)!,
            Kind = kind,
            RunID = runID,
            RecordedAt = recordedAt,
            SourceID = Cut(sourceID, MaxSourceIDLength)!,
            Sheet = Cut(sheet, MaxSheetLength),
            Cells = Cut(cells, MaxCellsLength),
            Value = Cut(value, MaxValueLength),
            Detail = Cut(detail, MaxDetailLength),
            HistoricalImportJobID = historicalImportJobID,
            HistoricalSourceSnapshotID = historicalSourceSnapshotID,
        };
    }

    public long HistoricalProvenanceID { get; private set; }
    /// <summary>The entity's type, e.g. User, Team, SeasonMatch.</summary>
    public string Entity { get; private set; } = null!;
    /// <summary>Its key as text (an int ID, a match's GUID; a composite key's parts joined by commas).</summary>
    public string EntityKey { get; private set; } = null!;
    public HistoricalProvenanceKind Kind { get; private set; }
    /// <summary>The import run that wrote it (one import job's run, or one import of an uploaded archive).</summary>
    public Guid RunID { get; private set; }
    public DateTime RecordedAt { get; private set; }
    /// <summary>The job of the run, when one ran it. Not a foreign key: the job's retention never takes this row with it.</summary>
    public int? HistoricalImportJobID { get; private set; }
    /// <summary>The snapshot of <see cref="SourceID"/> the run read, when the run read snapshots (an uploaded archive has
    /// none). Not a foreign key, as the job.</summary>
    public int? HistoricalSourceSnapshotID { get; private set; }
    /// <summary>The catalog's source ID (HistoricalSource.HistoricalSourceID), e.g. 2023-da-wl23-stats.</summary>
    public string SourceID { get; private set; } = null!;
    /// <summary>The tab, when the import knows it.</summary>
    public string? Sheet { get; private set; }
    /// <summary>The cells' A1 reference or range ("D14", "A20:K20"), when the import knows them.</summary>
    public string? Cells { get; private set; }
    /// <summary>The cells' text as the sheet had it (a row's cells joined by " | ").</summary>
    public string? Value { get; private set; }
    /// <summary>Why the import did what it did, when it decided something.</summary>
    public string? Detail { get; private set; }

    private static string? Cut(string? text, int length)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > length ? trimmed[..length] : trimmed;
    }
}

public enum HistoricalProvenanceKind
{
    /// <summary>The cells that made the entity: kept.</summary>
    Created = 1,
    /// <summary>The cells the latest run read the entity from: replaced by the next run that reads it.</summary>
    Read = 2,
    /// <summary>The cells that changed an entity the import didn't make (a Discord ID linked, a region set, a merge): kept.</summary>
    Changed = 3,
}
