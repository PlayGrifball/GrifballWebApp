using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

#pragma warning disable EF1001 // EF's temporal query roots, as PostgresTemporalQueries takes them.

namespace GrifballWebApp.Test;

/// <summary>
/// The SQL PostgresTemporalQueries reads row history with, and the model's periods, with no database:
/// these run on both. What the queries return, on both databases, is TemporalQueryTests.
/// </summary>
[TestFixture]
public class PostgresTemporalQueriesTests
{
    private static GrifballContext Postgres() => new(new DbContextOptionsBuilder<GrifballContext>()
        .UseGrifballDatabase(DatabaseProvider.Postgres, "Host=unused;Database=unused").Options);

    private static readonly DateTime A = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime B = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Literal_IsAUtcTimestamptz_ToTheMicrosecond()
    {
        var time = A.AddTicks(1234567);

        Assert.Multiple(() =>
        {
            Assert.That(PostgresTemporalQueries.Literal(time), Is.EqualTo("'2026-10-01 12:00:00.123456+00'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(time, roundUp: true), Is.EqualTo("'2026-10-01 12:00:00.123457+00'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(A, roundUp: true), Is.EqualTo("'2026-10-01 12:00:00.000000+00'::timestamptz"), "a whole microsecond stays");
            Assert.That(PostgresTemporalQueries.Literal(DateTime.SpecifyKind(time, DateTimeKind.Unspecified)), Is.EqualTo(PostgresTemporalQueries.Literal(time)));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MaxValue), Is.EqualTo("'infinity'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MinValue), Is.EqualTo("'-infinity'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MaxValue.AddTicks(-1)), Is.EqualTo("'9999-12-31 23:59:59.999999+00'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MaxValue.AddTicks(-1), roundUp: true), Is.EqualTo("'infinity'::timestamptz"));
        });
    }

    [Test]
    public void Literal_RefusesLocalTimes()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PostgresTemporalQueries.Literal(DateTime.Now));

