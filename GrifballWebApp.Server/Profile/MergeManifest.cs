using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// Everything a merge changed, batch by batch (one per SaveChanges inside <see cref="UserMergeService.Merge"/>): each
/// row moved (its changed columns, old and new), removed (all its values) or added (all its values), keyed by its
/// primary key. Built from the change tracker, so every path through the merge is covered alike. Values are JSON of
/// the property's CLR type, which the model gives back on undo.
/// </summary>
public sealed class MergeManifest
{
    public List<MergeManifestBatch> Batches { get; set; } = [];

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static MergeManifest Parse(string json)
        => string.IsNullOrWhiteSpace(json) ? new MergeManifest() : JsonSerializer.Deserialize<MergeManifest>(json, JsonOptions) ?? new MergeManifest();

    public IEnumerable<MergeManifestEntry> Entries => Batches.SelectMany(b => b.Entries);

    /// <summary>Each row the merge touched, as "table|key": what two merges are compared by.</summary>
    public HashSet<string> RowKeys() => Entries.Select(e => e.RowKey).ToHashSet(StringComparer.Ordinal);
}

public sealed class MergeManifestBatch
{
    public List<MergeManifestEntry> Entries { get; set; } = [];
}

public enum MergeManifestOp
{
    Modified = 1,
    Deleted = 2,
    Added = 3,
}

public sealed class MergeManifestEntry
{
    /// <summary>The EF entity type's name, which finds its CLR type and properties on undo.</summary>
    public string Entity { get; set; } = "";
    /// <summary>schema.table, for people reading it.</summary>
    public string Table { get; set; } = "";
    public MergeManifestOp Op { get; set; }
    public Dictionary<string, JsonElement> Key { get; set; } = [];
    /// <summary>Removed: the row as it was. Added: the row as added.</summary>
    public Dictionary<string, JsonElement>? Values { get; set; }
    /// <summary>Moved: the columns changed.</summary>
    public Dictionary<string, MergeManifestChange>? Changes { get; set; }

    [JsonIgnore]
    public string RowKey => Table + "|" + string.Join(",", Key.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value.GetRawText()));
}

public sealed class MergeManifestChange
{
    public JsonElement Old { get; set; }
    public JsonElement New { get; set; }
}

/// <summary>
/// Saves a merge's changes, recording them in a <see cref="MergeManifest"/> first: one helper for every SaveChanges of a
/// merge, nested Transfer* ones included. Rows the database alone changes (a cascade of rows not loaded) can't be seen
/// here, so the merge loads what deleting the source would cascade to before it deletes it.
/// </summary>
internal sealed class MergeManifestRecorder
{
    private readonly DbContext _context;

    public MergeManifestRecorder(DbContext context)
    {
        _context = context;
    }

