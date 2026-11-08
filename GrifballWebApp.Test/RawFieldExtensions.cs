using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

internal static class RawFieldExtensions
{
    /// <summary>
    /// Sets a tracked entity's field past its model's methods, for seeding a row as something outside the model left it
    /// (Halo's raw values as a pull saved them, a layout the decoder decided, a constraint test's illegal row).
    /// </summary>
    public static TEntity SetRaw<TEntity, TValue>(this DbContext context, TEntity entity, Expression<Func<TEntity, TValue>> property, TValue value)
        where TEntity : class
    {
        context.Entry(entity).Property(property).CurrentValue = value;
        return entity;
    }
}
