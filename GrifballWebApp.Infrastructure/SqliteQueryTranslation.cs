using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;

namespace GrifballWebApp.Infrastructure;

/// <summary>
/// SQLite's query postprocessing (SqliteQueryTranslationPostprocessor's: its type mappings, and no APPLY), with
/// two more steps, so the app's queries run there as written:
/// <list type="bullet">
/// <item>APPLY, which SQLite doesn't have, turned into joins where it's a join in all but name
/// (<see cref="SqliteApplyRewriter"/>). Any APPLY left still fails, with SQLite's own message.</item>
/// <item>Text compared case-insensitively where SQLite would compare bytes (<see cref="SqliteNoCaseComparisons"/>).</item>
/// </list>
/// </summary>
public sealed class SqliteQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies) : IQueryTranslationPostprocessorFactory
{
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new SqliteQueryTranslationPostprocessorWithJoins(dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext);
}

public sealed class SqliteQueryTranslationPostprocessorWithJoins(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    RelationalQueryCompilationContext queryCompilationContext)
    : RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
    /// <summary>SqliteQueryTranslationPostprocessor's message, for an APPLY that isn't a join.</summary>
    internal const string ApplyNotSupported = "Translating this query requires the SQL APPLY operation, which is not supported on SQLite.";

    public override Expression Process(Expression query)
    {
        var result = base.Process(query);
        result = new SqliteApplyRewriter(RelationalDependencies.SqlExpressionFactory).Visit(result);
        result = new SqliteNoCaseComparisons().Visit(result);
        new ApplyValidatingVisitor().Visit(result);
        return result;
    }

#pragma warning disable EF1001 // SQLite's own type mapping postprocessing, as its postprocessor has it
    protected override Expression ProcessTypeMappings(Expression expression)
        => new SqliteTypeMappingPostprocessor(Dependencies, RelationalDependencies, RelationalQueryCompilationContext).Process(expression);
#pragma warning restore EF1001

    private sealed class ApplyValidatingVisitor : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            if (node is ShapedQueryExpression shaped)
            {
                Visit(shaped.QueryExpression);
                Visit(shaped.ShaperExpression);
                return node;
            }
            if (node is CrossApplyExpression or OuterApplyExpression)
                throw new InvalidOperationException(ApplyNotSupported);
            return base.VisitExtension(node);
        }
    }
}

/// <summary>
/// APPLY joins each outer row to a table expression that may refer to it. Two kinds the app's queries make are
/// joins in all but name, and become them:
/// <list type="bullet">
/// <item>APPLY of a table: the table refers to nothing, so it's a CROSS JOIN (OUTER APPLY: a LEFT JOIN on 1 = 1). A
/// list of every role with whether the user has each makes one, the outer reference in the SELECT list, which
/// SQLite evaluates per row.</item>
/// <item>APPLY of a subquery that refers to the outer row only by equalities in its WHERE (a navigation's
/// foreign key, outer.Key = inner.ForeignKey, and outer.Key IS NOT NULL), with no LIMIT, OFFSET, GROUP BY or
/// ORDER BY: the inner side of each equality joins its projection, and the equalities make the join's ON
/// (CROSS APPLY an INNER JOIN, OUTER APPLY a LEFT JOIN). A DISTINCT stays: with the key in its projection, its
/// rows for one outer row are the ones it had. A navigation's Select(...).Distinct() makes one.</item>
/// </list>
/// Anything else is left as it is, for SQLite's check to refuse.
/// </summary>
internal sealed class SqliteApplyRewriter(ISqlExpressionFactory sql) : ExpressionVisitor
{
    protected override Expression VisitExtension(Expression node)
    {
        if (node is ShapedQueryExpression shaped)
            return shaped.Update(Visit(shaped.QueryExpression), Visit(shaped.ShaperExpression));
        var visited = base.VisitExtension(node);
        return visited is SelectExpression select ? Rewrite(select) : visited;
    }

    private SelectExpression Rewrite(SelectExpression select)
    {
        if (!select.Tables.Any(t => t is CrossApplyExpression or OuterApplyExpression))
            return select;

        var outer = select.Tables.Select(Unwrap).Select(t => t.Alias).OfType<string>().ToHashSet();
        var tables = select.Tables.Select(table => table switch
        {
            CrossApplyExpression { Table: TableExpression t } => new CrossJoinExpression(t),
            // ON 1 = 1: EF processes a join predicate as a comparison
            OuterApplyExpression { Table: TableExpression t } => new LeftJoinExpression(t, sql.Equal(sql.Constant(1), sql.Constant(1)), prunable: false),
            CrossApplyExpression { Table: SelectExpression inner } when Decorrelate(inner, outer) is var (joined, on) && joined is not null
                => new InnerJoinExpression(joined, on!, prunable: false),
            OuterApplyExpression { Table: SelectExpression inner } when Decorrelate(inner, outer) is var (joined, on) && joined is not null
                => new LeftJoinExpression(joined, on!, prunable: false),
            _ => table,
        }).ToList();

        return select.Update(tables, select.Predicate, select.GroupBy, select.Having, select.Projection, select.Orderings, select.Offset, select.Limit);
    }

    private static TableExpressionBase Unwrap(TableExpressionBase table) => table is JoinExpressionBase join ? join.Table : table;

