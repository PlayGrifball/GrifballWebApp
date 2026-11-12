using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

#pragma warning disable EF1001 // EF's own cache key generators, extended

namespace GrifballWebApp.Infrastructure;

/// <summary>
/// EF's compiled-query cache keys, told the query's temporal operations. EF compares a query with the cached ones by
/// its expression, and a plain root (<see cref="EntityQueryRootExpression"/>) equals any root of its entity type,
/// temporal ones included, which share its hash code; a temporal root equals only its own kind. So once a plain query
/// of a table is cached, the same query made temporal (TemporalAll, TemporalAsOf and the rest) finds it, and runs
/// plain: the current rows instead of the history, and no refusal for a table without history. Each key here carries
/// the query's temporal roots too (<see cref="Signature"/>), so a temporal query is never another's.
/// </summary>
public static class TemporalQueryCacheKeys
{
    /// <summary>EF's key, with the query's temporal roots; EF's alone when it has none.</summary>
    public static object With(object key, Expression query)
        => Signature(query) is { } temporal ? new TemporalKey(key, temporal) : key;

    /// <summary>
    /// Each temporal root of <paramref name="query"/> in order: its operation, entity type and times (or the
    /// parameter holding them, PostgresTemporalQueries); null when it has none.
    /// </summary>
    public static string? Signature(Expression query)
    {
        var finder = new TemporalRoots();
        finder.Visit(query);
        return finder.Found.Length == 0 ? null : finder.Found.ToString();
    }

    private sealed record TemporalKey(object Key, string Temporal);

    private sealed class TemporalRoots : ExpressionVisitor
    {
        public StringBuilder Found { get; } = new();

        protected override Expression VisitExtension(Expression node)
        {
            if (node is TemporalQueryRootExpression root)
            {
                Found.Append(root.GetType().Name).Append('(').Append(root.EntityType.Name).Append(';').Append(Times(root)).Append(')');
                return node;
            }
            // EF's other nodes in a query not yet compiled are leaves (other roots, parameters): nothing under them
            return node;
        }

        private static string Times(TemporalQueryRootExpression root) => root switch
        {
            IPostgresTemporalParameterRoot parameterized => parameterized.ParameterName,
            TemporalAsOfQueryRootExpression asOf => asOf.PointInTime.Ticks.ToString(),
            TemporalRangeQueryRootExpression range => $"{range.From.Ticks},{range.To.Ticks}",
            _ => "",
        };
    }
}

/// <summary>SQL Server's key generator, its keys told the query's temporal roots (<see cref="TemporalQueryCacheKeys"/>).</summary>
public class SqlServerTemporalCompiledQueryCacheKeyGenerator(
    CompiledQueryCacheKeyGeneratorDependencies dependencies,
    RelationalCompiledQueryCacheKeyGeneratorDependencies relationalDependencies,
    ISqlServerConnection sqlServerConnection)
    : SqlServerCompiledQueryCacheKeyGenerator(dependencies, relationalDependencies, sqlServerConnection)
{
#pragma warning disable S2306 // EF's parameter name, which an override keeps (S927)
    public override object GenerateCacheKey(Expression query, bool async)
#pragma warning restore S2306
        => TemporalQueryCacheKeys.With(base.GenerateCacheKey(query, async), query);
}

/// <summary>Postgres's key generator, its keys told the query's temporal roots (<see cref="TemporalQueryCacheKeys"/>).</summary>
public class NpgsqlTemporalCompiledQueryCacheKeyGenerator(
    CompiledQueryCacheKeyGeneratorDependencies dependencies,
    RelationalCompiledQueryCacheKeyGeneratorDependencies relationalDependencies)
    : NpgsqlCompiledQueryCacheKeyGenerator(dependencies, relationalDependencies)
{
#pragma warning disable S2306 // EF's parameter name, which an override keeps (S927)
    public override object GenerateCacheKey(Expression query, bool async)
#pragma warning restore S2306
        => TemporalQueryCacheKeys.With(base.GenerateCacheKey(query, async), query);
}

/// <summary>SQLite's key generator (EF's relational one), its keys told the query's temporal roots (<see cref="TemporalQueryCacheKeys"/>).</summary>
public class SqliteTemporalCompiledQueryCacheKeyGenerator(
    CompiledQueryCacheKeyGeneratorDependencies dependencies,
    RelationalCompiledQueryCacheKeyGeneratorDependencies relationalDependencies)
    : RelationalCompiledQueryCacheKeyGenerator(dependencies, relationalDependencies)
{
#pragma warning disable S2306 // EF's parameter name, which an override keeps (S927)
    public override object GenerateCacheKey(Expression query, bool async)
#pragma warning restore S2306
        => TemporalQueryCacheKeys.With(base.GenerateCacheKey(query, async), query);
}
