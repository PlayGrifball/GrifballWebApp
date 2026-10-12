using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

// EF's temporal query roots, its navigation expansion helper and Npgsql's translator are internal API
// ("pubternal"): what EF's own SQL Server provider builds its temporal queries on.
#pragma warning disable EF1001

namespace GrifballWebApp.Database;

/// <summary>
/// EF's temporal queries on Postgres, over row history's tables (RowHistory): the same calls as on SQL
/// Server - context.Matches.TemporalAsOf(t), TemporalAll(), TemporalFromTo(a, b), TemporalBetween(a, b),
/// TemporalContainedIn(a, b) - with the same results. EF's TemporalXxx methods work on any provider: each
/// wraps the DbSet's query root in a TemporalQueryRootExpression, which SQL Server translates to FOR
/// SYSTEM_TIME and no other provider translates at all. On Postgres the translator (registered by
/// UseGrifballDatabase) turns each into FromSql over the table and its history table (<see cref="Sql"/>),
/// which EF then composes as it would any FromSql: filters, ordering, projections, joins, Includes, set
/// operations. The navigation helper carries an AsOf to the navigations and Includes of the query, and
/// refuses what SQL Server refuses, with its messages.
/// </summary>
public static class PostgresTemporalQueries
{
    /// <summary>
    /// The versions <paramref name="root"/> asks for, every column of the table, PeriodStart and
    /// PeriodEnd included, by SQL Server's rules: AS OF t, PeriodStart &lt;= t AND PeriodEnd &gt; t; FROM a
    /// TO b, PeriodStart &lt; b AND PeriodEnd &gt; a; BETWEEN a AND b, PeriodStart &lt;= b AND PeriodEnd &gt; a;
    /// CONTAINED IN (a, b), PeriodStart &gt;= a AND PeriodEnd &lt;= b; ALL, every version. A history row's
    /// periods are its Valid's bounds, lower("Valid") and upper("Valid"). Like FOR SYSTEM_TIME, it leaves
    /// out versions that lasted no time (PeriodStart = PeriodEnd), though RowHistoryInterceptor writes
    /// none: each SaveChanges has its own time, and a version gets at least a microsecond.
    /// </summary>
    public static string Sql(TemporalQueryRootExpression root)
    {
        var entityType = root.EntityType;
        var history = entityType.GetRootType().FindHistory()
            ?? throw new InvalidOperationException(
                $"'{entityType.DisplayName()}' has no history: its table '{entityType.GetSchemaQualifiedTableName()}' isn't temporal, so it has no temporal queries.");

        string? Condition(string start, string end) => root switch
        {
            TemporalAsOfQueryRootExpression asOf => $"{start} <= {Literal(asOf.PointInTime)} AND {end} > {Literal(asOf.PointInTime)}",
            TemporalFromToQueryRootExpression fromTo => $"{start} < {Literal(fromTo.To, roundUp: true)} AND {end} > {Literal(fromTo.From)}",
            TemporalBetweenQueryRootExpression between => $"{start} <= {Literal(between.To)} AND {end} > {Literal(between.From)}",
            TemporalContainedInQueryRootExpression containedIn => $"{start} >= {Literal(containedIn.From, roundUp: true)} AND {end} <= {Literal(containedIn.To)}",
            TemporalAllQueryRootExpression => null,
            _ => throw new InvalidOperationException($"Unknown temporal operation {root.GetType().Name}."),
        };

        var table = entityType.GetTableMappings().First().Table;
        var tableCondition = Condition(Quote(RowHistory.PeriodStart), Quote(RowHistory.PeriodEnd));
        var sql = new StringBuilder()
            .Append("SELECT ").AppendJoin(", ", table.Columns.Select(c => Quote(c.Name)))
            .Append(" FROM ").Append(Name(table.Schema, table.Name));
        if (tableCondition is not null)
            sql.Append(" WHERE ").Append(tableCondition);

        // The same columns from the history table, the periods from Valid.
        var start = $"lower({Quote(RowHistory.Valid)})";
        var end = $"upper({Quote(RowHistory.Valid)})";
        var historyCondition = Condition(start, end);
        sql.Append(" UNION ALL SELECT ").AppendJoin(", ", table.Columns.Select(c => c.Name switch
            {
                RowHistory.PeriodStart => $"{start} AS {Quote(c.Name)}",
                RowHistory.PeriodEnd => $"{end} AS {Quote(c.Name)}",
                _ => Quote(c.Name),
            }))
            .Append(" FROM ").Append(Name(history.GetSchema(), history.GetTableName()!))
            .Append(" WHERE ");
        if (historyCondition is not null)
            sql.Append(historyCondition).Append(" AND ");
        sql.Append(start).Append(" < ").Append(end);
        return sql.ToString();
    }

    /// <summary>
    /// <paramref name="value"/> as a timestamptz literal: written here, not a parameter, as SQL Server's
    /// are (EF caches each query with its time). UTC, Unspecified taken as UTC, Local refused, as every
    /// DateTime the app stores (UtcDateTimeConverter). Postgres keeps microseconds and SQL Server 100ns:
    /// a time between two microseconds is rounded down, or up where the comparison is PeriodStart &lt; or
    /// PeriodStart &gt;=, so each comparison with a period column, which holds whole microseconds, gives
    /// what it would with the exact time. DateTime.MinValue and MaxValue are -infinity and infinity, as
    /// Npgsql writes them: MaxValue is the PeriodEnd of the current rows.
    /// </summary>
    public static string Literal(DateTime value, bool roundUp = false)
    {
        value = UtcDateTimeConverter.ToUtc(value);
        if (value == DateTime.MaxValue)
            return "'infinity'::timestamptz";
        if (value == DateTime.MinValue)
            return "'-infinity'::timestamptz";

        var ticks = value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond;
        if (roundUp && ticks != value.Ticks)
        {
            // Past the last microsecond DateTime has: after every time Postgres holds bar infinity.
            if (ticks > DateTime.MaxValue.Ticks - TimeSpan.TicksPerMicrosecond)
                return "'infinity'::timestamptz";
            ticks += TimeSpan.TicksPerMicrosecond;
        }
        return "'" + new DateTime(ticks).ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "+00'::timestamptz";
    }

    private static string Name(string? schema, string name) => schema is null ? Quote(name) : $"{Quote(schema)}.{Quote(name)}";

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
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
            return base.VisitExtension(new FromSqlQueryRootExpression(
                root.EntityType, PostgresTemporalQueries.Sql(root), Expression.Constant(Array.Empty<object>(), typeof(object[]))));
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
/// history: a table has history when RowHistory gave it a history table (FindHistory). An AsOf query's
/// navigations and Includes are AsOf the same time; a navigation under another temporal operation, or
/// to an entity without history, is refused; a set operation takes two queries of the same operation
/// and times.
/// </summary>
public class PostgresTemporalNavigationExpansionExtensibilityHelper(NavigationExpansionExtensibilityHelperDependencies dependencies)
    : NavigationExpansionExtensibilityHelper(dependencies)
{
    public override EntityQueryRootExpression CreateQueryRoot(IEntityType entityType, EntityQueryRootExpression? source)
    {
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
            if (entityType.GetRootType().FindHistory() is null)
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
