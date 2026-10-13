using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

// EF's temporal query roots, its query compiler, its navigation expansion helper and Npgsql's translator
// are internal API ("pubternal"): what EF's own SQL Server provider builds its temporal queries on.
#pragma warning disable EF1001

namespace GrifballWebApp.Database;

/// <summary>
/// EF's temporal queries on Postgres, over row history's tables (PostgresHistory): the same calls as
/// on SQL Server - context.Matches.TemporalAsOf(t), TemporalAll(), TemporalFromTo(a, b),
/// TemporalBetween(a, b), TemporalContainedIn(a, b) - with the same results. EF's TemporalXxx methods
/// work on any provider: each wraps the DbSet's query root in a TemporalQueryRootExpression, which SQL
/// Server translates to FOR SYSTEM_TIME and no other provider translates at all. On Postgres the
/// translator (registered by UseGrifballDatabase) turns each into FromSql over the table and its history
/// table (<see cref="Sql(TemporalQueryRootExpression)"/>), which EF then composes as it would any FromSql:
/// filters, ordering, projections, joins, Includes, set operations. The navigation helper carries an AsOf
/// to the navigations and Includes of the query, and refuses what SQL Server refuses, with its messages.
/// <para>
/// The times are parameters: EF compiles a query once, whatever its times, and caches it, as it does
/// any query's captured values. EF's temporal roots hold their times, and EF's query cache compares them,
/// so SQL Server compiles a query anew for each time; here the query compiler
/// (<see cref="PostgresTemporalQueryCompiler"/>) takes each root's times out into the query's own
/// parameters, before the cache, leaving a root with the parameter's name in their place.
/// </para>
/// </summary>
public static class PostgresTemporalQueries
{
    private const string Start = $"\"{PostgresHistory.PeriodStart}\"";
    private const string End = $"\"{PostgresHistory.PeriodEnd}\"";

    /// <summary>
    /// The versions <paramref name="root"/> asks for, every column of the table, PeriodStart and
    /// PeriodEnd included, by SQL Server's rules: AS OF t, PeriodStart &lt;= t AND PeriodEnd &gt; t; FROM a
    /// TO b, PeriodStart &lt; b AND PeriodEnd &gt; a; BETWEEN a AND b, PeriodStart &lt;= b AND PeriodEnd &gt; a;
    /// CONTAINED IN (a, b), PeriodStart &gt;= a AND PeriodEnd &lt;= b; ALL, every version. Like FOR
    /// SYSTEM_TIME, it leaves out versions that lasted no time (PeriodStart = PeriodEnd: a row changed
    /// again in the transaction that changed it). A root with parameters (<see cref="IPostgresTemporalParameterRoot"/>)
    /// has FromSql's placeholders, {0} and {1}, for its times (<see cref="Instants"/>); any other, its
    /// times as literals (<see cref="Literal"/>).
    /// </summary>
    public static string Sql(TemporalQueryRootExpression root)
    {
        var entityType = root.EntityType;
        var history = entityType.GetRootType().FindHistoryEntityType()
            ?? throw new InvalidOperationException(
                $"'{entityType.DisplayName()}' has no history: its table '{entityType.GetSchemaQualifiedTableName()}' isn't temporal, so it has no temporal queries.");

        var condition = Condition(root);
        if (condition is not null && root is not IPostgresTemporalParameterRoot)
            condition = string.Format(CultureInfo.InvariantCulture, condition, [.. Instants(root)!.Select(i => Literal(i))]);

        var table = entityType.GetTableMappings().First().Table;
        var columns = string.Join(", ", table.Columns.Select(c => Quote(c.Name)));
        var sql = new StringBuilder()
            .Append("SELECT ").Append(columns).Append(" FROM ").Append(Name(table.Schema, table.Name));
        if (condition is not null)
            sql.Append(" WHERE ").Append(condition);
        sql.Append(" UNION ALL SELECT ").Append(columns).Append(" FROM ").Append(Name(history.GetSchema(), history.GetTableName()!))
            .Append(" WHERE ");
        if (condition is not null)
            sql.Append(condition).Append(" AND ");
        sql.Append(Start).Append(" < ").Append(End);
        return sql.ToString();
    }

