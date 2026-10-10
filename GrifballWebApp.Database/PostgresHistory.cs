using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database;

/// <summary>
/// Postgres's stand-in for SQL Server's temporal tables: the same tables keep a history of each row
/// change, in the same place ("Schema"."TableHistory"). Each table gets a PeriodStart column, when its
/// row took its current values, and a history table that EF creates and alters with it: a keyless
/// property-bag entity cloned from the table's columns, plus PeriodEnd, when the row stopped having
/// them. Triggers (the AddRowHistory migration's functions) stamp PeriodStart and copy each row an
/// UPDATE, DELETE or TRUNCATE replaces into the history table; GrifballWebApp.Migrations.Postgres adds
/// the call that turns them on to the migration that creates a history table.
/// </summary>
public static class PostgresHistory
{
    public const string Suffix = "History";
    public const string PeriodStart = "PeriodStart";
    public const string PeriodEnd = "PeriodEnd";

    /// <summary>On a history table's entity type: the name of the table it keeps the history of.</summary>
    public const string HistoryOfAnnotation = "Grif:HistoryOf";

    // UTC, as SQL Server's period columns are; DateTime's column type on Postgres (GrifballContext).
    private const string Timestamp = "timestamp without time zone";

    /// <summary>
    /// Adds PeriodStart and a history table to the tables of <paramref name="entityTypes"/>. Called last
    /// in OnModelCreating, when every column is configured: the history table copies them as they are.
    /// </summary>
    public static void AddHistoryTables(this ModelBuilder modelBuilder, IEnumerable<IMutableEntityType> entityTypes)
    {
        // A table can hold more than one entity type (a hierarchy): its history takes all their columns.
        var tables = entityTypes
            .Where(e => e.GetTableName() is not null)
            .GroupBy(e => (Schema: e.GetSchema(), Table: e.GetTableName()!))
            .ToList();

        foreach (var table in tables)
        {
            var root = table.First(e => e.BaseType is null);
            Builder(modelBuilder, root).Property<DateTime>(PeriodStart)
                .HasColumnType(Timestamp)
                // For the rows a table has when it gets the column; the trigger stamps every write after.
                .HasDefaultValueSql("transaction_timestamp() AT TIME ZONE 'UTC'")
                .ValueGeneratedOnAddOrUpdate();

            var store = StoreObjectIdentifier.Table(table.Key.Table, table.Key.Schema);
            var columns = new Dictionary<string, IMutableProperty>();
            foreach (var entityType in table)
                foreach (var property in entityType.GetProperties())
                    if (property.GetColumnName(store) is { } column)
                        columns.TryAdd(column, property);

            var keyColumns = root.FindPrimaryKey()?.Properties.Select(p => p.GetColumnName(store)!).ToArray() ?? [];

            var name = table.Key.Schema is null ? table.Key.Table + Suffix : $"{table.Key.Schema}.{table.Key.Table}{Suffix}";
            modelBuilder.SharedTypeEntity<Dictionary<string, object>>(name, b =>
            {
                b.ToTable(table.Key.Table + Suffix, table.Key.Schema);
                b.HasNoKey();
                b.HasAnnotation(HistoryOfAnnotation, table.Key.Table);
                foreach (var (column, property) in columns)
                    CopyColumn(b, column, property);
                b.IndexerProperty<DateTime>(PeriodEnd).HasColumnType(Timestamp);
                // A row's versions, and the one in effect at a time.
                b.HasIndex([.. keyColumns, PeriodEnd]);
            });
        }
    }

    /// <summary>
    /// The history table's copy of a column: the same store type, and no identity, default or
    /// constraint. Nullable bar PeriodStart: a column added to a table with rows needs no default in its
    /// history, and one the history table has but the table doesn't is left empty rather than failing
    /// the trigger's insert, which would fail the app's write.
    /// </summary>
    private static void CopyColumn(EntityTypeBuilder<Dictionary<string, object>> b, string column, IMutableProperty property)
    {
        var type = property.ClrType.IsValueType && Nullable.GetUnderlyingType(property.ClrType) is null
            ? typeof(Nullable<>).MakeGenericType(property.ClrType)
            : property.ClrType;
        var copy = b.IndexerProperty(type, column).HasColumnName(column).IsRequired(column == PeriodStart);
        if (property.GetColumnType() is { } columnType)
            copy.HasColumnType(columnType);
        if (property.GetMaxLength() is { } maxLength)
            copy.HasMaxLength(maxLength);
        if (property.GetPrecision() is { } precision)
        {
            if (property.GetScale() is { } scale)
                copy.HasPrecision(precision, scale);
            else
                copy.HasPrecision(precision);
        }
        if (property.IsUnicode() is { } unicode)
            copy.IsUnicode(unicode);
        if (property.IsFixedLength() is { } fixedLength)
            copy.IsFixedLength(fixedLength);
        if (property.GetCollation() is { } collation)
            copy.UseCollation(collation);
        // Both, or an enum stored as text becomes a number in its history.
        if (property.GetValueConverter() is { } converter)
            copy.HasConversion(converter);
        else if (property.GetProviderClrType() is { } providerType)
            copy.HasConversion(providerType);
    }

    private static EntityTypeBuilder Builder(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        return entityType.HasSharedClrType
            ? modelBuilder.SharedTypeEntity(entityType.Name, entityType.ClrType)
            : modelBuilder.Entity(entityType.Name);
    }
}
