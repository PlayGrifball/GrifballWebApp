using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace GrifballWebApp.Domain;

/// <summary>
/// A model's rule (an expression on the model, e.g. <c>Team.HasArchivedRecord</c> or <c>Team.PicksOf</c>, of any result)
/// used where it can't be passed to <c>Where</c>: inside a projection, or on a navigation. Write <c>x.Apply(Model.Rule)</c>
/// in the query and pass the query through <see cref="Inline{T, TResult}"/>: the call becomes the rule itself, so it
/// translates to SQL and the rule has one definition. A loaded object's bool runs the same rule through
/// <see cref="Evaluate{T, TResult}"/>.
/// </summary>
public static class ModelRules
{
    private static readonly ConcurrentDictionary<LambdaExpression, Delegate> Compiled = new();

    private static readonly MethodInfo ApplyDefinition = typeof(ModelRules).GetMethod(nameof(Apply))!;

    /// <summary>
    /// <paramref name="rule"/> on <paramref name="value"/>, in a query passed through <see cref="Inline{T, TResult}"/>,
    /// which replaces the call by the rule's body. Refused (thrown) anywhere else, as <c>EF.Functions</c> are: a projection
    /// that forgot <see cref="Inline{T, TResult}"/> would otherwise run it on half-loaded entities, with no error. On a
    /// loaded object use <see cref="Evaluate{T, TResult}"/>.
    /// </summary>
    public static TResult Apply<T, TResult>(this T value, Expression<Func<T, TResult>> rule)
        => throw new InvalidOperationException($"{nameof(Apply)} is only for queries passed through {nameof(ModelRules)}.{nameof(Inline)}; use {nameof(Evaluate)} on a loaded object");

    /// <summary><paramref name="rule"/> on a loaded <paramref name="value"/>: the rule compiled once, then cached.</summary>
    public static TResult Evaluate<T, TResult>(T value, Expression<Func<T, TResult>> rule)
        => ((Func<T, TResult>)Compiled.GetOrAdd(rule, r => r.Compile()))(value);

    /// <summary>The query lambda with every <see cref="Apply"/> call, nested lambdas and rules included, replaced by the
    /// rule it names, on the value it was given.</summary>
    public static Expression<Func<T, TResult>> Inline<T, TResult>(Expression<Func<T, TResult>> query)
        => (Expression<Func<T, TResult>>)new Inliner().Visit(query);

    private sealed class Inliner : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod || node.Method.GetGenericMethodDefinition() != ApplyDefinition)
                return base.VisitMethodCall(node);
            // The rule is the model's: a static field, or a factory of the request's values (Team.CaptainedBy(userID)),
            // evaluated here once per inlining
            var rule = (LambdaExpression)Expression.Lambda<Func<object>>(Expression.Convert(node.Arguments[1], typeof(object))).Compile(preferInterpretation: true)();
            return Visit(new ParameterReplacer(rule.Parameters[0], Visit(node.Arguments[0])).Visit(rule.Body));
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
