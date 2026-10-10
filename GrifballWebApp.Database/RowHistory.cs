using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NpgsqlTypes;

namespace GrifballWebApp.Database;

/// <summary>
/// Postgres: the history SQL Server's temporal tables keep, kept by the app instead. Each table SQL Server
/// keeps a history of gets, on Postgres, a PeriodStart column (when its row's current version began) and a
/// history table, &lt;Table&gt;History in the same schema, with the table's columns and Valid, the range of
/// time the version was current. <see cref="Interceptors.RowHistoryInterceptor"/> writes a history row,
/// in the same SaveChanges, for each row it updates or deletes. A version is current or in history, never
/// both, so the tables hold only current rows and are queried as before; history is queried explicitly
/// (<see cref="RowHistoryExtensions.AsOf"/>, <see cref="RowHistoryExtensions.HistoryOf"/>).
/// </summary>
public static class RowHistory
{
    public const string PeriodStart = "PeriodStart";
    public const string Valid = "Valid";
    public const string HistoryID = "HistoryID";

    /// <summary>The history entity, and its table, of <paramref name="table"/>: as SQL Server names it.</summary>
    public static string HistoryName(string table) => table + "History";

    /// <summary>
    /// UNIQUE (key, Valid WITHOUT OVERLAPS) on a history table: no two versions of a row are current at
    /// once. EF has no such constraint (nor would it take a range in a unique index), so the model has
    /// its index, GiST on (key, Valid), by this name, and <see cref="PostgresMigrationsSqlGenerator"/>
    /// creates it as the constraint.
    /// </summary>
    public static string ConstraintName(string historyTable) => $"AK_{historyTable}_Valid";

    /// <summary>The history entity of <paramref name="entityType"/>, if it has one.</summary>
    public static IEntityType? FindHistory(this IEntityType entityType)
    {
        return entityType.GetTableName() is { } table && entityType.FindProperty(PeriodStart) is not null
            ? entityType.Model.FindEntityType(HistoryName(table))
            : null;
    }

    /// <summary>Adds PeriodStart and a history entity to each of <paramref name="entityTypes"/>.</summary>
    internal static void Configure(ModelBuilder modelBuilder, IEnumerable<IMutableEntityType> entityTypes)
    {
        // WITHOUT OVERLAPS on an integer and a range needs GiST support for the integer.
        modelBuilder.HasPostgresExtension("btree_gist");

        foreach (var entityType in entityTypes)
        {
            var table = entityType.GetTableName()!;
            var key = entityType.FindPrimaryKey()!.Properties.Select(p => p.Name).ToList();
            var properties = entityType.GetProperties().ToList();

            // The app sets it (RowHistoryInterceptor); the default is for rows inserted otherwise, and
            // for the rows already there when it's added.
            modelBuilder.Entity(entityType.Name).Property<DateTime>(PeriodStart)
                .HasDefaultValueSql("now() AT TIME ZONE 'UTC'");

            modelBuilder.SharedTypeEntity<Dictionary<string, object>>(HistoryName(table), b =>
            {
                b.ToTable(HistoryName(table), entityType.GetSchema());

                // Valid can't be part of an EF key (EF compares key values, and ranges don't compare), so
                // the key is a number of its own; the constraint is the real one.
                b.IndexerProperty<long>(HistoryID);
                b.HasKey(HistoryID);

                // Nullable but for the key: a column added later has no value in the versions before it.
                // The facets the model uses are copied; a test fails if a column's type differs from its
                // table's (RowHistoryModelTests).
                foreach (var property in properties)
                {
                    var isKey = key.Contains(property.Name);
                    var clrType = isKey || !property.ClrType.IsValueType || Nullable.GetUnderlyingType(property.ClrType) is not null
                        ? property.ClrType
                        : typeof(Nullable<>).MakeGenericType(property.ClrType);
                    var column = b.IndexerProperty(clrType, property.Name)
                        .HasColumnName(property.GetColumnName())
                        .IsRequired(isKey)
                        .ValueGeneratedNever();
                    if (property.GetMaxLength() is { } maxLength)
                        column.HasMaxLength(maxLength);
                    if (property.GetColumnType() is { } columnType)
                        column.HasColumnType(columnType);
                    if (property.GetValueConverter() is { } converter)
                        column.HasConversion(converter);
                }

                // timestamp, like every DateTime here, rather than the default timestamptz.
                b.IndexerProperty<NpgsqlRange<DateTime>>(Valid).HasColumnType("tsrange");

                b.HasIndex([.. key, Valid])
                    .HasMethod("gist")
                    .HasDatabaseName(ConstraintName(HistoryName(table)));
            });
        }
    }
}
