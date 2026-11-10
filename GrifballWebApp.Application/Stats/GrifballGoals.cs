using System.Linq.Expressions;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Stats;

/// <summary>
/// The one definition of a player's Grifball goals in a Halo Infinite game, read by the season stats
/// (<see cref="PlayerGameStatsQuery"/>), the grades (GradesService) and the match page's box scores. A match whose mode
/// layout is known (<see cref="Match.ScoreLayout"/>, #217) has its goals decoded onto the participant
/// (<see cref="MatchParticipant.Goals"/>); one not decoded yet reads a Score of up to
/// <c>CustomScoreDecoder.PlainMaxScore</c> as goals, and has none it can read from a bigger one (null).
/// </summary>
public static class GrifballGoals
{
    /// <summary>A participant's goals, as an expression a query can translate (the model's rule,
    /// <see cref="MatchParticipant.CountedGoals"/>).</summary>
    public static readonly Expression<Func<MatchParticipant, int?>> InfiniteExpression = MatchParticipant.CountedGoals;

    private static readonly Func<MatchParticipant, int?> InfiniteCompiled = InfiniteExpression.Compile();

    /// <summary>A participant's goals. Inside a query lambda passed through <see cref="Inline{T, TResult}"/> the call is
    /// replaced by <see cref="InfiniteExpression"/>, so it translates to SQL.</summary>
    public static int? Infinite(MatchParticipant participant) => InfiniteCompiled(participant);

    /// <summary>The query lambda with every <see cref="Infinite"/> call, nested lambdas included, replaced by the
    /// definition itself; the model's rules it names (<see cref="ModelRules.Apply"/>, e.g. a match's
    /// <see cref="Match.WithHalosPoints"/>) are inlined too.</summary>
    public static Expression<Func<T, TResult>> Inline<T, TResult>(Expression<Func<T, TResult>> query)
        => ModelRules.Inline((Expression<Func<T, TResult>>)new Inliner().Visit(query));

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
