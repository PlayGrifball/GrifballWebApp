using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

#pragma warning disable EF1001 // EF's temporal query roots, as PostgresTemporalQueries takes them.

namespace GrifballWebApp.Test;

/// <summary>
/// The SQL PostgresTemporalQueries reads row history with, with no database: these run on both. What
/// the queries return, on both databases, is TemporalQueryTests.
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
    public void Sql_ReadsTheTableAndItsHistory_WithSqlServersConditions()
    {
        const string start = "\"PeriodStart\"";
        const string end = "\"PeriodEnd\"";
        var a = PostgresTemporalQueries.Literal(A);
        var b = PostgresTemporalQueries.Literal(B);

        var asOf = Sql(e => new TemporalAsOfQueryRootExpression(e, A));
        var all = Sql(e => new TemporalAllQueryRootExpression(e));

        Assert.Multiple(() =>
        {
            Assert.That(asOf, Does.StartWith("SELECT \"SeasonID\", "));
            Assert.That(asOf, Does.Contain($" FROM \"Event\".\"Seasons\" WHERE {start} <= {a} AND {end} > {a} UNION ALL SELECT "));
            Assert.That(asOf, Does.EndWith($" FROM \"Event\".\"SeasonsHistory\" WHERE {start} <= {a} AND {end} > {a} AND {start} < {end}"));
            Assert.That(all, Does.Contain(" FROM \"Event\".\"Seasons\" UNION ALL SELECT "));
            Assert.That(all, Does.EndWith($" FROM \"Event\".\"SeasonsHistory\" WHERE {start} < {end}"));
            Assert.That(Sql(e => new TemporalFromToQueryRootExpression(e, A, B)), Does.EndWith($"WHERE {start} < {b} AND {end} > {a} AND {start} < {end}"));
            Assert.That(Sql(e => new TemporalBetweenQueryRootExpression(e, A, B)), Does.EndWith($"WHERE {start} <= {b} AND {end} > {a} AND {start} < {end}"));
            Assert.That(Sql(e => new TemporalContainedInQueryRootExpression(e, A, B)), Does.EndWith($"WHERE {start} >= {a} AND {end} <= {b} AND {start} < {end}"));
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
}