    /// <summary>The condition on the periods, with {0} and {1} for <see cref="Instants"/>; none for ALL.</summary>
    private static string? Condition(TemporalQueryRootExpression root) => root switch
    {
        TemporalAsOfQueryRootExpression => $"{Start} <= {{0}} AND {End} > {{0}}",
        TemporalFromToQueryRootExpression => $"{Start} < {{1}} AND {End} > {{0}}",
        TemporalBetweenQueryRootExpression => $"{Start} <= {{1}} AND {End} > {{0}}",
        TemporalContainedInQueryRootExpression => $"{Start} >= {{0}} AND {End} <= {{1}}",
        TemporalAllQueryRootExpression => null,
        _ => throw new InvalidOperationException($"Unknown temporal operation {root.GetType().Name}."),
    };

    /// <summary>
    /// The times of EF's <paramref name="root"/>, as its condition compares them (<see cref="Instant"/>):
    /// AS OF t, [t]; the others, [from, to], rounded up where the comparison is PeriodStart &lt; or
    /// PeriodStart &gt;=. Empty for ALL; null for a root this doesn't know, or one with parameters.
    /// </summary>
    public static DateTime[]? Instants(TemporalQueryRootExpression root) => root switch
    {
        IPostgresTemporalParameterRoot => null,
        TemporalAsOfQueryRootExpression asOf => [Instant(asOf.PointInTime)],
        TemporalFromToQueryRootExpression fromTo => [Instant(fromTo.From), Instant(fromTo.To, roundUp: true)],
        TemporalBetweenQueryRootExpression between => [Instant(between.From), Instant(between.To)],
        TemporalContainedInQueryRootExpression containedIn => [Instant(containedIn.From, roundUp: true), Instant(containedIn.To)],
        TemporalAllQueryRootExpression => [],
        _ => null,
    };

