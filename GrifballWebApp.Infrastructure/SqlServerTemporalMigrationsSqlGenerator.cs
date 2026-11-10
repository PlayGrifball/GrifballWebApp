using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // SQL Server's temporal annotation names

namespace GrifballWebApp.Infrastructure;

/// <summary>
/// SQL Server's SQL for a migration, a table made temporal after it has rows leaving its period columns as a table made
/// temporal from the start leaves them: without default constraints. EF adds the period columns to such a table as
/// plain columns with a default, which fills the rows already there, and only then makes them its period. SQL Server
/// writes them itself from then on, but the default constraint stays, with a name of SQL Server's own. Nothing needs it,
/// and it stops anything dropping the column that doesn't drop it first (the database converter, TargetWriter and
/// DatabaseScriptExporter, drops the period columns of every temporal table). So each one is dropped once its column is
/// filled, in the migration that adds it, with no edit to the migration.
/// Applying migrations uses this, as the app and the bundle do (DatabaseProvider), and so does scripting them.
/// </summary>
public class SqlServerTemporalMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, ICommandBatchPreparer commandBatchPreparer)
    : SqlServerMigrationsSqlGenerator(dependencies, commandBatchPreparer)
{
    // The period columns the migration adds, found before EF rewrites the operations: it takes their period
    // annotations off to add them as plain columns first.
    private readonly HashSet<(string? Schema, string Table, string Column)> _periodColumnsAdded = [];

    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations, IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        _periodColumnsAdded.Clear();
        foreach (var column in operations.OfType<AddColumnOperation>().Where(IsPeriodColumn))
            _periodColumnsAdded.Add((column.Schema, column.Table, column.Name));
        return base.Generate(operations, model, options);
    }

    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        base.Generate(operation, model, builder, terminate);
        if (!terminate || !_periodColumnsAdded.Contains((operation.Schema, operation.Table, operation.Name)))
            return;

        // The rows are filled: the default has done its part
        DropDefaultConstraint(operation.Schema, operation.Table, operation.Name, null, builder);
        builder.EndCommand();
    }

    /// <summary>Whether <paramref name="column"/> is one of its table's period columns.</summary>
    public static bool IsPeriodColumn(AddColumnOperation column)
        => column[SqlServerAnnotationNames.TemporalIsPeriodStartColumn] as bool? == true
            || column[SqlServerAnnotationNames.TemporalIsPeriodEndColumn] as bool? == true;
}
