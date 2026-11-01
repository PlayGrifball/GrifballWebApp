using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Database;

/// <summary>
/// SQLite's SQL for a migration, with row history's triggers (SqliteHistory) created last. EF rebuilds a table
/// it can't alter in place (a new table, the rows copied, the old one dropped and the new one renamed), and
/// writes every rebuild at the end of the migration, after the operations it was given, the migration's
/// CREATE TRIGGERs (SqliteHistoryMigrationsModelDiffer) among them: the rebuild would drop the new triggers
/// with the old table. So they're moved after it, in their order. And a foreign key dropped from a table the
/// migration drops is left to go with the table: SQLite can't drop a foreign key on its own (EF refuses the
/// operation), and a migration's Down drops the keys between tables before the tables (the baseline's does).
/// With the keys left, dropping a table its keys refer to would fail the next drop, so a migration that drops
/// tables runs with foreign keys off, as EF's own rebuilds do (outside the transaction: SQLite ignores the
/// setting inside one).
/// Applying migrations uses this, as the app and the bundle do (DatabaseProvider), and so does scripting them.
/// </summary>
public class SqliteHistoryMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IRelationalAnnotationProvider migrationsAnnotations)
    : SqliteMigrationsSqlGenerator(dependencies, migrationsAnnotations)
{
    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var dropped = operations.OfType<DropTableOperation>().Select(o => (o.Schema, o.Name)).ToHashSet();
        operations = [.. operations.Where(o => o is not DropForeignKeyOperation fk || !dropped.Contains((fk.Schema, fk.Table)))];
        var commands = base.Generate(operations, model, options);
        IReadOnlyList<MigrationCommand> ordered = [.. commands.Where(c => !IsHistoryTrigger(c)), .. commands.Where(IsHistoryTrigger)];
        if (dropped.Count == 0)
            return ordered;
        return [.. Pragma("PRAGMA foreign_keys = 0;"), .. ordered, .. Pragma("PRAGMA foreign_keys = 1;")];
    }

    private IReadOnlyList<MigrationCommand> Pragma(string sql)
    {
        var builder = new MigrationCommandListBuilder(Dependencies);
        builder.Append(sql).EndCommand(suppressTransaction: true);
        return builder.GetCommandList();
    }

    private static bool IsHistoryTrigger(MigrationCommand command)
        => command.CommandText.TrimStart().StartsWith("CREATE TRIGGER ", StringComparison.Ordinal)
            && command.CommandText.Contains($"{SqliteHistory.UpdateTriggerSuffix}\" AFTER UPDATE ON ", StringComparison.Ordinal);
}