    /// <summary>
    /// <paramref name="value"/> as Postgres compares it with a period column: UTC, Unspecified taken as
    /// UTC, Local refused, as every DateTime the app stores (UtcDateTimeConverter). Postgres keeps
    /// microseconds and SQL Server 100ns: a time between two microseconds is rounded down, or up where
    /// <paramref name="roundUp"/> (the comparison is PeriodStart &lt; or PeriodStart &gt;=), so each
    /// comparison with a period column, which holds whole microseconds, gives what it would with the
    /// exact time. DateTime.MinValue and MaxValue stay as they are: Npgsql writes them as -infinity and
    /// infinity, and MaxValue is the PeriodEnd of the current rows.
    /// </summary>
    public static DateTime Instant(DateTime value, bool roundUp = false)
    {
        value = UtcDateTimeConverter.ToUtc(value);
        if (value == DateTime.MaxValue || value == DateTime.MinValue)
            return value;

        var ticks = value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond;
        if (roundUp && ticks != value.Ticks)
        {
            // Past the last microsecond DateTime has: after every time Postgres holds bar infinity.
            if (ticks > DateTime.MaxValue.Ticks - TimeSpan.TicksPerMicrosecond)
                return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            ticks += TimeSpan.TicksPerMicrosecond;
        }
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// <paramref name="value"/> as a timestamptz literal, <see cref="Instant"/>'s time: in the SQL of a
    /// query EF compiles for its times alone (EF.CompileQuery, whose roots keep their times).
    /// </summary>
    public static string Literal(DateTime value, bool roundUp = false)
    {
        var instant = Instant(value, roundUp);
        if (instant == DateTime.MaxValue)
            return "'infinity'::timestamptz";
        if (instant == DateTime.MinValue)
            return "'-infinity'::timestamptz";
        return "'" + instant.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "+00'::timestamptz";
    }

    private static string Name(string? schema, string name) => schema is null ? Quote(name) : $"{Quote(schema)}.{Quote(name)}";

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}

/// <summary>
/// A temporal query root whose times are a query parameter, <see cref="ParameterName"/>: an object[] of
/// the times (PostgresTemporalQueries.Instants), FromSql's arguments. Two are the same root, for EF's query
/// cache, when they're the same operation on the same entity type with the same parameter, whatever the
/// times. Their times are the parameter's alone: the roots' own (PointInTime, From, To) throw.
/// </summary>
public interface IPostgresTemporalParameterRoot
{
    string ParameterName { get; }
}

/// <summary>TemporalAsOf, its time a parameter (IPostgresTemporalParameterRoot).</summary>
public sealed class PostgresTemporalAsOfQueryRootExpression(IEntityType entityType, string parameterName)
    : TemporalAsOfQueryRootExpression(entityType, default), IPostgresTemporalParameterRoot
{
    public string ParameterName { get; } = parameterName;

    public override DateTime PointInTime => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override Expression DetachQueryProvider() => this;

    public override EntityQueryRootExpression UpdateEntityType(IEntityType entityType)
        => PostgresTemporalParameterRoots.Update(this, entityType, e => new PostgresTemporalAsOfQueryRootExpression(e, ParameterName));

    protected override void Print(ExpressionPrinter expressionPrinter)
        => PostgresTemporalParameterRoots.Print(this, expressionPrinter, "TemporalAsOf");

    public override bool Equals(object? obj) => PostgresTemporalParameterRoots.Equal(this, obj);

    public override int GetHashCode() => PostgresTemporalParameterRoots.HashCode(this);
}

/// <summary>TemporalFromTo, its times a parameter (IPostgresTemporalParameterRoot).</summary>
public sealed class PostgresTemporalFromToQueryRootExpression(IEntityType entityType, string parameterName)
    : TemporalFromToQueryRootExpression(entityType, default, default), IPostgresTemporalParameterRoot
{
    public string ParameterName { get; } = parameterName;

    public override DateTime From => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override DateTime To => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override Expression DetachQueryProvider() => this;

    public override EntityQueryRootExpression UpdateEntityType(IEntityType entityType)
        => PostgresTemporalParameterRoots.Update(this, entityType, e => new PostgresTemporalFromToQueryRootExpression(e, ParameterName));

    protected override void Print(ExpressionPrinter expressionPrinter)
        => PostgresTemporalParameterRoots.Print(this, expressionPrinter, "TemporalFromTo");

    public override bool Equals(object? obj) => PostgresTemporalParameterRoots.Equal(this, obj);

    public override int GetHashCode() => PostgresTemporalParameterRoots.HashCode(this);
}

/// <summary>TemporalBetween, its times a parameter (IPostgresTemporalParameterRoot).</summary>
public sealed class PostgresTemporalBetweenQueryRootExpression(IEntityType entityType, string parameterName)
    : TemporalBetweenQueryRootExpression(entityType, default, default), IPostgresTemporalParameterRoot
{
    public string ParameterName { get; } = parameterName;

    public override DateTime From => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override DateTime To => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override Expression DetachQueryProvider() => this;

    public override EntityQueryRootExpression UpdateEntityType(IEntityType entityType)
        => PostgresTemporalParameterRoots.Update(this, entityType, e => new PostgresTemporalBetweenQueryRootExpression(e, ParameterName));

    protected override void Print(ExpressionPrinter expressionPrinter)
        => PostgresTemporalParameterRoots.Print(this, expressionPrinter, "TemporalBetween");

    public override bool Equals(object? obj) => PostgresTemporalParameterRoots.Equal(this, obj);

    public override int GetHashCode() => PostgresTemporalParameterRoots.HashCode(this);
}

/// <summary>TemporalContainedIn, its times a parameter (IPostgresTemporalParameterRoot).</summary>
public sealed class PostgresTemporalContainedInQueryRootExpression(IEntityType entityType, string parameterName)
    : TemporalContainedInQueryRootExpression(entityType, default, default), IPostgresTemporalParameterRoot
{
    public string ParameterName { get; } = parameterName;

    public override DateTime From => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override DateTime To => throw PostgresTemporalParameterRoots.NoTime(ParameterName);

    public override Expression DetachQueryProvider() => this;

    public override EntityQueryRootExpression UpdateEntityType(IEntityType entityType)
        => PostgresTemporalParameterRoots.Update(this, entityType, e => new PostgresTemporalContainedInQueryRootExpression(e, ParameterName));

    protected override void Print(ExpressionPrinter expressionPrinter)
        => PostgresTemporalParameterRoots.Print(this, expressionPrinter, "TemporalContainedIn");

    public override bool Equals(object? obj) => PostgresTemporalParameterRoots.Equal(this, obj);

    public override int GetHashCode() => PostgresTemporalParameterRoots.HashCode(this);
}

/// <summary>What the four roots with parameters share.</summary>
internal static class PostgresTemporalParameterRoots
{
    public static InvalidOperationException NoTime(string parameter)
        => new($"The times of this temporal query root are the query parameter '{parameter}'.");

