using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Database;

/// <summary>
/// Row history on PostgreSQL (readme.md, Database), as the model knows it. The tables SQL Server keeps
/// history of - those configured temporal (IsTemporal), every one but MatchReschedules - have SQL Server's
/// period columns, PeriodStart and PeriodEnd, mapped as SQL Server maps a temporal table's: shadow DateTime
/// properties the database fills, never written by the app (EF leaves them out of every INSERT and UPDATE
/// and reads them back). The RowHistory migration (EF's AddColumns) adds them, and
/// RowHistoryMigrationsSqlGenerator as periods would (rows already there: since -infinity; PeriodEnd
/// 'infinity', which Npgsql reads as DateTime.MaxValue, SQL Server's end of time); with the periods
/// extension, add_system_time_period takes them over, and its triggers stamp them on every write. Without
/// the extension they stay as inserted. Having the two columns is what makes a table versioned when the
/// database has the extension (RowHistoryMigrationsSqlGenerator, as a migration creates a table with them or
/// adds them to one; the Helm chart's history step), so the same tables as SQL Server's: the history
/// tables, views and functions are periods', outside the model.
/// </summary>
public static class RowHistory
{
    public const string PeriodStart = "PeriodStart";
    public const string PeriodEnd = "PeriodEnd";

    // periods' period columns: an instant, as SQL Server's are read back in UTC.
    private const string Timestamp = "timestamp with time zone";

    /// <summary>The defaults periods gives the columns: a new row's start is its transaction's.</summary>
    public const string StartDefault = "transaction_timestamp()";

    public const string EndDefault = "'infinity'::timestamp with time zone";

    /// <summary>
    /// PeriodStart and PeriodEnd on the tables of <paramref name="temporal"/>, the entity types configured
    /// temporal for SQL Server (on a hierarchy's root). Called last in OnModelCreating, on Postgres only:
    /// SQL Server's temporal tables have their own.
    /// </summary>
    public static void AddSystemTimePeriods(this ModelBuilder modelBuilder, IEnumerable<IMutableEntityType> temporal)
    {
        var roots = temporal.Select(e => e.GetRootType()).Where(e => e.GetTableName() is not null).Distinct().ToList();
        foreach (var root in roots)
        {
            var entity = modelBuilder.Entity(root.Name);
            entity.Property<DateTime>(PeriodStart)
                .HasColumnType(Timestamp)
                .HasDefaultValueSql(StartDefault)
                .ValueGeneratedOnAddOrUpdate();
            entity.Property<DateTime>(PeriodEnd)
                .HasColumnType(Timestamp)
                .HasDefaultValueSql(EndDefault)
                .ValueGeneratedOnAddOrUpdate();
        }
    }

    /// <summary>
    /// Whether <paramref name="entityType"/>'s table is versioned when the database has the extension: it
    /// has the period, as the tables temporal on SQL Server have (<see cref="AddSystemTimePeriods"/>).
    /// </summary>
    public static bool HasSystemTimePeriod(this IReadOnlyEntityType entityType)
    {
        var root = entityType.GetRootType();
        return entityType.GetTableName() is not null && IsPeriodColumn(root.FindProperty(PeriodStart)) && IsPeriodColumn(root.FindProperty(PeriodEnd));
    }

    private static bool IsPeriodColumn(IReadOnlyProperty? property) =>
        property is not null && property.IsShadowProperty() && property.ValueGenerated == ValueGenerated.OnAddOrUpdate;

    /// <summary>
    /// The name periods gives an object of <paramref name="table"/>'s (add_system_versioning, through
    /// periods._choose_name): "Seasons_history", "Seasons_with_history". Too long for a PostgreSQL name
    /// (63 bytes, less 2), the table's part is cut a character at a time until it fits.
    /// </summary>
    public static string PeriodsName(string table, string suffix)
    {
        const int maxBytes = 64 - 2 - 1;
        var name = table;
        while (Encoding.UTF8.GetByteCount($"{name}_{suffix}") > maxBytes)
            name = name[..^1];
        return $"{name}_{suffix}";
    }
}
