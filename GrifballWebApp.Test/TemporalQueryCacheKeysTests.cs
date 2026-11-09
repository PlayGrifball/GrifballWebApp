using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

#pragma warning disable EF1001 // EF's temporal roots, built as its query compiler would

namespace GrifballWebApp.Test;

/// <summary>
/// The temporal part of a compiled-query cache key (<see cref="TemporalQueryCacheKeys"/>): each temporal root's
/// operation, entity type and times, so no two different temporal queries, nor a temporal and a plain one, share a key.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TemporalQueryCacheKeysTests
{
    private static readonly DateTime A = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime B = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IEntityType Seasons()
    {
        using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.SqlServer, "Server=offline;Database=x").Options);
        return context.Model.FindEntityType(typeof(Season))!;
    }

    private static Expression TakeOne(Expression root)
        => Expression.Call(typeof(Queryable), nameof(Queryable.Take), [typeof(Season)], root, Expression.Constant(1));

    [Test]
    public void APlainQuery_HasNoTemporalPart_AndKeepsEFsKey()
    {
        var query = TakeOne(new EntityQueryRootExpression(Seasons()));
        var key = new object();

        Assert.Multiple(() =>
        {
            Assert.That(TemporalQueryCacheKeys.Signature(query), Is.Null);
            Assert.That(TemporalQueryCacheKeys.With(key, query), Is.SameAs(key));
        });
    }

    [Test]
    public void EachTemporalOperationAndTime_HasItsOwnKey()
    {
        var seasons = Seasons();
        var queries = new Expression[]
        {
            new TemporalAllQueryRootExpression(seasons),
            new TemporalAsOfQueryRootExpression(seasons, A),
            new TemporalAsOfQueryRootExpression(seasons, B),
            new TemporalFromToQueryRootExpression(seasons, A, B),
            new TemporalBetweenQueryRootExpression(seasons, A, B),
            new TemporalContainedInQueryRootExpression(seasons, A, B),
            new PostgresTemporalAsOfQueryRootExpression(seasons, "__grif_temporal_0"),
        }.Select(TakeOne).ToList();
        var key = new object();

        var keys = queries.Select(q => TemporalQueryCacheKeys.With(key, q)).ToList();
        var signatures = queries.Select(TemporalQueryCacheKeys.Signature).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(signatures, Is.All.Not.Null);
            Assert.That(signatures, Is.Unique);
            Assert.That(keys, Has.None.SameAs(key));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count));
            Assert.That(signatures[3], Does.Contain($"{A.Ticks},{B.Ticks}"));
            Assert.That(signatures[6], Does.Contain("__grif_temporal_0"));
        });
    }

    [Test]
    public void TheSameTemporalQuery_HasTheSameKey()
    {
        var seasons = Seasons();
        var key = new object();

        Assert.That(TemporalQueryCacheKeys.With(key, TakeOne(new TemporalAsOfQueryRootExpression(seasons, A))),
            Is.EqualTo(TemporalQueryCacheKeys.With(key, TakeOne(new TemporalAsOfQueryRootExpression(seasons, A)))));
    }
}
