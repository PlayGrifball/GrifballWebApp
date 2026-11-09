using System.Linq.Expressions;
using System.Reflection;

namespace GrifballWebApp.Server.Extensions;

/// <summary>Composing projections the database translates as one, where C# would otherwise copy a base DTO's fields or a
/// model's rule.</summary>
public static class Projections
{
    private static readonly MethodInfo ApplyMethod = typeof(Projections).GetMethod(nameof(Apply))!;

    /// <summary>
    /// The projection of a DTO that derives from <typeparamref name="TBase"/>: the members <paramref name="extra"/> sets (a
    /// member initializer, e.g. <c>s =&gt; new SeasonListItemDto { TeamCount = s.Teams.Count }</c>) and the base's from
    /// <paramref name="baseFields"/> (a member initializer too), as one expression. A member both set is <paramref name="extra"/>'s.
    /// The model's rules either uses (<see cref="Apply"/>) are inlined.
    /// </summary>
    public static Expression<Func<TSource, T>> Extend<TSource, TBase, T>(Expression<Func<TSource, TBase>> baseFields, Expression<Func<TSource, T>> extra)
        where T : TBase
    {
        var (newT, bindings) = extra.Body switch
        {
            MemberInitExpression init => (init.NewExpression, (IReadOnlyCollection<MemberBinding>)init.Bindings),
            NewExpression created => (created, []),
            _ => throw new ArgumentException("Expected a member initializer.", nameof(extra)),
        };
        if (baseFields.Body is not MemberInitExpression)
            throw new ArgumentException("Expected a member initializer.", nameof(baseFields));
        var parameter = extra.Parameters[0];
        var set = bindings.Select(b => b.Member.Name).ToHashSet();
        var fields = ((MemberInitExpression)new ReplaceParameter(baseFields.Parameters[0], parameter).Visit(baseFields.Body)).Bindings
            .Where(b => !set.Contains(b.Member.Name));
        return Inline(Expression.Lambda<Func<TSource, T>>(Expression.MemberInit(newT, fields.Concat(bindings)), parameter));
    }

    /// <summary>
    /// A model's rule (an expression of it for queries, e.g. <c>Team.HasArchivedRecord</c>) applied to
    /// <paramref name="value"/> inside a projection: <see cref="Inline"/> puts the rule's own expression in its place, so
    /// the database translates it and the rule stays in one place. Outside a query it runs the rule.
    /// </summary>
    public static TResult Apply<T, TResult>(this T value, Expression<Func<T, TResult>> rule) => rule.Compile()(value);

    /// <summary>The projection with each rule it <see cref="Apply"/>s inlined: what the database is given.</summary>
    public static Expression<Func<TSource, T>> Inline<TSource, T>(Expression<Func<TSource, T>> projection)
        => (Expression<Func<TSource, T>>)new InlineRules().Visit(projection);

    private sealed class InlineRules : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod || node.Method.GetGenericMethodDefinition() != ApplyMethod)
                return base.VisitMethodCall(node);
            // The rule is the model's (a field, or a method of the request's values): evaluated here, once per projection
            var rule = (LambdaExpression)Expression.Lambda<Func<object>>(Expression.Convert(node.Arguments[1], typeof(object))).Compile(preferInterpretation: true)();
            return Visit(new ReplaceParameter(rule.Parameters[0], Visit(node.Arguments[0])).Visit(rule.Body));
        }
    }

    private sealed class ReplaceParameter(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