        Assert.That(ex!.Message, Does.Contain("Kind Local"));
    }

    private static string Sql(Func<IEntityType, TemporalQueryRootExpression> root)
    {
        using var context = Postgres();
        return PostgresTemporalQueries.Sql(root(context.Model.FindEntityType(typeof(Season))!));
    }

    [Test]
    public void Sql_ReadsPeriodsView_WithSqlServersConditions()
    {
        const string start = "\"PeriodStart\"";
        const string end = "\"PeriodEnd\"";
        var a = PostgresTemporalQueries.Literal(A);
        var b = PostgresTemporalQueries.Literal(B);
        const string view = " FROM \"Event\".\"Seasons_with_history\"";

        var asOf = Sql(e => new TemporalAsOfQueryRootExpression(e, A));
        var all = Sql(e => new TemporalAllQueryRootExpression(e));

        Assert.Multiple(() =>
        {
            Assert.That(asOf, Does.StartWith("SELECT \"SeasonID\", "));
            Assert.That(asOf, Does.EndWith($"{view} WHERE {start} <= {a} AND {end} > {a}"));
            Assert.That(all, Does.EndWith(view));
            Assert.That(Sql(e => new TemporalFromToQueryRootExpression(e, A, B)), Does.EndWith($"{view} WHERE {start} < {b} AND {end} > {a}"));
            Assert.That(Sql(e => new TemporalBetweenQueryRootExpression(e, A, B)), Does.EndWith($"{view} WHERE {start} <= {b} AND {end} > {a}"));
            Assert.That(Sql(e => new TemporalContainedInQueryRootExpression(e, A, B)), Does.EndWith($"{view} WHERE {start} >= {a} AND {end} <= {b}"));
        });
    }

    [Test]
    public void Sql_SelectsEveryColumn_ThePeriodsIncluded()
    {
        using var context = Postgres();
        var season = context.Model.FindEntityType(typeof(Season))!;
        var columns = season.GetTableMappings().First().Table.Columns.Select(c => c.Name).ToList();

        var sql = Sql(e => new TemporalAllQueryRootExpression(e));

        Assert.That(sql, Does.StartWith($"SELECT {string.Join(", ", columns.Select(c => $"\"{c}\""))} FROM "));
        Assert.That(columns, Does.Contain("PeriodStart").And.Contain("PeriodEnd"));
    }

    [Test]
    public void Sql_OfATableWithoutHistory_Throws()
    {
        using var context = Postgres();
        var reschedule = context.Model.FindEntityType(typeof(MatchReschedule))!;

        var ex = Assert.Throws<InvalidOperationException>(() => PostgresTemporalQueries.Sql(new TemporalAllQueryRootExpression(reschedule)));

        Assert.That(ex!.Message, Does.Contain("'MatchReschedule' has no history"));
    }

    [Test]
    public void Sql_OfAnUnknownTemporalOperation_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Sql(e => new SomeOtherTemporalQueryRoot(e)));

        Assert.That(ex!.Message, Does.Contain(nameof(SomeOtherTemporalQueryRoot)));
    }

    private sealed class SomeOtherTemporalQueryRoot(IEntityType entityType) : TemporalQueryRootExpression(entityType)
    {
        public override Expression DetachQueryProvider() => this;
    }

    [Test]
    public void Queries_ArePeriodsView_IncludesAsOfTheSameTime()
    {
        using var context = Postgres();

        var sql = context.SeasonMatches.TemporalAsOf(A).Include(m => m.Season).ToQueryString();

        Assert.That(sql, Does.Contain("FROM \"Event\".\"SeasonMatches_with_history\" WHERE \"PeriodStart\" <= ")
            .And.Contain("FROM \"Event\".\"Seasons_with_history\" WHERE \"PeriodStart\" <= "));
    }

    private const string HistoryQuery = "SELECT s.\"SeasonID\" FROM (\n    SELECT \"SeasonID\" FROM \"Event\".\"Seasons_with_history\"\n) AS s";

    private static PostgresException Undefined(int position, string sqlState = PostgresErrorCodes.UndefinedTable) =>
        new("relation does not exist", "ERROR", "ERROR", sqlState, position: position);

    // The error's position is where the missing name starts, 1-based.
    [Test]
    public void IsMissingHistoryView_TheNameAtTheErrorsPosition()
    {
        var view = HistoryQuery.IndexOf("\"Event\"", StringComparison.Ordinal) + 1;
        var table = HistoryQuery.IndexOf("\"SeasonID\"", StringComparison.Ordinal) + 1;
        const string other = "SELECT 1 FROM \"Event\".\"Seasons\" JOIN \"Event\".\"Seasons_with_history\" ON true";
        const string unquoted = "SELECT 1 FROM nope";

        Assert.Multiple(() =>
        {
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(view), HistoryQuery), Is.True);
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(view, PostgresErrorCodes.UndefinedColumn), HistoryQuery), Is.False);
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(table), HistoryQuery), Is.False, "another name");
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(15), other), Is.False, "another table, a view after it");
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(15), unquoted), Is.False, "the name ends the command");
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(0), HistoryQuery), Is.True, "no position: anywhere");
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(0), unquoted), Is.False);
            Assert.That(PostgresTemporalQueries.IsMissingHistoryView(Undefined(HistoryQuery.Length + 1), HistoryQuery), Is.True, "past the end: anywhere");
        });
    }

    [Test]
    public void NoHistoryException_SaysSo_TheDatabasesErrorInside()
    {
        var missing = Undefined(1);

        var ex = PostgresTemporalQueries.NoHistoryException(missing);

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.StartWith("Row history isn't enabled on this PostgreSQL database: the periods extension isn't installed"));
            Assert.That(ex.Message, Does.Contain("readme.md, Row history").And.EndWith("relation does not exist."));
            Assert.That(ex.InnerException, Is.SameAs(missing));
        });
    }

    // Only what a query may run with: the connection's empty command, and any reading a periods view.
    [Test]
    public void CommandBuilder_BuildsTheHistoryCommand_ForQueriesOnly()
    {
        using var context = Postgres();
        var factory = context.GetService<IRelationalCommandBuilderFactory>();

        Assert.Multiple(() =>
        {
            Assert.That(factory, Is.InstanceOf<PostgresTemporalRelationalCommandBuilderFactory>());
            Assert.That(factory.Create().Build(), Is.InstanceOf<PostgresTemporalRelationalCommand>());
            Assert.That(factory.Create().Append(HistoryQuery).Build(), Is.InstanceOf<PostgresTemporalRelationalCommand>());
            Assert.That(factory.Create().Append("UPDATE \"Event\".\"Seasons\" SET x = 1").Build(), Is.Not.InstanceOf<PostgresTemporalRelationalCommand>());
        });
    }

    [TestCase("Seasons", "with_history", "Seasons_with_history")]
    [TestCase("Seasons", "history", "Seasons_history")]
    public void PeriodsName_IsPeriods(string table, string suffix, string name)
    {
        Assert.That(RowHistory.PeriodsName(table, suffix), Is.EqualTo(name));
    }

    [Test]
    public void PeriodsName_OfALongTable_IsCutAsPeriodsCutsIt()
    {
        // periods._choose_name: 61 bytes at most, the table's part cut.
        var name = RowHistory.PeriodsName(new string('t', 70), "with_history");

        Assert.That(name, Is.EqualTo(new string('t', 48) + "_with_history"));
    }

    private static GrifballContext SqlServer() => new(new DbContextOptionsBuilder<GrifballContext>()
        .UseGrifballDatabase(DatabaseProvider.SqlServer, "Server=unused;Database=unused").Options);

    private static string[] TableNames(IEnumerable<ITable> tables) => [.. tables.Select(t => $"{t.Schema}.{t.Name}").Order()];

    [Test]
    public void Model_TheTablesTemporalOnSqlServer_HaveThePeriod_TheDatabasesToFill()
    {
        using var postgres = Postgres();
        using var sqlServer = SqlServer();
        var tables = postgres.Model.GetRelationalModel().Tables.ToList();
        var withPeriod = tables.Where(t => t.FindColumn(RowHistory.PeriodStart) is not null && t.FindColumn(RowHistory.PeriodEnd) is not null);
        var temporal = sqlServer.Model.GetRelationalModel().Tables.Where(t => t.EntityTypeMappings.Any(m => ((IEntityType)m.TypeBase).IsTemporal()));
        var periods = postgres.Model.GetEntityTypes().Where(e => e.HasSystemTimePeriod())
            .SelectMany(e => new[] { e.FindProperty(RowHistory.PeriodStart)!, e.FindProperty(RowHistory.PeriodEnd)! })
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(TableNames(withPeriod), Is.EqualTo(TableNames(temporal)));
            Assert.That(withPeriod.Count(), Is.EqualTo(40), "the RowHistory migration's tables");
            Assert.That(postgres.Model.FindEntityType(typeof(MatchReschedule))!.HasSystemTimePeriod(), Is.False);
            Assert.That(tables.Single(t => t.Name == "MatchReschedules").Columns.Select(c => c.Name), Has.None.StartsWith("Period"));
            Assert.That(periods, Has.All.Matches<IProperty>(p =>
                p.IsShadowProperty()
                && p.GetBeforeSaveBehavior() == PropertySaveBehavior.Ignore
                && p.GetAfterSaveBehavior() == PropertySaveBehavior.Ignore
                && p.GetColumnType() == "timestamp with time zone"
                && !p.IsNullable));
        });
    }

    [Test]
    public void Model_OnSqlServer_HasItsOwnTemporalPeriods()
    {
        using var context = SqlServer();
        var season = context.Model.FindEntityType(typeof(Season))!;

        Assert.Multiple(() =>
        {
            Assert.That(season.IsTemporal(), Is.True);
            Assert.That(season.FindProperty(RowHistory.PeriodStart)!.GetDefaultValueSql(), Is.Null);
            Assert.That(context.Model.FindEntityType(typeof(MatchReschedule))!.FindProperty(RowHistory.PeriodStart), Is.Null);
        });
    }
}
