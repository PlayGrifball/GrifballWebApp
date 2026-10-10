using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Test;

/// <summary>Row history's model and migrations on Postgres (RowHistory), and its absence on SQL Server. No database.</summary>
[TestFixture]
public class RowHistoryModelTests
{
    private static GrifballContext Context(DatabaseProvider provider)
    {
        var connectionString = provider == DatabaseProvider.Postgres ? "Host=example;Database=Grif" : "Server=example;Database=Grif";
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(provider, connectionString).Options);
    }

    [Test]
    public void Postgres_KeepsHistoryOfTheTablesSqlServerDoes()
    {
        using var sqlServer = Context(DatabaseProvider.SqlServer);
        using var postgres = Context(DatabaseProvider.Postgres);

        var temporal = sqlServer.Model.GetEntityTypes().Where(e => e.IsTemporal()).Select(e => e.Name).Order().ToList();
        var withHistory = postgres.Model.GetEntityTypes().Where(e => e.FindHistory() is not null).Select(e => e.Name).Order().ToList();

        Assert.That(temporal, Has.Count.EqualTo(40));
        Assert.That(withHistory, Is.EqualTo(temporal));
    }

    [Test]
    public void SqlServer_HasNoAppKeptHistory()
    {
        using var context = Context(DatabaseProvider.SqlServer);

        Assert.Multiple(() =>
        {
            Assert.That(context.Model.GetEntityTypes().Where(e => e.HasSharedClrType), Is.Empty);
            Assert.That(context.Model.GetEntityTypes().Select(e => e.FindHistory()), Has.All.Null);
        });
    }

    [Test]
    public void Postgres_HistoryTables_HaveTheirTablesColumns_AndTheConstraint()
    {
        using var context = Context(DatabaseProvider.Postgres);

        foreach (var entityType in context.Model.GetEntityTypes().Where(e => e.FindHistory() is not null))
        {
            var history = entityType.FindHistory()!;
            var key = entityType.FindPrimaryKey()!.Properties.Select(p => p.Name).ToList();
            var historyTable = RowHistory.HistoryName(entityType.GetTableName()!);

            Assert.Multiple(() =>
            {
                Assert.That(history.GetTableName(), Is.EqualTo(historyTable));
                Assert.That(history.GetSchema(), Is.EqualTo(entityType.GetSchema()));
                Assert.That(entityType.FindProperty(RowHistory.PeriodStart)!.GetColumnType(), Is.EqualTo("timestamp without time zone"));
                Assert.That(history.FindProperty(RowHistory.Valid)!.GetColumnType(), Is.EqualTo("tsrange"));
                Assert.That(history.FindPrimaryKey()!.Properties.Select(p => p.Name), Is.EqualTo(new[] { RowHistory.HistoryID }));

                foreach (var property in entityType.GetProperties().Where(p => p.Name != RowHistory.PeriodStart))
                {
                    var column = history.FindProperty(property.Name);
                    Assert.That(column, Is.Not.Null, $"{historyTable} has no {property.Name}");
                    // A facet RowHistory.Configure doesn't copy shows here: copy it there.
                    Assert.That(column!.GetColumnType(), Is.EqualTo(property.GetColumnType()), $"{historyTable}.{property.Name}'s type");
                    Assert.That(column!.GetColumnName(), Is.EqualTo(property.GetColumnName()));
                    Assert.That(column!.IsNullable, Is.EqualTo(!key.Contains(property.Name)), $"{historyTable}.{property.Name} is nullable but for the key");
                    Assert.That(column!.ValueGenerated, Is.EqualTo(ValueGenerated.Never));
                }

                var constraint = history.GetIndexes().Single(i => i.GetDatabaseName() == RowHistory.ConstraintName(historyTable));
                Assert.That(constraint.Properties.Select(p => p.Name), Is.EqualTo(key.Append(RowHistory.Valid)));
                Assert.That(history.GetForeignKeys(), Is.Empty);
            });
        }
    }

    // The database changes rows ON DELETE SET NULL without the app seeing it, so their history would
    // be missing a version, and the next one would overlap the last (RowHistoryInterceptor reads only
    // what ON DELETE CASCADE deletes).
    [Test]
    public void NoForeignKey_SetsNullInTheDatabase()
    {
        using var context = Context(DatabaseProvider.Postgres);

        var setNull = context.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.SetNull)
            .Select(fk => $"{fk.DeclaringEntityType.DisplayName()} -> {fk.PrincipalEntityType.DisplayName()}");

        Assert.That(setNull, Is.Empty);
    }

    [TestCase(DatabaseProvider.SqlServer, false)]
    [TestCase(DatabaseProvider.Postgres, true)]
    public void UseGrifballDatabase_RecordsHistory_OnPostgresOnly(DatabaseProvider provider, bool records)
    {
        using var context = Context(provider);

        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors ?? [];

        Assert.Multiple(() =>
        {
            Assert.That(interceptors.Contains(RowHistoryInterceptor.Instance), Is.EqualTo(records));
            Assert.That(context.GetService<IMigrationsSqlGenerator>() is PostgresMigrationsSqlGenerator, Is.EqualTo(records));
        });
    }

    [Test]
    public void Interceptor_ComesBeforeThoseAddedAfter()
    {
        var audit = new AuditInterceptor(NSubstitute.Substitute.For<Database.Services.ICurrentUserService>());
        using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Postgres, "Host=example").AddInterceptors(audit).Options);

        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors!;

        Assert.That(interceptors, Is.EqualTo(new IInterceptor[] { RowHistoryInterceptor.Instance, audit }));
    }

    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = Context(DatabaseProvider.Postgres);
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.Model);
        return string.Join("", commands.Select(c => c.CommandText)).Trim();
    }

    [Test]
    public void Migrations_CreateTheHistoryIndex_AsTheConstraint()
    {
        var sql = Sql(new CreateIndexOperation
        {
            Name = "AK_TeamsHistory_Valid",
            Schema = "Event",
            Table = "TeamsHistory",
            Columns = ["TeamID", "Valid"],
        });

        Assert.That(sql, Is.EqualTo(@"ALTER TABLE ""Event"".""TeamsHistory"" ADD CONSTRAINT ""AK_TeamsHistory_Valid"" UNIQUE (""TeamID"", ""Valid"" WITHOUT OVERLAPS);"));
    }

    [Test]
    public void Migrations_DropTheHistoryIndex_AsTheConstraint()
    {
        var sql = Sql(new DropIndexOperation { Name = "AK_TeamsHistory_Valid", Schema = "Event", Table = "TeamsHistory" });

        Assert.That(sql, Is.EqualTo(@"ALTER TABLE ""Event"".""TeamsHistory"" DROP CONSTRAINT ""AK_TeamsHistory_Valid"";"));
    }

    [Test]
    public void Migrations_OtherIndexes_AreIndexes()
    {
        var create = Sql(new CreateIndexOperation { Name = "IX_Teams_SeasonID", Schema = "Event", Table = "Teams", Columns = ["SeasonID"] });
        var drop = Sql(new DropIndexOperation { Name = "IX_Teams_SeasonID", Schema = "Event", Table = "Teams" });

        Assert.Multiple(() =>
        {
            Assert.That(create, Is.EqualTo(@"CREATE INDEX ""IX_Teams_SeasonID"" ON ""Event"".""Teams"" (""SeasonID"");"));
            Assert.That(drop, Is.EqualTo(@"DROP INDEX ""Event"".""IX_Teams_SeasonID"";"));
        });
    }
}
