using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

    [Test]
    public void Instant_IsAUtcTime_ToTheMicrosecond_TheEndsOfTimeKept()
    {
        var time = A.AddTicks(1234567);

        Assert.Multiple(() =>
        {
            Assert.That(PostgresTemporalQueries.Instant(time), Is.EqualTo(A.AddTicks(1234560)));
            Assert.That(PostgresTemporalQueries.Instant(time).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(PostgresTemporalQueries.Instant(time, roundUp: true), Is.EqualTo(A.AddTicks(1234570)));
            Assert.That(PostgresTemporalQueries.Instant(A, roundUp: true), Is.EqualTo(A), "a whole microsecond stays");
            Assert.That(PostgresTemporalQueries.Instant(DateTime.SpecifyKind(time, DateTimeKind.Unspecified)).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MaxValue), Is.EqualTo(DateTime.MaxValue));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MinValue), Is.EqualTo(DateTime.MinValue));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MaxValue.AddTicks(-1), roundUp: true), Is.EqualTo(DateTime.MaxValue));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MaxValue.AddTicks(-1), roundUp: true).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.Throws<InvalidOperationException>(() => PostgresTemporalQueries.Instant(DateTime.Now));
        });
    }

    [Test]
    public void Instants_AreEachOperationsTimes_RoundedAsItsConditionNeeds()
    {
        using var context = Postgres();
        var e = context.Model.FindEntityType(typeof(Season))!;
        var a = A.AddTicks(1);
        var b = B.AddTicks(1);
        var up = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond - 1);

        Assert.Multiple(() =>
        {
            Assert.That(PostgresTemporalQueries.Instants(new TemporalAsOfQueryRootExpression(e, a)), Is.EqualTo(new[] { A }));
            Assert.That(PostgresTemporalQueries.Instants(new TemporalFromToQueryRootExpression(e, a, b)), Is.EqualTo(new[] { A, b + up }));
            Assert.That(PostgresTemporalQueries.Instants(new TemporalBetweenQueryRootExpression(e, a, b)), Is.EqualTo(new[] { A, B }));
            Assert.That(PostgresTemporalQueries.Instants(new TemporalContainedInQueryRootExpression(e, a, b)), Is.EqualTo(new[] { a + up, B }));
            Assert.That(PostgresTemporalQueries.Instants(new TemporalAllQueryRootExpression(e)), Is.Empty);
            Assert.That(PostgresTemporalQueries.Instants(new PostgresTemporalAsOfQueryRootExpression(e, "p")), Is.Null, "its times are its parameter's");
            Assert.That(PostgresTemporalQueries.Instants(new SomeOtherTemporalQueryRoot(e)), Is.Null);
        });
    }

    [Test]
    public void Sql_OfARootWithParameters_HasFromSqlsPlaceholders()
    {
        const string start = "\"PeriodStart\"";
        const string end = "\"PeriodEnd\"";

        Assert.Multiple(() =>
        {
            Assert.That(Sql(e => new PostgresTemporalAsOfQueryRootExpression(e, "p")),
                Does.EndWith($" FROM \"Event\".\"SeasonsHistory\" WHERE {start} <= {{0}} AND {end} > {{0}} AND {start} < {end}"));
            Assert.That(Sql(e => new PostgresTemporalAsOfQueryRootExpression(e, "p")), Does.Contain($" FROM \"Event\".\"Seasons\" WHERE {start} <= {{0}} AND {end} > {{0}} UNION ALL "));
            Assert.That(Sql(e => new PostgresTemporalFromToQueryRootExpression(e, "p")), Does.EndWith($"WHERE {start} < {{1}} AND {end} > {{0}} AND {start} < {end}"));
            Assert.That(Sql(e => new PostgresTemporalBetweenQueryRootExpression(e, "p")), Does.EndWith($"WHERE {start} <= {{1}} AND {end} > {{0}} AND {start} < {end}"));
            Assert.That(Sql(e => new PostgresTemporalContainedInQueryRootExpression(e, "p")), Does.EndWith($"WHERE {start} >= {{0}} AND {end} <= {{1}} AND {start} < {end}"));
        });
    }

    private static IEnumerable<Func<IEntityType, string, TemporalQueryRootExpression>> ParameterRoots() =>
    [
        (e, p) => new PostgresTemporalAsOfQueryRootExpression(e, p),
        (e, p) => new PostgresTemporalFromToQueryRootExpression(e, p),
        (e, p) => new PostgresTemporalBetweenQueryRootExpression(e, p),
        (e, p) => new PostgresTemporalContainedInQueryRootExpression(e, p),
    ];

    // For EF's query cache, a root with parameters is its operation, entity type and parameter: the same
    // whatever the times. Its own times aren't there to read.
    [TestCaseSource(nameof(ParameterRoots))]
    public void ParameterRoot_IsItsOperationEntityAndParameter(Func<IEntityType, string, TemporalQueryRootExpression> create)
    {
        using var context = Postgres();
        var season = context.Model.FindEntityType(typeof(Season))!;
        var team = context.Model.FindEntityType(typeof(Team))!;
        var root = create(season, "p");
        var operation = root.GetType().Name.Replace("PostgresTemporal", "Temporal").Replace("QueryRootExpression", "");

        Assert.Multiple(() =>
        {
            Assert.That(root, Is.EqualTo(create(season, "p")));
            Assert.That(root.GetHashCode(), Is.EqualTo(create(season, "p").GetHashCode()));
            Assert.That(root, Is.Not.EqualTo(create(season, "q")));
            Assert.That(root, Is.Not.EqualTo(create(team, "p")));
            Assert.That(root, Is.Not.EqualTo(ParameterRoots().Select(c => c(season, "p")).First(r => r.GetType() != root.GetType())));
            Assert.That(root.Equals(root), Is.True);
            Assert.That(root.Equals(null), Is.False);
            Assert.That(((IPostgresTemporalParameterRoot)root).ParameterName, Is.EqualTo("p"));
            Assert.That(root.DetachQueryProvider(), Is.SameAs(root));
            Assert.That(root.UpdateEntityType(season), Is.EqualTo(root));
            Assert.Throws<InvalidOperationException>(() => root.UpdateEntityType(team));
            Assert.That(new ExpressionPrinter().PrintExpression(root), Is.EqualTo($"DbSet<Season>().{operation}(@p)"));
            var times = root switch
            {
                TemporalAsOfQueryRootExpression asOf => new Action[] { () => _ = asOf.PointInTime },
                TemporalRangeQueryRootExpression range => [() => _ = range.From, () => _ = range.To],
                _ => [],
            };
            Assert.That(times, Is.Not.Empty);
            foreach (var time in times)
                Assert.That(Assert.Throws<InvalidOperationException>(time)!.Message, Does.Contain("'p'"));
        });
    }

    // The query compiler (registered by UseGrifballDatabase) takes each root's times out into the query's
    // parameters, one parameter for each operation and times; EF.CompileQuery's roots keep theirs.
    [Test]
    public void QueryCompiler_TakesTheTimesOutIntoTheParameters()
    {
        using var context = Postgres();
        var compiler = (PostgresTemporalQueryCompiler)context.GetService<IQueryCompiler>();
        var logger = context.GetService<IDiagnosticsLogger<DbLoggerCategory.Query>>();
        var query = context.Seasons.TemporalAsOf(A).Where(s => s.SeasonID == 1)
            .Concat(context.Seasons.TemporalAsOf(A))
            .Concat(context.Seasons.TemporalAsOf(B))
            .Concat(context.Seasons.TemporalFromTo(A, B))
            .Concat(context.Seasons.TemporalAll()).Expression;
        var parameters = new Dictionary<string, object?>();
        var compiledParameters = new Dictionary<string, object?>();

        var roots = Roots(compiler.ExtractParameters(query, parameters, logger));
        var compiledRoots = Roots(compiler.ExtractParameters(query, compiledParameters, logger, compiledQuery: true));

        Assert.Multiple(() =>
        {
            Assert.That(roots.Select(r => (r.GetType().Name, (r as IPostgresTemporalParameterRoot)?.ParameterName)), Is.EquivalentTo(new (string, string?)[]
            {
                (nameof(PostgresTemporalAsOfQueryRootExpression), "__grif_temporal_0"),
                (nameof(PostgresTemporalAsOfQueryRootExpression), "__grif_temporal_0"),
                (nameof(PostgresTemporalAsOfQueryRootExpression), "__grif_temporal_1"),
                (nameof(PostgresTemporalFromToQueryRootExpression), "__grif_temporal_2"),
                (nameof(TemporalAllQueryRootExpression), null),
            }));
            Assert.That(parameters.Where(p => p.Key.StartsWith("__grif_temporal_")).OrderBy(p => p.Key).Select(p => p.Value), Is.EqualTo(new object[]
            {
                new object[] { A }, new object[] { B }, new object[] { A, B },
            }));
            Assert.That(compiledRoots.Select(r => r.GetType().Name), Is.EquivalentTo(new[]
            {
                nameof(TemporalAsOfQueryRootExpression), nameof(TemporalAsOfQueryRootExpression), nameof(TemporalAsOfQueryRootExpression),
                nameof(TemporalFromToQueryRootExpression), nameof(TemporalAllQueryRootExpression),
            }));
            Assert.That(compiledParameters.Keys.Where(k => k.StartsWith("__grif_temporal_")), Is.Empty);
        });
    }

    private static List<TemporalQueryRootExpression> Roots(Expression expression)
    {
        var roots = new List<TemporalQueryRootExpression>();
        new RootFinder(roots).Visit(expression);
        return roots;
    }

    private sealed class RootFinder(List<TemporalQueryRootExpression> roots) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            if (node is TemporalQueryRootExpression root)
                roots.Add(root);
            return base.VisitExtension(node);
        }
    }

    // The SQL of an AsOf, whatever its time: the time a parameter, and the SQL the same.
    [Test]
    public void ToQueryString_HasTheTimeAsAParameter()
    {
        using var context = Postgres();
        string Sql(DateTime time) => context.Seasons.TemporalAsOf(time).Include(s => s.Teams).Where(s => s.SeasonID == 1).ToQueryString();
        static string WithoutParameters(string sql) => string.Join("\n", sql.Split('\n').Where(l => !l.StartsWith("-- ")));

        var a = Sql(A);
        var b = Sql(B);

        Assert.Multiple(() =>
        {
            Assert.That(WithoutParameters(a), Is.EqualTo(WithoutParameters(b)));
            Assert.That(WithoutParameters(a), Does.Not.Contain("2026"));
            Assert.That(a, Does.Contain("2026-10-01"));
            Assert.That(b, Does.Contain("2026-10-02"));
            Assert.That(a.Split('\n').Count(l => l.StartsWith("-- ")), Is.EqualTo(2), "the time once, for the season and its teams, and the key");
        });
    }

    private sealed class SomeOtherTemporalQueryRoot(IEntityType entityType) : TemporalQueryRootExpression(entityType)
    {
        public override Expression DetachQueryProvider() => this;
    }
}
