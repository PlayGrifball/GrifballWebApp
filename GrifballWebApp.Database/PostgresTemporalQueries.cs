using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

// EF's temporal query roots, its navigation expansion helper and Npgsql's translator are internal API
// ("pubternal"): what EF's own SQL Server provider builds its temporal queries on.
#pragma warning disable EF1001

namespace GrifballWebApp.Database;

/// <summary>
/// EF's temporal queries on Postgres, over row history (RowHistory: the periods extension's SYSTEM
/// VERSIONING): the same calls as on SQL Server - context.Matches.TemporalAsOf(t), TemporalAll(),
/// TemporalFromTo(a, b), TemporalBetween(a, b), TemporalContainedIn(a, b) - with the same results. EF's
/// TemporalXxx methods work on any provider: each wraps the DbSet's query root in a
/// TemporalQueryRootExpression, which SQL Server translates to FOR SYSTEM_TIME and no other provider
/// translates at all. On Postgres the translator (registered by UseGrifballDatabase) turns each into
/// FromSql over the table and its history (<see cref="Sql"/>), which EF then composes as it would any
/// FromSql: filters, ordering, projections, joins, Includes, set operations. The navigation helper
/// carries an AsOf to the navigations and Includes of the query, and refuses what SQL Server refuses,
/// with its messages. A database without the extension has no history to read: the query fails on the
/// missing view, as SQL Server's FOR SYSTEM_TIME fails on a table that isn't temporal, with an
/// InvalidOperationException that says so (<see cref="NoHistoryException"/>), never an empty result.
/// </summary>
public static class PostgresTemporalQueries
{
    /// <summary>
    /// The versions <paramref name="root"/> asks for, every column of the table, PeriodStart and
    /// PeriodEnd included, by SQL Server's rules: AS OF t, PeriodStart &lt;= t AND PeriodEnd &gt; t; FROM a
    /// TO b, PeriodStart &lt; b AND PeriodEnd &gt; a; BETWEEN a AND b, PeriodStart &lt;= b AND PeriodEnd &gt; a;
    /// CONTAINED IN (a, b), PeriodStart &gt;= a AND PeriodEnd &lt;= b; ALL, every version. Read from periods'
    /// view of the table and its history table ("Schema"."Table_with_history", UNION ALL of the two, the
    /// same columns). Like FOR SYSTEM_TIME, no version that lasted no time: periods writes none (a row
    /// changed again in the transaction that changed it keeps its PeriodStart, and nothing goes to
    /// history), and the table's own rows have PeriodStart &lt; PeriodEnd (its CHECK constraint).
    /// </summary>
    public static string Sql(TemporalQueryRootExpression root)
    {
        var entityType = root.EntityType;
        if (!entityType.HasSystemTimePeriod())
        {
            throw new InvalidOperationException(
                $"'{entityType.DisplayName()}' has no history: its table '{entityType.GetSchemaQualifiedTableName()}' isn't temporal, so it has no temporal queries.");
        }

        const string start = $"\"{RowHistory.PeriodStart}\"";
        const string end = $"\"{RowHistory.PeriodEnd}\"";
        var condition = root switch
        {
            TemporalAsOfQueryRootExpression asOf => $"{start} <= {Literal(asOf.PointInTime)} AND {end} > {Literal(asOf.PointInTime)}",
            TemporalFromToQueryRootExpression fromTo => $"{start} < {Literal(fromTo.To, roundUp: true)} AND {end} > {Literal(fromTo.From)}",
            TemporalBetweenQueryRootExpression between => $"{start} <= {Literal(between.To)} AND {end} > {Literal(between.From)}",
            TemporalContainedInQueryRootExpression containedIn => $"{start} >= {Literal(containedIn.From, roundUp: true)} AND {end} <= {Literal(containedIn.To)}",
            TemporalAllQueryRootExpression => null,
            _ => throw new InvalidOperationException($"Unknown temporal operation {root.GetType().Name}."),
        };

        var table = entityType.GetTableMappings().First().Table;
        var columns = string.Join(", ", table.Columns.Select(c => Quote(c.Name)));
        var sql = new StringBuilder()
            .Append("SELECT ").Append(columns).Append(" FROM ").Append(Name(table.Schema, RowHistory.PeriodsName(table.Name, WithHistory)));
        if (condition is not null)
            sql.Append(" WHERE ").Append(condition);
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

    /// <summary>The end of the name of every periods view a temporal query reads (<see cref="Sql"/>).</summary>
    private const string WithHistory = "with_history";

    internal const string ViewNameEnd = "_" + WithHistory + "\"";

    /// <summary>What a temporal query throws on a database without row history, the database's error inside.</summary>
    public static InvalidOperationException NoHistoryException(PostgresException missingView) => new(
        "Row history isn't enabled on this PostgreSQL database: the periods extension isn't installed, or doesn't "
        + "version this table (readme.md, Row history; the Helm chart's database.history.enabled). " + missingView.MessageText + ".",
        missingView);

    /// <summary>
    /// Whether <paramref name="exception"/>, from <paramref name="commandText"/>, is a missing periods view:
    /// undefined_table (42P01) at a name ending _with_history - what the database answers a temporal query
    /// when it has no row history. Read from the command at the error's position (the name's start), not
    /// from the message, which the server may translate; anywhere in the command if it gives none.
    /// </summary>
    public static bool IsMissingHistoryView(PostgresException exception, string commandText)
    {
        if (exception.SqlState != PostgresErrorCodes.UndefinedTable)
            return false;
        if (exception.Position <= 0 || exception.Position > commandText.Length)
            return commandText.Contains(ViewNameEnd, StringComparison.Ordinal);
        // "View" or "Schema"."View": the identifiers are the app's table names, quoted, without spaces.
        var name = commandText.AsSpan(exception.Position - 1);
        var end = name.IndexOfAny(" \t\r\n),");
        return (end < 0 ? name : name[..end]).EndsWith(ViewNameEnd, StringComparison.Ordinal);
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
/// EF's command, with a temporal query on a database without row history failing with
/// <see cref="PostgresTemporalQueries.NoHistoryException"/> rather than a bare undefined_table. Every query
/// runs with a command EF rents from the connection (built empty, here, and filled per query). On any
/// other query this costs the call; the check runs only once one has failed, after EF has logged the
/// error and released the connection.
/// </summary>
public class PostgresTemporalRelationalCommand(
    RelationalCommandBuilderDependencies dependencies, string commandText, string logCommandText, IReadOnlyList<IRelationalParameter> parameters)
    : RelationalCommand(dependencies, commandText, logCommandText, parameters)
{
    public override RelationalDataReader ExecuteReader(RelationalCommandParameterObject parameterObject)
    {
        try
        {
            return base.ExecuteReader(parameterObject);
        }
        catch (PostgresException e) when (PostgresTemporalQueries.IsMissingHistoryView(e, CommandText))
        {
            throw PostgresTemporalQueries.NoHistoryException(e);
        }
    }

    public override async Task<RelationalDataReader> ExecuteReaderAsync(
        RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.ExecuteReaderAsync(parameterObject, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException e) when (PostgresTemporalQueries.IsMissingHistoryView(e, CommandText))
        {
            throw PostgresTemporalQueries.NoHistoryException(e);
        }
    }
}

/// <summary>
/// EF's command builder, building a <see cref="PostgresTemporalRelationalCommand"/> wherever a query may
/// run: an empty command (the connection's, which queries fill) or one that reads a periods view. Any
/// other - SaveChanges', raw SQL's - stays EF's own.
/// </summary>
public class PostgresTemporalRelationalCommandBuilder(RelationalCommandBuilderDependencies dependencies)
    : RelationalCommandBuilder(dependencies)
{
    public override IRelationalCommand Build()
    {
        var command = base.Build();
        return command.CommandText.Length == 0 || command.CommandText.Contains(PostgresTemporalQueries.ViewNameEnd, StringComparison.Ordinal)
            ? new PostgresTemporalRelationalCommand(Dependencies, command.CommandText, command.LogCommandText, command.Parameters)
            : command;
    }
}

/// <summary>Builds <see cref="PostgresTemporalRelationalCommandBuilder"/>s (registered by UseGrifballDatabase).</summary>
public class PostgresTemporalRelationalCommandBuilderFactory(RelationalCommandBuilderDependencies dependencies)
    : RelationalCommandBuilderFactory(dependencies)
{
    public override IRelationalCommandBuilder Create() => new PostgresTemporalRelationalCommandBuilder(Dependencies);
}

/// <summary>
/// SQL Server's navigation expansion helper (SqlServerNavigationExpansionExtensibilityHelper), on row
/// history: a table has history when it has the period (RowHistory.HasSystemTimePeriod), as every table
/// of the model has on Postgres - the tables the RowHistory migration, the migrations SQL generator and
/// the Helm chart's history step version. An AsOf query's navigations and Includes are AsOf the same
/// time; a navigation under another temporal operation, or to an entity without history, is refused; a set operation takes two queries of the same operation
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
            if (!entityType.HasSystemTimePeriod())
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
