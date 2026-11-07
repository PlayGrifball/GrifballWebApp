using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;

#pragma warning disable EF1001 // SQL Server's temporal annotation names

namespace GrifballWebApp.Test;

/// <summary>
/// A table made temporal after it has rows keeps no default constraints on its period columns
/// (<see cref="SqlServerTemporalMigrationsSqlGenerator"/>): EF's default fills the rows, and is dropped once it has.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SqlServerTemporalMigrationsSqlGeneratorTests
{
    private static GrifballContext Offline() => new(new DbContextOptionsBuilder<GrifballContext>()
        .UseGrifballDatabase(DatabaseProvider.SqlServer, "Server=offline;Database=x").Options);

    /// <summary>A table with rows made temporal, as EF's migration does it.</summary>
    private static List<MigrationOperation> MakeTemporal()
    {
        var alter = new AlterTableOperation { Name = "Requests" };
        alter.AddAnnotation(SqlServerAnnotationNames.IsTemporal, true);
        alter.AddAnnotation(SqlServerAnnotationNames.TemporalHistoryTableName, "RequestsHistory");
        alter.AddAnnotation(SqlServerAnnotationNames.TemporalPeriodStartColumnName, "PeriodStart");
        alter.AddAnnotation(SqlServerAnnotationNames.TemporalPeriodEndColumnName, "PeriodEnd");
        List<MigrationOperation> operations = [alter];
        foreach (var (name, annotation) in new[] { ("PeriodEnd", SqlServerAnnotationNames.TemporalIsPeriodEndColumn), ("PeriodStart", SqlServerAnnotationNames.TemporalIsPeriodStartColumn) })
        {
            var column = new AddColumnOperation
            {
                Name = name, Table = "Requests", ClrType = typeof(DateTime), ColumnType = "datetime2", IsNullable = false,
                DefaultValue = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            };
            // As a generated migration has it: the column says only which period column it is, the table the rest
            column.AddAnnotation(annotation, true);
            operations.Add(column);
        }
        return operations;
    }

    [Test]
    public void APeriodColumnAddedToATable_HasItsDefaultDroppedOnceItsRowsAreFilled()
    {
        using var context = Offline();
        var generator = context.GetService<IMigrationsSqlGenerator>();

        var sql = string.Join("\n", generator.Generate(MakeTemporal(), context.Model).Select(c => c.CommandText));

        Assert.Multiple(() =>
        {
            Assert.That(generator, Is.TypeOf<SqlServerTemporalMigrationsSqlGenerator>());
            foreach (var column in new[] { "PeriodStart", "PeriodEnd" })
            {
                var added = sql.IndexOf($"ADD [{column}] datetime2", StringComparison.Ordinal);
                var dropped = sql.IndexOf($"[c].[name] = N'{column}'", added, StringComparison.Ordinal);
                Assert.That(added, Is.GreaterThanOrEqualTo(0), column);
                Assert.That(dropped, Is.GreaterThan(added), $"{column}'s default is dropped after it's added");
            }
            Assert.That(sql, Does.Contain("ADD PERIOD FOR SYSTEM_TIME"));
        });
    }

    [Test]
    public void AnyOtherColumn_KeepsItsDefault()
    {
        using var context = Offline();
        var column = new AddColumnOperation { Name = "Notes", Table = "Requests", ClrType = typeof(int), ColumnType = "int", IsNullable = false, DefaultValue = 0 };

        var sql = string.Join("\n", context.GetService<IMigrationsSqlGenerator>().Generate([column], context.Model).Select(c => c.CommandText));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("ADD [Notes] int NOT NULL DEFAULT 0"));
            Assert.That(sql, Does.Not.Contain("default_constraints"));
        });
    }

    [Test]
    public async Task NoPeriodColumn_OfTheMigratedDatabase_HasADefaultConstraint()
    {
        if (TestDatabase.Provider != DatabaseProvider.SqlServer)
            Assert.Ignore("SQL Server's temporal tables only");
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var withDefaults = await context.Database.SqlQueryRaw<string>("""
                SELECT OBJECT_NAME(c.object_id) + N'.' + c.name AS Value
                FROM sys.default_constraints dc
                JOIN sys.columns c ON c.default_object_id = dc.object_id
                JOIN sys.periods p ON p.object_id = c.object_id AND c.column_id IN (p.start_column_id, p.end_column_id)
                """).ToListAsync();

            Assert.That(withDefaults, Is.Empty);
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