    public static EntityQueryRootExpression Update(EntityQueryRootExpression root, IEntityType entityType, Func<IEntityType, EntityQueryRootExpression> create)
        => entityType.ClrType != root.EntityType.ClrType || entityType.Name != root.EntityType.Name
            ? throw new InvalidOperationException(CoreStrings.QueryRootDifferentEntityType(entityType.DisplayName()))
            : create(entityType);

    public static void Print<T>(T root, ExpressionPrinter printer, string operation)
        where T : EntityQueryRootExpression, IPostgresTemporalParameterRoot
        => printer.Append($"DbSet<{root.EntityType.ShortName()}>().{operation}(@{root.ParameterName})");

    public static bool Equal<T>(T root, object? obj)
        where T : EntityQueryRootExpression, IPostgresTemporalParameterRoot
        => ReferenceEquals(root, obj)
            || obj is T other && other.EntityType == root.EntityType && other.ParameterName == root.ParameterName;

    public static int HashCode<T>(T root)
        where T : EntityQueryRootExpression, IPostgresTemporalParameterRoot
        => System.HashCode.Combine(typeof(T), root.EntityType, root.ParameterName);

    /// <summary>
    /// <paramref name="root"/> with its times taken out into <paramref name="parameters"/>, under a name the
    /// query's other roots of the same operation and times share; or itself, if it has none (ALL) or
    /// this doesn't know it.
    /// </summary>
    public static Expression Parameterize(TemporalQueryRootExpression root, Dictionary<string, object?> parameters, Dictionary<string, string> names)
    {
        if (PostgresTemporalQueries.Instants(root) is not { Length: > 0 } instants)
            return root;

        var key = root.GetType().Name + ":" + string.Join(",", instants.Select(i => i.Ticks));
        if (!names.TryGetValue(key, out var name))
        {
            var i = names.Count;
            while (parameters.ContainsKey(name = $"__grif_temporal_{i}"))
                i++;
            names[key] = name;
            parameters[name] = instants.Cast<object>().ToArray();
        }

        return root switch
        {
            TemporalAsOfQueryRootExpression => new PostgresTemporalAsOfQueryRootExpression(root.EntityType, name),
            TemporalFromToQueryRootExpression => new PostgresTemporalFromToQueryRootExpression(root.EntityType, name),
            TemporalBetweenQueryRootExpression => new PostgresTemporalBetweenQueryRootExpression(root.EntityType, name),
            _ => new PostgresTemporalContainedInQueryRootExpression(root.EntityType, name),
        };
    }
}

/// <summary>
/// EF's query compiler, with the times of a query's temporal roots taken out into its parameters
/// (PostgresTemporalQueries), after EF's own: so EF's query cache holds the query once, whatever its
/// times. Each execution has its own parameters, as with any captured value. Not for EF.CompileQuery,
/// whose parameters are its arguments alone: its roots keep their times, which its SQL has as literals.
/// </summary>
public class PostgresTemporalQueryCompiler(
    IQueryContextFactory queryContextFactory,
    ICompiledQueryCache compiledQueryCache,
    ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
    IDatabase database,
    IDiagnosticsLogger<DbLoggerCategory.Query> logger,
    ICurrentDbContext currentContext,
    IEvaluatableExpressionFilter evaluatableExpressionFilter,
    IModel model)
    : QueryCompiler(queryContextFactory, compiledQueryCache, compiledQueryCacheKeyGenerator, database, logger, currentContext, evaluatableExpressionFilter, model)
{
    public override Expression ExtractParameters(
        Expression query,
        Dictionary<string, object?> parameters,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger,
        bool compiledQuery = false,
        bool generateContextAccessors = false)
    {
        query = base.ExtractParameters(query, parameters, logger, compiledQuery, generateContextAccessors);
        return compiledQuery ? query : new TemporalParameterizer(parameters).Visit(query);
    }

    private sealed class TemporalParameterizer(Dictionary<string, object?> parameters) : ExpressionVisitor
    {
        private readonly Dictionary<string, string> _names = [];

        protected override Expression VisitExtension(Expression node)
            => node is TemporalQueryRootExpression root
                ? PostgresTemporalParameterRoots.Parameterize(root, parameters, _names)
                : base.VisitExtension(node);
    }
}
/// <summary>Npgsql's translator, with EF's temporal query roots read from row history (PostgresTemporalQueries).</summary>
public class PostgresTemporalQueryableMethodTranslatingExpressionVisitor : NpgsqlQueryableMethodTranslatingExpressionVisitor
{
    public PostgresTemporalQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext,
        INpgsqlSingletonOptions npgsqlSingletonOptions)
        : base(dependencies, relationalDependencies, queryCompilationContext, npgsqlSingletonOptions)
    {
    }