    public MergeManifest Manifest { get; } = new();

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        _context.ChangeTracker.DetectChanges();
        var batch = new MergeManifestBatch();
        var added = new List<(EntityEntry Entry, MergeManifestEntry Item)>();
        foreach (var entry in _context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Modified:
                    var changes = new Dictionary<string, MergeManifestChange>();
                    foreach (var property in entry.Properties.Where(p => p.IsModified && !IsSkipped(p.Metadata)))
                    {
                        if (MergeValues.Same(property.OriginalValue, property.CurrentValue))
                            continue;
                        changes[property.Metadata.Name] = new MergeManifestChange
                        {
                            Old = MergeValues.ToJson(property.OriginalValue, property.Metadata.ClrType),
                            New = MergeValues.ToJson(property.CurrentValue, property.Metadata.ClrType),
                        };
                    }
                    if (changes.Count > 0)
                        batch.Entries.Add(NewEntry(entry, MergeManifestOp.Modified, original: true, changes: changes));
                    break;
                case EntityState.Deleted:
                    var deleted = NewEntry(entry, MergeManifestOp.Deleted, original: true);
                    deleted.Values = Values(entry, original: true);
                    batch.Entries.Add(deleted);
                    break;
                case EntityState.Added:
                    // Its key (an identity) is only known once saved
                    var item = new MergeManifestEntry { Entity = entry.Metadata.Name, Table = TableOf(entry.Metadata), Op = MergeManifestOp.Added };
                    added.Add((entry, item));
                    batch.Entries.Add(item);
                    break;
            }
        }

        await _context.SaveChangesAsync(ct);

        foreach (var (entry, item) in added)
        {
            item.Key = Key(entry, original: false);
            item.Values = Values(entry, original: false);
        }
        if (batch.Entries.Count > 0)
            Manifest.Batches.Add(batch);
    }

    private static MergeManifestEntry NewEntry(EntityEntry entry, MergeManifestOp op, bool original, Dictionary<string, MergeManifestChange>? changes = null) => new()
    {
        Entity = entry.Metadata.Name,
        Table = TableOf(entry.Metadata),
        Op = op,
        Key = Key(entry, original),
        Changes = changes,
    };

    private static Dictionary<string, JsonElement> Key(EntityEntry entry, bool original)
        => entry.Metadata.FindPrimaryKey()!.Properties.ToDictionary(p => p.Name, p => MergeValues.ToJson(Value(entry, p, original), p.ClrType));

    private static Dictionary<string, JsonElement> Values(EntityEntry entry, bool original)
        => entry.Metadata.GetProperties().Where(p => !IsSkipped(p))
            .ToDictionary(p => p.Name, p => MergeValues.ToJson(Value(entry, p, original), p.ClrType));

    private static object? Value(EntityEntry entry, IProperty property, bool original)
        => original ? entry.OriginalValues[property] : entry.CurrentValues[property];

    internal static string TableOf(IEntityType type) => (type.GetSchema() is { } schema ? schema + "." : "") + type.GetTableName();

    /// <summary>The temporal period columns: the database writes them, and nothing can set them back.</summary>
    internal static bool IsSkipped(IProperty property)
        => property.IsShadowProperty() && property.Name is "PeriodStart" or "PeriodEnd" or "ValidFrom" or "ValidTo";

    internal static bool IsAudit(IProperty property)
        => typeof(IAuditable).IsAssignableFrom(property.DeclaringType.ClrType)
           && property.Name is nameof(IAuditable.CreatedAt) or nameof(IAuditable.CreatedByID) or nameof(IAuditable.ModifiedAt) or nameof(IAuditable.ModifiedByID);
}

/// <summary>Values to and from the manifest's JSON, and compared.</summary>
internal static class MergeValues
{
    public static JsonElement ToJson(object? value, Type type) => JsonSerializer.SerializeToElement(value, type, MergeManifest.JsonOptions);

    public static object? FromJson(JsonElement json, Type type) => json.ValueKind == JsonValueKind.Undefined ? null : json.Deserialize(type, MergeManifest.JsonOptions);

    /// <summary>Equal values (byte arrays by content; decimals by value whatever their scale; dates by ticks).</summary>
    public static bool Same(object? a, object? b)
    {
        if (a is byte[] x && b is byte[] y)
            return x.AsSpan().SequenceEqual(y);
        return Equals(a, b);
    }
}

/// <summary>An undo that can't be done, and why: the message is for the sysadmin (409).</summary>
public class MergeUndoRefusedException(string message) : Exception(message);

/// <summary>
/// Plays a manifest back in reverse, in the caller's transaction: per batch, the rows the merge added go, the rows it
/// moved go back, then the rows it removed come back with their keys (principals first), and last the references to
/// those rows are restored. Refuses (throws <see cref="MergeUndoRefusedException"/>) when a row was changed since, is
/// missing, or its key is taken.
/// </summary>
internal sealed class MergeManifestPlayer
{
    private readonly GrifballWebApp.Database.GrifballContext _context;

    public MergeManifestPlayer(GrifballWebApp.Database.GrifballContext context)
    {
        _context = context;
    }

    public async Task UndoAsync(MergeManifest manifest, CancellationToken ct)
    {
        foreach (var batch in Enumerable.Reverse(manifest.Batches))
            await UndoBatch(batch, ct);
    }

