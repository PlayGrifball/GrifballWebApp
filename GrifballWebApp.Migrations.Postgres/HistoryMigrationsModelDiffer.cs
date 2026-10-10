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
/// dropped with its history table takes its triggers with it.
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

        return operations;
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
