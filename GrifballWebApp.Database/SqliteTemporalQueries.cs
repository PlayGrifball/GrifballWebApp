using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

// EF's temporal query roots, its query compiler and SQLite's translator are internal API, as on Postgres
// (PostgresTemporalQueries).
#pragma warning disable EF1001

namespace GrifballWebApp.Database;

/// <summary>
/// How a database's SQL names the tables of a temporal query and writes its times
/// (PostgresTemporalQueries.Sql): Postgres's, or SQLite's. The query itself, the period conditions and the
/// parameters are the same on both.
/// </summary>
public abstract class TemporalSql
{
    public static readonly TemporalSql Postgres = new PostgresTemporalSql();
    public static readonly TemporalSql Sqlite = new SqliteTemporalSql();

    /// <summary>A table, by its schema and name.</summary>
    public abstract string Name(string? schema, string name);

    /// <summary>A time as the database compares it with a period column; rounded up for PeriodStart &lt; or &gt;=.</summary>
    public abstract DateTime Instant(DateTime value, bool roundUp = false);

    /// <summary>A time as a literal, in the SQL of a query EF compiles for its times alone (EF.CompileQuery).</summary>
    public abstract string Literal(DateTime value, bool roundUp = false);

    /// <summary>The times of <paramref name="root"/>, as its condition compares them (PostgresTemporalQueries.Instants).</summary>
    public DateTime[]? Instants(TemporalQueryRootExpression root) => PostgresTemporalQueries.Instants(root, (t, up) => Instant(t, up));

    protected static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private sealed class PostgresTemporalSql : TemporalSql
    {
        public override string Name(string? schema, string name) => schema is null ? Quote(name) : $"{Quote(schema)}.{Quote(name)}";

        public override DateTime Instant(DateTime value, bool roundUp = false) => PostgresTemporalQueries.Instant(value, roundUp);

        public override string Literal(DateTime value, bool roundUp = false) => PostgresTemporalQueries.Literal(value, roundUp);
    }

    /// <summary>
    /// SQLite: no schemas, so a table by its name alone; times as EF stores them there, text it compares as
    /// times ("yyyy-MM-dd HH:mm:ss.FFFFFFF"), to the 100ns of a DateTime, so nothing is rounded. DateTime.MaxValue
    /// is the PeriodEnd of the current rows (SqliteHistory.EndOfTime).
    /// </summary>
    private sealed class SqliteTemporalSql : TemporalSql
    {
        public override string Name(string? schema, string name) => Quote(name);

        public override DateTime Instant(DateTime value, bool roundUp = false) => UtcDateTimeConverter.ToUtc(value);

        public override string Literal(DateTime value, bool roundUp = false)
            => "'" + Instant(value).ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture) + "'";
    }
}

/// <summary>
/// EF's temporal queries on SQLite, over row history's tables (SqliteHistory), as on Postgres
/// (PostgresTemporalQueries): the same roots, read as FromSql over a table and its history table, their times
/// parameters, AsOf carried to navigations and Includes (PostgresTemporalNavigationExpansionExtensibilityHelper).
/// History's times are SQLite's clock's, to the millisecond: a write and a time within the same millisecond
/// can fall either side of each other.
/// </summary>
public class SqliteTemporalQueryCompiler(
    IQueryContextFactory queryContextFactory,
    ICompiledQueryCache compiledQueryCache,
    ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
    IDatabase database,
    IDiagnosticsLogger<DbLoggerCategory.Query> logger,
    ICurrentDbContext currentContext,
    IEvaluatableExpressionFilter evaluatableExpressionFilter,
    IModel model)
    : PostgresTemporalQueryCompiler(queryContextFactory, compiledQueryCache, compiledQueryCacheKeyGenerator, database, logger, currentContext, evaluatableExpressionFilter, model)
{
    protected override TemporalSql Dialect => TemporalSql.Sqlite;
}

/// <summary>SQLite's translator, with EF's temporal query roots read from row history (SqliteTemporalQueryCompiler).</summary>
public class SqliteTemporalQueryableMethodTranslatingExpressionVisitor : SqliteQueryableMethodTranslatingExpressionVisitor
{
    public SqliteTemporalQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext)
        : base(dependencies, relationalDependencies, queryCompilationContext)
    {
    }

    protected SqliteTemporalQueryableMethodTranslatingExpressionVisitor(SqliteTemporalQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
    {
    }

    // Subqueries too: an Include or a navigation under an AsOf is one.
    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new SqliteTemporalQueryableMethodTranslatingExpressionVisitor(this);

    protected override Expression VisitExtension(Expression extensionExpression)
        => base.VisitExtension(extensionExpression is TemporalQueryRootExpression root
            ? PostgresTemporalQueries.FromSql(root, TemporalSql.Sqlite)
            : extensionExpression);
}

public class SqliteTemporalQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies)
    : SqliteQueryableMethodTranslatingExpressionVisitorFactory(dependencies, relationalDependencies)
{
    public override QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new SqliteTemporalQueryableMethodTranslatingExpressionVisitor(
            Dependencies, RelationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext);
}