    private async Task UndoBatch(MergeManifestBatch batch, CancellationToken ct)
    {
        // 1. The rows the merge added go, unless changed since
        foreach (var item in batch.Entries.Where(e => e.Op == MergeManifestOp.Added))
        {
            var (type, row) = await Find(item, ct);
            if (row is null)
                throw Refused(item, "the row it added is gone");
            foreach (var (name, json) in item.Values ?? [])
            {
                var property = type.FindProperty(name);
                if (property is null || MergeManifestRecorder.IsAudit(property) || property.IsPrimaryKey())
                    continue;
                if (!MergeValues.Same(row.Property(name).CurrentValue, MergeValues.FromJson(json, property.ClrType)))
                    throw Refused(item, $"the row it added was changed since ({name})");
            }
            row.State = EntityState.Deleted;
        }

        // 2. The rows it moved go back, except references to rows this batch removed: those come back first (step 3)
        var removed = batch.Entries.Where(e => e.Op == MergeManifestOp.Deleted).Select(e => e.RowKey).ToHashSet(StringComparer.Ordinal);
        var deferred = new List<(EntityEntry Row, string Property, object? Value)>();
        foreach (var item in batch.Entries.Where(e => e.Op == MergeManifestOp.Modified))
        {
            var (type, row) = await Find(item, ct);
            if (row is null)
                throw Refused(item, "a row it moved is gone");
            foreach (var (name, change) in item.Changes ?? [])
            {
                var property = type.FindProperty(name) ?? throw Refused(item, $"the column {name} no longer exists");
                if (!MergeValues.Same(row.Property(name).CurrentValue, MergeValues.FromJson(change.New, property.ClrType)))
                    throw Refused(item, $"a row it moved was changed since ({name})");
                var old = MergeValues.FromJson(change.Old, property.ClrType);
                if (ReferencesRemoved(property, old, removed))
                    deferred.Add((row, name, old));
                else
                    row.Property(name).CurrentValue = old;
            }
        }
        await _context.SaveChangesAsync(ct);

        // 3. The rows it removed come back, keeping their keys: principals before the rows that reference them
        var deletedItems = batch.Entries.Where(e => e.Op == MergeManifestOp.Deleted).ToList();
        foreach (var group in DependencyOrder(deletedItems))
        {
            foreach (var item in group.Items)
            {
                var (_, existing) = await Find(item, ct);
                if (existing is not null)
                    throw Refused(item, "the key of a row it removed is taken now");
                Restore(group.Type, item);
            }
            await SaveWithExplicitKeys(group.Type, ct);
        }

        // 4. References to the rows restored
        foreach (var (row, name, value) in deferred)
            row.Property(name).CurrentValue = value;
        if (deferred.Count > 0)
            await _context.SaveChangesAsync(ct);
    }

    private IEntityType TypeOf(MergeManifestEntry item)
        => _context.Model.FindEntityType(item.Entity) ?? throw Refused(item, $"the table {item.Table} no longer exists");

    private async Task<(IEntityType Type, EntityEntry? Row)> Find(MergeManifestEntry item, CancellationToken ct)
    {
        var type = TypeOf(item);
        var key = type.FindPrimaryKey()!.Properties
            .Select(p => item.Key.TryGetValue(p.Name, out var json) ? MergeValues.FromJson(json, p.ClrType) : throw Refused(item, "its key changed"))
            .ToArray();
        var entity = await _context.FindAsync(type.ClrType, key, ct);
        return (type, entity is null ? null : _context.Entry(entity));
    }

    /// <summary>Whether the value of a foreign key column is the key of a row this batch removed.</summary>
    private static bool ReferencesRemoved(IProperty property, object? value, HashSet<string> removed)
    {
        if (value is null)
            return false;
        foreach (var fk in property.GetContainingForeignKeys().Where(f => f.Properties.Count == 1))
        {
            var principalKey = fk.PrincipalKey.Properties[0];
            var rowKey = MergeManifestRecorder.TableOf(fk.PrincipalEntityType) + "|" + principalKey.Name + "=" + MergeValues.ToJson(value, principalKey.ClrType).GetRawText();
            if (removed.Contains(rowKey))
                return true;
        }
        return false;
    }

    private void Restore(IEntityType type, MergeManifestEntry item)
    {
        // Non-public too: an aggregate's children (a matched player, a vote) are made only by their root
        var entity = Activator.CreateInstance(type.ClrType, nonPublic: true)!;
        var shadow = new List<(string Name, object? Value)>();
        foreach (var (name, json) in item.Values ?? [])
        {
            var property = type.FindProperty(name);
            if (property is null || MergeManifestRecorder.IsSkipped(property))
                continue;
            var value = MergeValues.FromJson(json, property.ClrType);
            if (property.PropertyInfo is { CanWrite: true } info)
                info.SetValue(entity, value);
            else if (property.FieldInfo is { } field)
                field.SetValue(entity, value);
            else
                shadow.Add((name, value));
        }
        var entry = _context.Entry(entity);
        entry.State = EntityState.Added;
        foreach (var (name, value) in shadow)
            entry.Property(name).CurrentValue = value;
        if (entity is User)
            SettleUser(entry);
    }

