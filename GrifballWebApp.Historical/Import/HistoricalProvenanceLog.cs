using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Import;

/// <summary>
/// One import run's provenance (<see cref="HistoricalProvenance"/>): where each row it makes, reads or changes came from.
/// The import notes an entity with the record's <see cref="ArchiveOrigin"/> as it goes, and <see cref="FlushAsync"/> writes
/// them once the rows are saved (their keys known), in the same transaction: a season rolled back (a dry run, the
/// guardrail, a failure) leaves none. An entity noted before it was ever saved is <see cref="HistoricalProvenanceKind.Created"/>,
/// any other <see cref="HistoricalProvenanceKind.Read"/>; an entity's Read rows of earlier runs from a source go when this run
/// writes its own from that source.
/// </summary>
public sealed class HistoricalProvenanceLog(IApplicationDbContext context, TimeProvider timeProvider, int? jobID = null,
    IReadOnlyDictionary<string, int>? snapshotIDs = null)
{
    /// <summary>How many entity keys one statement deletes the Read rows of.</summary>
    private const int DeleteBatch = 500;

    /// <summary>An entity noted by itself (its type and key read once it is saved), or by its type and key.</summary>
    private sealed record Note(object? Entity, HistoricalProvenanceKind Kind, ArchiveOrigin Origin, string? Detail, string? EntityName = null, string? EntityKey = null);

    private readonly List<Note> _notes = [];
    private readonly Dictionary<string, int> _snapshots = snapshotIDs is null ? [] : new(snapshotIDs, StringComparer.OrdinalIgnoreCase);

    public Guid RunID { get; } = Guid.NewGuid();

    /// <summary>Notes waiting for <see cref="FlushAsync"/>.</summary>
    public int Pending => _notes.Count;

    /// <summary>
    /// <paramref name="entity"/> was made or read from <paramref name="origin"/> (nothing when the archive doesn't say where):
    /// Created when it has never been saved, otherwise Read. <paramref name="detail"/> says why, when the import decided something.
    /// </summary>
    public void Read(object entity, ArchiveOrigin? origin, string? detail = null)
    {
        if (origin is null)
            return;
        _notes.Add(new Note(entity, IsNew(entity) ? HistoricalProvenanceKind.Created : HistoricalProvenanceKind.Read, origin, detail));
    }

    /// <summary><paramref name="entity"/> was made from <paramref name="origin"/>, though it may be saved already (a placeholder
    /// is saved as it is made).</summary>
    public void Created(object entity, ArchiveOrigin? origin, string? detail = null)
    {
        if (origin is null)
            return;
        _notes.Add(new Note(entity, HistoricalProvenanceKind.Created, origin, detail));
    }

    /// <summary><paramref name="entity"/>, which the import didn't make, was changed by <paramref name="origin"/>: <paramref name="detail"/> says how.</summary>
    public void Changed(object entity, ArchiveOrigin? origin, string detail)
    {
        if (origin is null)
            return;
        _notes.Add(new Note(entity, HistoricalProvenanceKind.Changed, origin, detail));
    }

    /// <summary>A row the import doesn't track (a Waypoint match, pulled from Halo), by its entity type and key, was read from
    /// <paramref name="origin"/>.</summary>
    public void Read(string entity, string key, ArchiveOrigin? origin, string? detail = null)
    {
        if (origin is null)
            return;
        _notes.Add(new Note(null, HistoricalProvenanceKind.Read, origin, detail, entity, key));
    }

    /// <summary>Forgets the notes: their rows were rolled back.</summary>
    public void Discard() => _notes.Clear();

    /// <summary>
    /// Writes the notes of saved entities (one never saved, or removed again, has no row to point at and is dropped) and
    /// removes those entities' Read rows of earlier runs. Call after the rows are saved and before the transaction ends;
    /// it saves its own.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct)
    {
        if (_notes.Count == 0)
            return;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rows = new List<HistoricalProvenance>(_notes.Count);
        var seen = new HashSet<(string, string, HistoricalProvenanceKind, string, string?, string?, string?)>();
        foreach (var note in _notes)
        {
            if (Identify(note) is not { } identified)
                continue;
            var (entity, key) = identified;
            var origin = note.Origin;
            // The same cells noted twice for one entity (a team read from each of its players' rows is noted per row; one
            // row twice is once)
            if (!seen.Add((entity, key, note.Kind, origin.SourceId, origin.Sheet, origin.Cells, note.Detail)))
                continue;
            rows.Add(HistoricalProvenance.Record(entity, key, note.Kind, RunID, now, origin.SourceId, origin.Sheet, origin.Cells,
                origin.Value, note.Detail, jobID, _snapshots.TryGetValue(origin.SourceId, out var snapshot) ? snapshot : null));
        }
        _notes.Clear();

        // A source's earlier Read rows of what this run read from it: a run of some seasons leaves what the others' sources said
        foreach (var group in rows.GroupBy(r => (r.Entity, r.SourceID)))
            foreach (var keys in group.Select(r => r.EntityKey).Distinct().Chunk(DeleteBatch))
                await context.HistoricalProvenance
                    .Where(p => p.Entity == group.Key.Entity && p.SourceID == group.Key.SourceID && keys.Contains(p.EntityKey)
                        && p.Kind == HistoricalProvenanceKind.Read && p.RunID != RunID)
                    .ExecuteDeleteAsync(ct);
        context.HistoricalProvenance.AddRange(rows);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>The note's entity type and key: given, or read from its saved entity; null for one not saved (never, or removed again).</summary>
    private (string Entity, string Key)? Identify(Note note)
    {
        if (note.Entity is null)
            return (note.EntityName!, note.EntityKey!);
        var entry = context.Entry(note.Entity);
        if (entry.State is EntityState.Detached or EntityState.Deleted || Key(entry) is not { } key)
            return null;
        return (entry.Metadata.ClrType.Name, key);
    }

    /// <summary>Never saved: tracked as Added, or not tracked with no key yet (the database makes int keys).</summary>
    private bool IsNew(object entity)
    {
        var entry = context.Entry(entity);
        return entry.State == EntityState.Added || (entry.State == EntityState.Detached && Key(entry) is null);
    }

    /// <summary>The entity's key as text (a composite one's parts joined by commas); null while any part is unset.</summary>
    private static string? Key(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var parts = new List<string>();
        foreach (var property in entry.Metadata.FindPrimaryKey()!.Properties)
        {
            var value = entry.Property(property.Name).CurrentValue;
            if (value is null || entry.Property(property.Name).IsTemporary || Equals(value, property.ClrType.IsValueType ? Activator.CreateInstance(property.ClrType) : null))
                return null;
            parts.Add(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!);
        }
        return string.Join(",", parts);
    }
}
