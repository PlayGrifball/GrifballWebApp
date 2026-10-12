using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database;

/// <summary>
/// Postgres's stand-in for SQL Server's temporal tables: the same tables keep a history of each row
/// change, in the same place ("Schema"."TableHistory"). Each table gets SQL Server's period columns:
/// PeriodStart, when its row took its current values, and PeriodEnd, always the end of time (a
/// generated 'infinity', which Npgsql reads as DateTime.MaxValue, SQL Server's 9999-12-31
/// 23:59:59.9999999). Its history table, which EF creates and alters with it, is a keyless
/// property-bag entity cloned from the table's columns, with PeriodEnd the time a row stopped having
/// them. Triggers (PostgresHistoryFunctions) stamp PeriodStart and copy each row an UPDATE, DELETE or
/// TRUNCATE replaces into the history table. GrifballWebApp.Migrations.Postgres scaffolds the functions,
/// and the call that turns them on for a table, into the migrations. EF's temporal queries
/// (TemporalAsOf and the rest) read both tables: PostgresTemporalQueries.
/// </summary>
public static class PostgresHistory
{
    public const string Suffix = "History";
    public const string PeriodStart = "PeriodStart";
    public const string PeriodEnd = "PeriodEnd";

    /// <summary>On a history table's entity type: the name of the table it keeps the history of.</summary>
    public const string HistoryOfAnnotation = "Grif:HistoryOf";

    // An instant, DateTime's column type on Postgres (GrifballContext): read back UTC, as SQL Server's
    // period columns are.
    private const string Timestamp = "timestamp with time zone";

    /// <summary>
    /// A current row's PeriodEnd. Npgsql reads 'infinity' as DateTime.MaxValue and writes
    /// DateTime.MaxValue as it, so it is SQL Server's end of time exactly, in values and comparisons.
    /// </summary>
    public const string EndOfTime = "'infinity'::timestamp with time zone";

    /// <summary>
    /// PeriodStart of the rows a table already has when it gets history. Npgsql reads '-infinity' as
    /// DateTime.MinValue, SQL Server's 0001-01-01, which EF gives a table's rows when it makes the table
    /// temporal: as there, they're current as far back as a temporal query asks.
    /// </summary>
    public const string BeginningOfTime = "'-infinity'::timestamp with time zone";

    /// <summary>PeriodStart's default: when the transaction that writes the row began.</summary>
    public const string PeriodStartDefault = "transaction_timestamp()";

    /// <summary>
    /// Adds PeriodStart, PeriodEnd and a history table to the tables of <paramref name="entityTypes"/>. Called last
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
                // The trigger stamps every write. A table that has rows when it gets the column gives them
                // BeginningOfTime instead (HistoryMigrationsModelDiffer).
                .HasDefaultValueSql(PeriodStartDefault)
                .ValueGeneratedOnAddOrUpdate();
            // Generated: the app can't write it, and EF reads it back after each insert and update.
            Builder(modelBuilder, root).Property<DateTime>(PeriodEnd)
                .HasColumnType(Timestamp)
                .HasComputedColumnSql(EndOfTime, stored: true)
                .ValueGeneratedOnAddOrUpdate();

            var store = StoreObjectIdentifier.Table(table.Key.Table, table.Key.Schema);
            var columns = new Dictionary<string, IMutableProperty>();
            foreach (var entityType in table)
                foreach (var property in entityType.GetProperties())
                    // PeriodEnd: the history table's own, below.
                    if (property.GetColumnName(store) is { } column && column != PeriodEnd)
                        columns.TryAdd(column, property);

            var keyColumns = root.FindPrimaryKey()?.Properties.Select(p => p.GetColumnName(store)!).ToArray() ?? [];

            modelBuilder.SharedTypeEntity<Dictionary<string, object>>(HistoryEntityName(root), b =>
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

        // The triggers' functions, which the migrations create with the first history table.
        if (tables.Count > 0)
            modelBuilder.HasAnnotation(PostgresHistoryFunctions.Annotation, PostgresHistoryFunctions.Current);
    }

    /// <summary>
    /// The name of the entity type of the history table of <paramref name="root"/>'s table: from the
    /// entity's name, not its table's. EF's differ pairs a table's old and new versions by the entity
    /// types they hold, so when a table is renamed or moves schema, its history table is renamed or
    /// moved with it (RenameTable, RenameIndex), keeping its rows and triggers, rather than dropped and
    /// created again.
    /// </summary>
    public static string HistoryEntityName(IReadOnlyEntityType root) => $"{root.Name}.{Suffix}";

    /// <summary>
    /// The entity type of the history table of <paramref name="entityType"/>'s table, if it has one. The
    /// temporal queries go by this, as SQL Server's IsTemporal annotations are taken off on Postgres.
    /// </summary>
    public static IReadOnlyEntityType? FindHistoryEntityType(this IReadOnlyEntityType entityType)
    {
        if (entityType.GetTableName() is not { } table)
            return null;
        var schema = entityType.GetSchema();
        return entityType.Model.GetEntityTypes().FirstOrDefault(e =>
            e.FindAnnotation(HistoryOfAnnotation)?.Value as string == table && e.GetSchema() == schema);
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
