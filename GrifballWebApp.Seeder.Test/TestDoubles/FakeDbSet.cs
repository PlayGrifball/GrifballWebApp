using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Seeder.Test;

/// <summary>A GrifballContext DbSet kept in memory: <see cref="Add"/>/<see cref="AddRange(IEnumerable{TEntity})"/> keep
/// what's added (so a test can assert on <see cref="Items"/>), and the IQueryable members answer EF's AnyAsync with its
/// synchronous fallback (no IAsyncQueryProvider, so it runs Any() on this set's own Items). DbSet{T}'s parameterless
/// constructor exists for exactly this: a test double standing in for a real, database-backed set.</summary>
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
    IQueryProvider IQueryable.Provider => Items.AsQueryable().Provider;
    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
}
