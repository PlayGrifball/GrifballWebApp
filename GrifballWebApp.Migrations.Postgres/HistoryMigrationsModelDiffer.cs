using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Migrations.Postgres;

/// <summary>
/// EF's own differ, plus row history's triggers (PostgresHistory): a migration that creates a history
/// table turns its table's versioning on once both exist, and one that drops a history table but keeps
/// its table turns it off first, before the triggers can write to a table that's gone. Each scaffolds
/// as a migrationBuilder.Sql(...) call, there to read in the migration like the rest of it. A table
/// dropped with its history table takes its triggers with it. A table that gets history once it has
/// rows gives them SQL Server's PeriodStart, the beginning of time: its PeriodStart column scaffolds as
/// added with that default, then altered to the model's.
/// </summary>
public class HistoryMigrationsModelDiffer(IMigrationsModelDiffer inner) : IMigrationsModelDiffer
{
    public bool HasDifferences(IRelationalModel? source, IRelationalModel? target) => inner.HasDifferences(source, target);

    public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
    {
        var operations = inner.GetDifferences(source, target).ToList();

        foreach (var drop in operations.OfType<DropTableOperation>().ToList())
        {
            if (HistoryOf(source, drop.Name, drop.Schema) is { } table && target?.FindTable(table, drop.Schema) is not null)
                operations.Insert(0, Call("grif_disable_versioning", drop.Schema, table));
        }

        foreach (var create in operations.OfType<CreateTableOperation>().ToList())
        {
            if (HistoryOf(target, create.Name, create.Schema) is { } table)
                operations.Add(Call("grif_enable_versioning", create.Schema, table));
        }

        // A table that gets history and already has rows: they get PeriodStart -infinity, which Npgsql reads
        // as DateTime.MinValue, as SQL Server gives them 0001-01-01. Then the column default goes back to
        // the model's, for rows written after. Both are changes to the catalog only: no row is rewritten.
        // A table created with its history has no rows: its CREATE TABLE has the model's default.
        foreach (var add in operations.OfType<AddColumnOperation>().ToList())
        {
            if (add.Name != PostgresHistory.PeriodStart || add.DefaultValueSql is not { } modelDefault
                || !HasHistory(target, add.Table, add.Schema))
                continue;

            add.DefaultValueSql = PostgresHistory.BeginningOfTime;
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
                    DefaultValueSql = PostgresHistory.BeginningOfTime,
                },
            });
        }

        return operations;
    }

    /// <summary>Whether <paramref name="table"/> has a history table in <paramref name="model"/>.</summary>
    private static bool HasHistory(IRelationalModel? model, string table, string? schema)
    {
        return model?.Tables.Any(t => t.Schema == schema && HistoryOf(model, t.Name, t.Schema) == table) ?? false;
    }

    /// <summary>The table <paramref name="name"/> keeps the history of, if it's a history table.</summary>
    private static string? HistoryOf(IRelationalModel? model, string name, string? schema)
    {
        return model?.FindTable(name, schema)?.EntityTypeMappings
            .Select(m => m.TypeBase.FindAnnotation(PostgresHistory.HistoryOfAnnotation)?.Value as string)
            .FirstOrDefault(table => table is not null);
    }

    // No schema: wherever the search path puts it, as with the table.
    private static SqlOperation Call(string procedure, string? schema, string table)
    {
        var name = schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
        return new SqlOperation { Sql = $"CALL public.{procedure}('{name}');" };
    }

    // An identifier in double quotes, inside a string literal in single quotes.
    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"").Replace("'", "''") + "\"";
}
