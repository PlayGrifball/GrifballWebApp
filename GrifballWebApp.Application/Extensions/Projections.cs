using System.Linq.Expressions;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Extensions;

/// <summary>Composing projections the database translates as one, where C# would otherwise copy a base DTO's fields.</summary>
public static class Projections
{
    /// <summary>
    /// The projection of a DTO that derives from <typeparamref name="TBase"/>: the members <paramref name="extra"/> sets (a
    /// member initializer, e.g. <c>s =&gt; new SeasonListItemDto { TeamCount = s.Teams.Count }</c>) and the base's from
    /// <paramref name="baseFields"/> (a member initializer too), as one expression. A member both set is <paramref name="extra"/>'s.
    /// The model's rules either uses (<see cref="ModelRules.Apply"/>) are inlined (<see cref="ModelRules.Inline"/>).
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
        return ModelRules.Inline(Expression.Lambda<Func<TSource, T>>(Expression.MemberInit(newT, fields.Concat(bindings)), parameter));
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
