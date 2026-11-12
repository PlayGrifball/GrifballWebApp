using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace GrifballWebApp.Seeder.Test;

/// <summary>A GrifballContext DbSet kept in memory: <see cref="Add"/>/<see cref="AddRange(IEnumerable{TEntity})"/> keep
/// what's added (so a test can assert on <see cref="Items"/>), and queries on it, composed or not (Where(...).AnyAsync(),
/// ToListAsync(), ...), run as LINQ to Objects on <see cref="Items"/> through <see cref="FakeAsyncQueryProvider"/>.
/// DbSet{T}'s parameterless constructor exists for exactly this: a test double standing in for a real, database-backed
/// set.</summary>
internal sealed class FakeDbSet<TEntity> : DbSet<TEntity>, IQueryable<TEntity>, IAsyncEnumerable<TEntity> where TEntity : class
{
    private readonly FakeAsyncQueryable<TEntity> _query;

    public FakeDbSet() => _query = new FakeAsyncQueryable<TEntity>(Items);

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

    public override IAsyncEnumerable<TEntity> AsAsyncEnumerable() => _query;

    // The query reads Items when it runs (EnumerableQuery over the list, not a copy), so one is enough
    Type IQueryable.ElementType => typeof(TEntity);
    Expression IQueryable.Expression => ((IQueryable)_query).Expression;
    IQueryProvider IQueryable.Provider => ((IQueryable)_query).Provider;
    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
    IAsyncEnumerator<TEntity> IAsyncEnumerable<TEntity>.GetAsyncEnumerator(CancellationToken cancellationToken)
        => _query.GetAsyncEnumerator(cancellationToken);
}

/// <summary>A LINQ to Objects query that EF's async operators accept: its provider is a
/// <see cref="FakeAsyncQueryProvider"/>, and it enumerates asynchronously (ToListAsync and the like).</summary>
internal sealed class FakeAsyncQueryable<T> : EnumerableQuery<T>, IQueryable<T>, IAsyncEnumerable<T>
{
    private readonly FakeAsyncQueryProvider _provider;

    public FakeAsyncQueryable(IEnumerable<T> items) : base(items) => _provider = new FakeAsyncQueryProvider(this);

    public FakeAsyncQueryable(Expression expression) : base(expression) => _provider = new FakeAsyncQueryProvider(this);

    IQueryProvider IQueryable.Provider => _provider;

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in this.AsEnumerable())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
        await Task.CompletedTask;
    }
}

/// <summary>
/// EF's async query operators refuse a provider that isn't IAsyncQueryProvider outright (no synchronous fallback): this
/// one keeps every composed query a <see cref="FakeAsyncQueryable{T}"/>, and answers the scalar operators (AnyAsync,
/// CountAsync, FirstOrDefaultAsync, ...) by running them synchronously with the LINQ to Objects provider, in a completed
/// Task. A cancelled token throws, as EF does.
/// </summary>
internal sealed class FakeAsyncQueryProvider(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetInterfaces().Prepend(expression.Type)
            .First(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GetGenericArguments()[0];
        return (IQueryable)Activator.CreateInstance(typeof(FakeAsyncQueryable<>).MakeGenericType(elementType), expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new FakeAsyncQueryable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // EF's scalar async operators all ask for Task<T>: anything else (an async sequence) goes through
        // FakeAsyncQueryable's own GetAsyncEnumerator, not here
        if (!typeof(TResult).IsGenericType || typeof(TResult).GetGenericTypeDefinition() != typeof(Task<>))
            throw new NotSupportedException($"{nameof(FakeAsyncQueryProvider)} answers Task<T> results only, not {typeof(TResult)}.");
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var result = typeof(IQueryProvider).GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(resultType).Invoke(inner, [expression]);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result])!;
    }
}