    /// <summary>
    /// <paramref name="inner"/> without its references to <paramref name="outer"/>'s tables, the inner side of each
    /// equality projected, and the ON that joins it; nulls when it refers to them otherwise.
    /// </summary>
    private (SelectExpression? Joined, SqlExpression? On) Decorrelate(SelectExpression inner, HashSet<string> outer)
    {
        if (inner.Alias is null || inner.Limit is not null || inner.Offset is not null || inner.GroupBy.Count > 0
            || inner.Having is not null || inner.Orderings.Count > 0
            || inner.Projection.Any(p => References(p.Expression, outer)) || inner.Tables.Any(t => References(t, outer)))
            return (null, null);

        var kept = new List<SqlExpression>();
        var pairs = new List<(ColumnExpression Outer, SqlExpression Inner)>();
        foreach (var term in Conjuncts(inner.Predicate))
        {
            if (!References(term, outer))
                kept.Add(term);
            else if (term is SqlBinaryExpression { OperatorType: ExpressionType.NotEqual, Left: ColumnExpression notNull, Right: SqlConstantExpression { Value: null } }
                && outer.Contains(notNull.TableAlias))
                continue; // outer.Key IS NOT NULL: the join's equality needs it already
            // outer.Key = inner.ForeignKey (EF's navigation join), or inner side first as a query may write it
            else if (term is SqlBinaryExpression { OperatorType: ExpressionType.Equal } equal && Correlation(equal, outer) is { } pair)
                pairs.Add(pair);
            else
                return (null, null);
        }
        if (pairs.Count == 0)
            return (null, null);

        // The keys projected under names of their own: EF names its projections after their members or c, c0...
        var projections = inner.Projection.ToList();
        var on = new List<SqlExpression>();
        foreach (var (outerColumn, innerSide) in pairs)
        {
            var name = $"_key{on.Count}";
            projections.Add(new ProjectionExpression(innerSide, name));
            on.Add(sql.Equal(outerColumn, new ColumnExpression(name, inner.Alias, innerSide.Type, innerSide.TypeMapping, nullable: true)));
        }

        var predicate = kept.Count == 0 ? null : kept.Aggregate(sql.AndAlso);
        var joined = inner.Update(inner.Tables, predicate, inner.GroupBy, inner.Having, projections, inner.Orderings, inner.Offset, inner.Limit);
        return (joined, on.Aggregate(sql.AndAlso));
    }

    private static (ColumnExpression, SqlExpression)? Correlation(SqlBinaryExpression equal, HashSet<string> outer) => (equal.Left, equal.Right) switch
    {
        (ColumnExpression key, var inner) when outer.Contains(key.TableAlias) && !References(inner, outer) => (key, inner),
        (var inner, ColumnExpression key) when outer.Contains(key.TableAlias) && !References(inner, outer) => (key, inner),
        _ => null,
    };

    private static IEnumerable<SqlExpression> Conjuncts(SqlExpression? predicate)
    {
        if (predicate is SqlBinaryExpression { OperatorType: ExpressionType.AndAlso } and)
            return Conjuncts(and.Left).Concat(Conjuncts(and.Right));
        return predicate is null ? [] : [predicate];
    }

    private static bool References(Expression expression, HashSet<string> outer)
    {
        var finder = new OuterColumnFinder(outer);
        finder.Visit(expression);
        return finder.Found;
    }

    private sealed class OuterColumnFinder(HashSet<string> outer) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitExtension(Expression node)
        {
            if (Found)
                return node;
            if (node is ColumnExpression column && outer.Contains(column.TableAlias))
            {
                Found = true;
                return node;
            }
            return base.VisitExtension(node);
        }
    }
}

/// <summary>
/// SQLite compares text by the collation of a column in the comparison (NOCASE, GrifballContext), and by bytes
/// when neither side is a column: a function's result, as in Gamertag.Replace(" ", "") == name, compared
/// case-sensitively there, where the other databases compare case-insensitively. Such comparisons get NOCASE
/// themselves.
/// </summary>
internal sealed class SqliteNoCaseComparisons : ExpressionVisitor
{
    private static readonly HashSet<ExpressionType> Comparisons =
    [
        ExpressionType.Equal, ExpressionType.NotEqual, ExpressionType.LessThan, ExpressionType.LessThanOrEqual,
        ExpressionType.GreaterThan, ExpressionType.GreaterThanOrEqual,
    ];

    protected override Expression VisitExtension(Expression node)
    {
        if (node is ShapedQueryExpression shaped)
            return shaped.Update(Visit(shaped.QueryExpression), Visit(shaped.ShaperExpression));
        var visited = base.VisitExtension(node);
        if (visited is SqlBinaryExpression binary && Comparisons.Contains(binary.OperatorType)
            && binary.Left.Type == typeof(string) && binary.Right.Type == typeof(string)
            && !HasCollation(binary.Left) && !HasCollation(binary.Right)
            && binary.Right is not SqlConstantExpression { Value: null } && binary.Left is not SqlConstantExpression { Value: null })
            return binary.Update(new CollateExpression(binary.Left, GrifballContext.SqliteCaseInsensitiveCollation), binary.Right);
        return visited;
    }

    private static bool HasCollation(SqlExpression expression) => expression is ColumnExpression or CollateExpression;
}
