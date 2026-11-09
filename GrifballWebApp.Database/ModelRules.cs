using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace GrifballWebApp.Database;

/// <summary>
/// A model's rule (an <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> on the model, e.g. <c>RecordedGame.Manual</c>) used
/// inside a bigger query, on a navigation or in a projection, where it can't be passed to <c>Where</c>. Write
/// <c>ModelRules.Meets(x, Model.Rule)</c> in the query and pass the query through <see cref="Inline{T, TResult}"/>: the
/// call becomes the rule itself, so it translates to SQL and the rule has one definition.
/// </summary>
public static class ModelRules
{
    private static readonly ConcurrentDictionary<LambdaExpression, Delegate> Compiled = new();

    /// <summary>Whether <paramref name="entity"/> meets <paramref name="rule"/>. Inside a query passed through
    /// <see cref="Inline{T, TResult}"/> the call is replaced by the rule's body.</summary>
    public static bool Meets<T>(T entity, Expression<Func<T, bool>> rule)
        => ((Func<T, bool>)Compiled.GetOrAdd(rule, r => r.Compile()))(entity);

    /// <summary>The query lambda with every <see cref="Meets"/> call, nested lambdas included, replaced by the rule it
    /// names, on the entity it was given.</summary>
    public static Expression<Func<T, TResult>> Inline<T, TResult>(Expression<Func<T, TResult>> query)
        => (Expression<Func<T, TResult>>)new Inliner().Visit(query);

    private sealed class Inliner : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(ModelRules) || node.Method.Name != nameof(Meets))
                return base.VisitMethodCall(node);

            var entity = Visit(node.Arguments[0]);
            var rule = (LambdaExpression)Expression.Lambda(node.Arguments[1]).Compile().DynamicInvoke()!;
            return new ParameterReplacer(rule.Parameters[0], entity).Visit(rule.Body);
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
