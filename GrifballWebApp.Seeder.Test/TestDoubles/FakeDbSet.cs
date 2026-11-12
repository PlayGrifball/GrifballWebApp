using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace GrifballWebApp.Seeder.Test;

/// <summary>A GrifballContext DbSet kept in memory: <see cref="Add"/>/<see cref="AddRange(IEnumerable{TEntity})"/> keep
/// what's added (so a test can assert on <see cref="Items"/>), and its <see cref="FakeAsyncQueryProvider"/> answers EF's
/// AnyAsync (and the other IQueryable async operators) by running the equivalent synchronous LINQ-to-Objects call on
/// <see cref="Items"/>. DbSet{T}'s parameterless constructor exists for exactly this: a test double standing in for a
/// real, database-backed set.</summary>
internal sealed class FakeDbSet<TEntity> : DbSet<TEntity>, IQueryable<TEntity> where TEntity : class
{
    public List<TEntity> Items { get; } = [];

    /// <summary>Never read: nothing in the seeder's unit tests needs the set's EF metadata.</summary>
    public override IEntityType EntityType => throw new NotSupportedException($"{nameof(FakeDbSet<TEntity>)} has no EF model.");

    public override EntityEntry<TEntity> Add(TEntity entity)
    {
        Items.Add(entity);
        return null!;
    }

    public override void AddRange(params TEntity[] entities) => Items.AddRange(entities);

    public override void AddRange(IEnumerable<TEntity> entities) => Items.AddRange(entities);

    Type IQueryable.ElementType => typeof(TEntity);
    Expression IQueryable.Expression => Items.AsQueryable().Expression;
    IQueryProvider IQueryable.Provider => new FakeAsyncQueryProvider(Items.AsQueryable().Provider);
    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
}

/// <summary>
/// EF's async query operators (AnyAsync and the rest) refuse a provider that isn't IAsyncQueryProvider outright (no
/// synchronous fallback): this one answers by running the expression with the real LINQ-to-Objects provider given
/// (<see cref="Queryable.AsQueryable"/>'s own), then wraps the (already computed) result in a completed Task of
/// whatever TResult's Task wraps.
/// </summary>
internal sealed class FakeAsyncQueryProvider(IQueryProvider inner) : IQueryProvider, IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => inner.CreateQuery(expression);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => inner.CreateQuery<TElement>(expression);
    public object? Execute(Expression expression) => inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        // TResult is Task<T> for every EF async operator (AnyAsync, CountAsync, ...): the inner, synchronous result
        // (already computed; nothing here is actually asynchronous) wrapped in a completed Task<T>.
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var result = typeof(IQueryProvider).GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(resultType).Invoke(inner, [expression]);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result])!;
    }
}
