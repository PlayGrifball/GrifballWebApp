using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Database;

/// <summary>
/// SQLite's row history: the same history tables as Postgres's (PostgresHistory.AddHistoryTables, the
/// same "Table"History beside each table, the same PeriodStart and PeriodEnd), written by two triggers on
/// each table instead of Postgres's functions. AFTER UPDATE copies the row as it was into the history
/// table, ended now, and stamps the row's PeriodStart now; AFTER DELETE copies it the same way. SQLite
/// has no schemas, so a table's name alone names it (no two of the model's tables share one) and its
/// triggers. Its times are text, as EF writes DateTimes there ("yyyy-MM-dd HH:mm:ss.FFFFFFF"), to the
/// millisecond: SQLite's clock. GrifballWebApp.Migrations.Sqlite writes the triggers into the migrations
/// (SqliteHistoryMigrationsModelDiffer). Nothing in the app queries history (EF's temporal queries are
/// translated on SQL Server, and on Postgres by PostgresTemporalQueries, not on SQLite): it's there to
/// read in a database tool, and the conversion (Conversion) carries it to and from the other servers.
/// </summary>
public static class SqliteHistory
{
    /// <summary>
    /// Now, as EF writes a DateTime: strftime's milliseconds without their trailing zeros, and without
    /// the point when they're all zeros, so it sorts and compares with the app's own times as text.
    /// </summary>
    public const string Now = "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')";

    /// <summary>A current row's PeriodEnd: DateTime.MaxValue, SQL Server's end of time, as EF writes it.</summary>
    public const string EndOfTime = "'9999-12-31 23:59:59.9999999'";

    /// <summary>
    /// PeriodStart of the rows a table already has when it gets history: DateTime.MinValue, as SQL Server
    /// gives them. SQLite adds a column only with a constant default, so the column is added with this
    /// and then altered to the model's (SqliteHistoryMigrationsModelDiffer).
    /// </summary>
    public const string BeginningOfTime = "'0001-01-01 00:00:00'";

    /// <summary>
    /// SQLite's period columns: its own DateTime type (TEXT), PeriodStart now, and PeriodEnd a virtual
    /// generated column, which SQLite can add to a table that has rows (a stored one it can't).
    /// </summary>
    public static readonly HistoryColumns Columns = new(null, Now, EndOfTime, EndStored: false, Functions: false);

    public static string UpdateTrigger(string table) => $"{table}_History_Update";

    public static string DeleteTrigger(string table) => $"{table}_History_Delete";

    /// <summary>
    /// Adds the history tables, and declares each table's triggers in the model: EF then reads a write's
    /// generated values back with a SELECT after it rather than RETURNING, whose values SQLite takes before
    /// AFTER triggers run, so PeriodStart reads back as the update trigger stamped it.
    /// </summary>
    public static void AddSqliteHistoryTables(this ModelBuilder modelBuilder, IEnumerable<IMutableEntityType> entityTypes)
    {
        foreach (var root in modelBuilder.AddHistoryTables(entityTypes, Columns))
        {
            var table = root.GetTableName()!;
            root.AddTrigger(UpdateTrigger(table));
            root.AddTrigger(DeleteTrigger(table));
        }
    }

    /// <summary>The table <paramref name="history"/> keeps the history of, if it's a history table.</summary>
    public static string? HistoryOf(ITable history)
    {
        return history.EntityTypeMappings
            .Select(m => m.TypeBase.FindAnnotation(PostgresHistory.HistoryOfAnnotation)?.Value as string)
            .FirstOrDefault(table => table is not null);
    }

    /// <summary>
    /// The triggers that keep <paramref name="table"/>'s history in <paramref name="history"/>. The copy takes
    /// the columns both have, bar PeriodEnd, which the history table's row gets as the time it ended. The
    /// update trigger's own UPDATE doesn't run the trigger again: SQLite runs triggers from a trigger only
    /// with recursive_triggers on, off by default.
    /// </summary>
    public static string CreateTriggers(ITable table, ITable history)
    {
        var columns = history.Columns
            .Select(c => c.Name)
            .Where(c => c != PostgresHistory.PeriodEnd && table.FindColumn(c) is not null)
            .ToList();
        var into = string.Join(", ", columns.Select(Quote).Append(Quote(PostgresHistory.PeriodEnd)));
        var values = string.Join(", ", columns.Select(c => "OLD." + Quote(c)).Append(Now));
        var insert = $"INSERT INTO {Quote(history.Name)} ({into}) VALUES ({values});";

        var sql = new StringBuilder();
        sql.AppendLine($"CREATE TRIGGER {Quote(UpdateTrigger(table.Name))} AFTER UPDATE ON {Quote(table.Name)} FOR EACH ROW BEGIN");
        sql.AppendLine($"    {insert}");
        sql.AppendLine($"    UPDATE {Quote(table.Name)} SET {Quote(PostgresHistory.PeriodStart)} = {Now} WHERE rowid = NEW.rowid;");
        sql.AppendLine("END;");
        sql.AppendLine($"CREATE TRIGGER {Quote(DeleteTrigger(table.Name))} AFTER DELETE ON {Quote(table.Name)} FOR EACH ROW BEGIN");
        sql.AppendLine($"    {insert}");
        sql.Append("END;");
        return sql.ToString();
    }

    public static string DropTriggers(string table)
    {
        return $"DROP TRIGGER IF EXISTS {Quote(UpdateTrigger(table))};\nDROP TRIGGER IF EXISTS {Quote(DeleteTrigger(table))};";
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