    protected PostgresTemporalQueryableMethodTranslatingExpressionVisitor(PostgresTemporalQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
    {
    }

    // Subqueries too: an Include or a navigation under an AsOf is one.
    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new PostgresTemporalQueryableMethodTranslatingExpressionVisitor(this);

    protected override Expression VisitExtension(Expression extensionExpression)
    {
        if (extensionExpression is TemporalQueryRootExpression root)
        {
            // A root with parameters: FromSql's arguments are the parameter, an object[] of its times, which
            // EF reads from each execution's parameters (PostgresTemporalQueryCompiler).
            Expression arguments = root is IPostgresTemporalParameterRoot parameterized
                ? new QueryParameterExpression(parameterized.ParameterName, typeof(object[]))
                : Expression.Constant(Array.Empty<object>(), typeof(object[]));
            return base.VisitExtension(new FromSqlQueryRootExpression(root.EntityType, PostgresTemporalQueries.Sql(root), arguments));
        }

        return base.VisitExtension(extensionExpression);
    }
}

public class PostgresTemporalQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlQueryableMethodTranslatingExpressionVisitorFactory(dependencies, relationalDependencies, npgsqlSingletonOptions)
{
    public override QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new PostgresTemporalQueryableMethodTranslatingExpressionVisitor(
            Dependencies, RelationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, npgsqlSingletonOptions);
}

/// <summary>
/// SQL Server's navigation expansion helper (SqlServerNavigationExpansionExtensibilityHelper), on row
/// history: a table has history when PostgresHistory gave it a history table. An AsOf query's
/// navigations and Includes are AsOf the same time; a navigation under another temporal operation, or
/// to an entity without history, is refused; a set operation takes two queries of the same operation
/// and times.
/// </summary>
public class PostgresTemporalNavigationExpansionExtensibilityHelper(NavigationExpansionExtensibilityHelperDependencies dependencies)
    : NavigationExpansionExtensibilityHelper(dependencies)
{
    public override EntityQueryRootExpression CreateQueryRoot(IEntityType entityType, EntityQueryRootExpression? source)
    {
        // Its time the same parameter (PostgresTemporalQueryCompiler).
        if (source is PostgresTemporalAsOfQueryRootExpression parameterized)
        {
            return new PostgresTemporalAsOfQueryRootExpression(entityType, parameterized.ParameterName);
        }

        if (source is TemporalAsOfQueryRootExpression asOf)
        {
            // AsOf is the only temporal operation that can pass the validation
            return source.QueryProvider != null
                ? new TemporalAsOfQueryRootExpression(source.QueryProvider, entityType, asOf.PointInTime)
                : new TemporalAsOfQueryRootExpression(entityType, asOf.PointInTime);
        }

        return base.CreateQueryRoot(entityType, source);
    }

    public override void ValidateQueryRootCreation(IEntityType entityType, EntityQueryRootExpression? source)
    {
        if (source is TemporalQueryRootExpression
            && !entityType.IsMappedToJson()
            && !OwnedEntityMappedToSameTableAsOwner(entityType))
        {
            if (entityType.GetRootType().FindHistoryEntityType() is null)
            {
                throw new InvalidOperationException(
                    SqlServerStrings.TemporalNavigationExpansionBetweenTemporalAndNonTemporal(entityType.DisplayName()));
            }

            if (source is not TemporalAsOfQueryRootExpression)
            {
                throw new InvalidOperationException(SqlServerStrings.TemporalNavigationExpansionOnlySupportedForAsOf("AsOf"));
            }
        }

        base.ValidateQueryRootCreation(entityType, source);
    }

    private static bool OwnedEntityMappedToSameTableAsOwner(IEntityType entityType)
        => entityType.IsOwned()
            && entityType.FindOwnership()!.PrincipalEntityType.GetTableMappings().FirstOrDefault()?.Table is { } ownerTable
            && entityType.GetTableMappings().FirstOrDefault()?.Table is { } entityTable
            && ownerTable == entityTable;

    public override bool AreQueryRootsCompatible(EntityQueryRootExpression? first, EntityQueryRootExpression? second)
    {
        if (!base.AreQueryRootsCompatible(first, second))
        {
            return false;
        }

        // Roots whose times are a parameter (PostgresTemporalQueryCompiler): the same operation and times
        // share one.
        if (first is IPostgresTemporalParameterRoot || second is IPostgresTemporalParameterRoot)
        {
            return first is IPostgresTemporalParameterRoot firstParameterized
                && second is IPostgresTemporalParameterRoot secondParameterized
                && first.GetType() == second.GetType()
                && firstParameterized.ParameterName == secondParameterized.ParameterName
                    ? true
                    : throw new InvalidOperationException(SqlServerStrings.TemporalSetOperationOnMismatchedSources((first ?? second)!.EntityType.DisplayName()));
        }

        var firstTemporal = first is TemporalQueryRootExpression;
        var secondTemporal = second is TemporalQueryRootExpression;

        if (firstTemporal && secondTemporal)
        {
            if (first is TemporalAsOfQueryRootExpression firstAsOf
                && second is TemporalAsOfQueryRootExpression secondAsOf
                && firstAsOf.PointInTime == secondAsOf.PointInTime)
            {
                return true;
            }

            if (first is TemporalAllQueryRootExpression
                && second is TemporalAllQueryRootExpression)
            {
                return true;
            }

            if (first is TemporalRangeQueryRootExpression firstRange
                && second is TemporalRangeQueryRootExpression secondRange
                && firstRange.From == secondRange.From
                && firstRange.To == secondRange.To)
            {
                return true;
            }
        }

        if (firstTemporal || secondTemporal)
        {
            var entityType = first?.EntityType ?? second?.EntityType;

            throw new InvalidOperationException(SqlServerStrings.TemporalSetOperationOnMismatchedSources(entityType!.DisplayName()));
        }

        return true;
    }
}