    /// <summary>
    /// A user put back as a merge made before the UserAggregate migration removed it may break the constraints that
    /// migration added: then the source kept its streak when its games moved (a win streak with no wins), which
    /// CK_Auth_Users_LateLeagueRecord refuses. Settled the way that migration settles the live rows: nothing negative, one
    /// streak at a time, no streak longer than its wins or losses, and a region with who chose it, or a region source with
    /// no region dropped (CK_Auth_Users_KnownRegionSource, CK_Auth_Users_RegionHasSource). A row from a later merge passes as
    /// it is.
    /// </summary>
    private static void SettleUser(EntityEntry entry)
    {
        int NotNegative(string name) => Math.Max(0, (int)entry.Property(name).CurrentValue!);
        var wins = NotNegative(nameof(User.Wins));
        var losses = NotNegative(nameof(User.Losses));
        var winStreak = NotNegative(nameof(User.WinStreak));
        var lossStreak = NotNegative(nameof(User.LossStreak));
        var both = winStreak > 0 && lossStreak > 0;
        entry.Property(nameof(User.MMR)).CurrentValue = NotNegative(nameof(User.MMR));
        entry.Property(nameof(User.Wins)).CurrentValue = wins;
        entry.Property(nameof(User.Losses)).CurrentValue = losses;
        entry.Property(nameof(User.WinStreak)).CurrentValue = both ? 0 : Math.Min(winStreak, wins);
        entry.Property(nameof(User.LossStreak)).CurrentValue = both ? 0 : Math.Min(lossStreak, losses);

        var source = entry.Property(nameof(User.RegionSource));
        source.CurrentValue = entry.Property(nameof(User.RegionID)).CurrentValue is null ? null
            : source.CurrentValue is RegionSource.User or RegionSource.Sysadmin or RegionSource.Import ? source.CurrentValue : RegionSource.User;
    }

    /// <summary>
    /// Saves rows added with the keys they had. SQL Server refuses explicit values in an identity column unless
    /// IDENTITY_INSERT is on, for one table at a time (hence a save per table); Postgres takes them (GENERATED BY DEFAULT),
    /// and as each key came from the sequence before, the sequence is already past it; SQLite takes them too, and its
    /// AUTOINCREMENT counter only moves forward.
    /// </summary>
    private async Task SaveWithExplicitKeys(IEntityType type, CancellationToken ct)
    {
        var key = type.FindPrimaryKey()!.Properties;
        var identity = key.Count == 1 && key[0].ValueGenerated == ValueGenerated.OnAdd && (key[0].ClrType == typeof(int) || key[0].ClrType == typeof(long));
        if (!identity || !_context.Database.IsSqlServer())
        {
            await _context.SaveChangesAsync(ct);
            return;
        }
        var table = $"[{type.GetSchema() ?? "dbo"}].[{type.GetTableName()}]";
#pragma warning disable EF1002 // The table name comes from the model, not from input
        await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} ON", ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} OFF", ct);
        }
#pragma warning restore EF1002
    }

    private sealed record TypeGroup(IEntityType Type, List<MergeManifestEntry> Items);

    /// <summary>The removed rows by table, each table after the tables it has foreign keys to.</summary>
    private List<TypeGroup> DependencyOrder(List<MergeManifestEntry> items)
    {
        var groups = items.GroupBy(i => i.Entity).Select(g => new TypeGroup(TypeOf(g.First()), g.ToList())).ToList();
        var ordered = new List<TypeGroup>();
        while (groups.Count > 0)
        {
            var next = groups.FirstOrDefault(g => !g.Type.GetForeignKeys().Any(fk =>
                    groups.Any(o => o != g && fk.PrincipalEntityType.ClrType.IsAssignableFrom(o.Type.ClrType))))
                ?? groups[0];
            ordered.Add(next);
            groups.Remove(next);
        }
        return ordered;
    }

    private static MergeUndoRefusedException Refused(MergeManifestEntry item, string why)
        => new($"Can't undo: {why} ({item.Table} {string.Join(", ", item.Key.Select(k => k.Key + " " + k.Value.GetRawText()))}).");
}
