using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Database;

/// <summary>
/// SQLite's SQL for a migration, with row history's triggers (SqliteHistory) created last. EF rebuilds a table
/// it can't alter in place (a new table, the rows copied, the old one dropped and the new one renamed), and
/// writes every rebuild at the end of the migration, after the operations it was given, the migration's
/// CREATE TRIGGERs (SqliteHistoryMigrationsModelDiffer) among them: the rebuild would drop the new triggers
/// with the old table. So they're moved after it, in their order. Applying migrations uses this, as the app
/// and the bundle do (DatabaseProvider), and so does scripting them.
/// </summary>
public class SqliteHistoryMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IRelationalAnnotationProvider migrationsAnnotations)
    : SqliteMigrationsSqlGenerator(dependencies, migrationsAnnotations)
{
    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var commands = base.Generate(operations, model, options);
        return [.. commands.Where(c => !IsHistoryTrigger(c)), .. commands.Where(IsHistoryTrigger)];
    }

    private static bool IsHistoryTrigger(MigrationCommand command)
        => command.CommandText.TrimStart().StartsWith("CREATE TRIGGER ", StringComparison.Ordinal)
            && command.CommandText.Contains($"{SqliteHistory.UpdateTriggerSuffix}\" AFTER UPDATE ON ", StringComparison.Ordinal);
}
