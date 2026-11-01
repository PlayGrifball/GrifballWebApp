using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;

namespace GrifballWebApp.Database;

/// <summary>
/// SQLite's method translations, with string.Contains case-insensitive as on the other servers. EF translates it
/// to instr(), which compares bytes whatever the column's collation (NOCASE), so a search for "carl" missed
/// "Carlos". Here both sides are upper-cased first, folding ASCII letters as NOCASE does. An empty search
/// matches everything, as EF's own translation has it.
/// </summary>
#pragma warning disable EF1001 // SqliteMethodCallTranslatorProvider: the provider's own translations, extended
public sealed class SqliteMethodCallTranslatorProviderWithContains : SqliteMethodCallTranslatorProvider
{
    public SqliteMethodCallTranslatorProviderWithContains(RelationalMethodCallTranslatorProviderDependencies dependencies)
        : base(dependencies)
    {
        AddTranslators([new CaseInsensitiveContains(dependencies.SqlExpressionFactory)]);
    }
}
#pragma warning restore EF1001

internal sealed class CaseInsensitiveContains(ISqlExpressionFactory sql) : IMethodCallTranslator
{
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    public SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || method != Contains)
            return null;

        var pattern = arguments[0];
        var mapping = ExpressionExtensions.InferTypeMapping(instance, pattern);
        instance = sql.ApplyTypeMapping(instance, mapping);
        pattern = sql.ApplyTypeMapping(pattern, mapping);

        SqlExpression Upper(SqlExpression e) => sql.Function("upper", [e], nullable: true, argumentsPropagateNullability: [true], typeof(string), mapping);

        return sql.OrElse(
            sql.Equal(pattern, sql.Constant(string.Empty, mapping)),
            sql.GreaterThan(
                sql.Function("instr", [Upper(instance), Upper(pattern)], nullable: true, argumentsPropagateNullability: [true, true], typeof(int)),
                sql.Constant(0)));
    }
}
