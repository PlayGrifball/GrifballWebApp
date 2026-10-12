using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

public static class RowHistoryExtensions
{
    /// <summary>
    /// ExecuteDeleteAsync, on Postgres with history: the rows, and what deleting them cascades to, are
    /// read and their history saved (SaveChanges, which saves the context's other changes too), then
    /// deleted as before, in one statement, in one transaction with it (the caller's, if there is one).
    /// One by one, SaveChanges' way, would fail where rows being deleted refer to each other (a bracket's
    /// matches). SQL Server: ExecuteDeleteAsync.
    /// </summary>
    public static async Task<int> ExecuteDeleteWithHistoryAsync<TEntity>(this IQueryable<TEntity> query, DbContext context, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (!context.Database.IsNpgsql())
            return await query.ExecuteDeleteAsync(cancellationToken);

        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await Interceptors.RowHistoryInterceptor.RecordDeletesAsync(context, query, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        var deleted = await query.ExecuteDeleteAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return deleted;
    }
}
