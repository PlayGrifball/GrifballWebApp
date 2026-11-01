using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Migrations.Sqlite;

/// <summary>
/// EF's own differ, plus row history's triggers (SqliteHistory), so migrations add scaffolds them:
/// <list type="bullet">
/// <item>A migration that changes a table with history, or its history table, in any way drops the table's
/// triggers first and creates them again last, from the target model, with the columns both tables then
/// have. SQLite can't alter much of a table in place, so EF rebuilds it (a new table, the rows copied, the
/// old one dropped), and its triggers go with the old one; and a trigger naming a column a migration drops
/// or renames would stop it. A migration that creates a history table creates the triggers once both
/// tables exist; one that drops it drops them first.</item>
/// <item>A table that gets history once it has rows gives them SQL Server's PeriodStart, the beginning of
/// time: SQLite adds a column only with a constant default, so its PeriodStart scaffolds as added with
/// that, then altered to the model's.</item>
/// </list>
/// Rows a migration itself writes to such a table (its data operations) get no history: the triggers are
/// off while it runs. Each trigger scaffolds as a migrationBuilder.Sql(...) call, there to read in the
/// migration like the rest of it.
/// </summary>
public class SqliteHistoryMigrationsModelDiffer(IMigrationsModelDiffer inner) : IMigrationsModelDiffer
{
    public bool HasDifferences(IRelationalModel? source, IRelationalModel? target) => inner.HasDifferences(source, target);

    public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
    {
        var operations = inner.GetDifferences(source, target).ToList();

        // A renamed table's later operations name it by its new name: its old one, in the source.
        var renamed = operations.OfType<RenameTableOperation>()
            .Where(o => o.NewName is not null)
            .ToDictionary(o => (o.NewSchema, o.NewName!), o => (o.Schema, o.Name));

        var fromSource = new HashSet<(string? Schema, string Table)>();
        var fromTarget = new HashSet<(string? Schema, string Table)>();
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case CreateTableOperation o:
                    fromTarget.Add((o.Schema, o.Name));
                    break;
                case DropTableOperation o:
                    fromSource.Add((o.Schema, o.Name));
                    break;
                case RenameTableOperation o:
                    fromSource.Add((o.Schema, o.Name));
                    fromTarget.Add((o.NewSchema, o.NewName ?? o.Name));
                    break;
                case ITableMigrationOperation o:
                    fromTarget.Add((o.Schema, o.Table));
                    fromSource.Add(renamed.GetValueOrDefault((o.Schema, o.Table), (o.Schema, o.Table)));
                    break;
            }
        }

        // First, while the tables are as the source has them.
        var drops = fromSource
            .Select(t => Versioned(source, t.Table, t.Schema))
            .OfType<ITable>()
            .DistinctBy(t => (t.Schema, t.Name))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new SqlOperation { Sql = SqliteHistory.DropTriggers(t.Name) })
            .ToList();

        // A table that gets history and already has rows: they get PeriodStart the beginning of time, then the
        // column's default goes back to the model's, for rows written after (EF rebuilds the table for that).
        // A table created with its history has no rows: its CREATE TABLE has the model's default.
        foreach (var add in operations.OfType<AddColumnOperation>().ToList())
        {
            if (add.Name != PostgresHistory.PeriodStart || add.DefaultValueSql is not { } modelDefault
                || HistoryTable(target, add.Table, add.Schema) is null)
                continue;

            add.DefaultValueSql = SqliteHistory.BeginningOfTime;
            operations.Insert(operations.IndexOf(add) + 1, new AlterColumnOperation
            {
                Name = add.Name,
                Schema = add.Schema,
                Table = add.Table,
                ClrType = add.ClrType,
                ColumnType = add.ColumnType,
                IsNullable = add.IsNullable,
                DefaultValueSql = modelDefault,
                OldColumn = new AddColumnOperation
                {
                    Name = add.Name,
                    Schema = add.Schema,
                    Table = add.Table,
                    ClrType = add.ClrType,
                    ColumnType = add.ColumnType,
                    IsNullable = add.IsNullable,
                    DefaultValueSql = SqliteHistory.BeginningOfTime,
                },
            });
        }

        // Last, once both tables are as the target has them.
        var creates = fromTarget
            .Select(t => Versioned(target, t.Table, t.Schema))
            .OfType<ITable>()
            .DistinctBy(t => (t.Schema, t.Name))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new SqlOperation { Sql = SqliteHistory.CreateTriggers(t, HistoryTable(target, t.Name, t.Schema)!) })
            .ToList();

        operations.InsertRange(0, drops);
        operations.AddRange(creates);
        return operations;
    }

    /// <summary>The table with history that <paramref name="name"/> is, or keeps the history of.</summary>
    private static ITable? Versioned(IRelationalModel? model, string name, string? schema)
    {
        if (model?.FindTable(name, schema) is not { } table)
            return null;
        if (HistoryTable(model, name, schema) is not null)
            return table;
        return SqliteHistory.HistoryOf(table) is { } of ? model.FindTable(of, schema) : null;
    }

    /// <summary><paramref name="table"/>'s history table in <paramref name="model"/>, if it has one.</summary>
    private static ITable? HistoryTable(IRelationalModel? model, string table, string? schema)
    {
        return model?.Tables.FirstOrDefault(t => t.Schema == schema && SqliteHistory.HistoryOf(t) == table);
    }
}
