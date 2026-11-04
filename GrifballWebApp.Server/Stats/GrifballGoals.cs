using System.Linq.Expressions;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Stats;

/// <summary>
/// The one definition of a player's Grifball goals in a Halo Infinite game, read by the season stats
/// (<see cref="PlayerGameStatsQuery"/>), the grades (GradesService) and the match page's box scores: the participant's
/// mode Score. Some custom modes pack other numbers into Score (#217); until that encoding is known their goals are the
/// value as recorded.
/// </summary>
public static class GrifballGoals
{
    /// <summary>A participant's goals, as an expression a query can translate.</summary>
    public static readonly Expression<Func<MatchParticipant, int>> InfiniteExpression = p => p.Score;

    private static readonly Func<MatchParticipant, int> InfiniteCompiled = InfiniteExpression.Compile();

    /// <summary>A participant's goals. Inside a query lambda passed through <see cref="Inline{T, TResult}"/> the call is
    /// replaced by <see cref="InfiniteExpression"/>, so it translates to SQL.</summary>
    public static int Infinite(MatchParticipant participant) => InfiniteCompiled(participant);

    /// <summary>The query lambda with every <see cref="Infinite"/> call, nested lambdas included, replaced by the
    /// definition itself.</summary>
    public static Expression<Func<T, TResult>> Inline<T, TResult>(Expression<Func<T, TResult>> query)
        => (Expression<Func<T, TResult>>)new Inliner().Visit(query);

    private sealed class Inliner : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(GrifballGoals) || node.Method.Name != nameof(Infinite))
                return base.VisitMethodCall(node);

            var participant = Visit(node.Arguments[0]);
            return new ParameterReplacer(InfiniteExpression.Parameters[0], participant).Visit(InfiniteExpression.Body);
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
