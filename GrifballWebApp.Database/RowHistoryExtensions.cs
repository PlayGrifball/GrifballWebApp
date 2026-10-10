using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace GrifballWebApp.Database;

public static class RowHistoryExtensions
{
    /// <summary>
    /// <typeparamref name="TEntity"/>'s rows as they were at <paramref name="asOf"/> (UTC, like every
    /// time here), untracked. SQL Server: its temporal table. Postgres: the current rows that began by
    /// then and the history rows that were current then:
    /// <code>
    /// SELECT ... FROM "Event"."Teams" WHERE "PeriodStart" &lt;= @asOf
    /// UNION ALL
    /// SELECT ..., lower("Valid") FROM "Event"."TeamsHistory" WHERE "Valid" @> @asOf
    /// </code>
    /// Only this table: navigations (Include, joins) read the current rows. Postgres has no history from
    /// before it began keeping it (AddRowHistory): rows already there began then.
    /// </summary>
    public static IQueryable<TEntity> AsOf<TEntity>(this DbContext context, DateTime asOf) where TEntity : class
    {
        if (!context.Database.IsNpgsql())
            return context.Set<TEntity>().TemporalAsOf(asOf).AsNoTracking();

        var entityType = context.Model.FindEntityType(typeof(TEntity))!;
        var history = entityType.FindHistory()
            ?? throw new InvalidOperationException($"{entityType.DisplayName()} has no history");
        var sql = context.GetService<ISqlGenerationHelper>();
        var columns = entityType.GetProperties().Select(p => sql.DelimitIdentifier(p.GetColumnName())).ToList();
        var pastColumns = columns.Select(c => c == sql.DelimitIdentifier(RowHistory.PeriodStart) ? $"lower({sql.DelimitIdentifier(RowHistory.Valid)})" : c);

        var query =
            $"SELECT {string.Join(", ", columns)} FROM {sql.DelimitIdentifier(entityType.GetTableName()!, entityType.GetSchema())} " +
            $"WHERE {sql.DelimitIdentifier(RowHistory.PeriodStart)} <= @asOf " +
            $"UNION ALL " +
            $"SELECT {string.Join(", ", pastColumns)} FROM {sql.DelimitIdentifier(history.GetTableName()!, history.GetSchema())} " +
            $"WHERE {sql.DelimitIdentifier(RowHistory.Valid)} @> @asOf";
        var parameter = new NpgsqlParameter("asOf", NpgsqlDbType.Timestamp) { Value = DateTime.SpecifyKind(asOf, DateTimeKind.Unspecified) };
        return context.Set<TEntity>().FromSqlRaw(query, parameter).AsNoTracking();
    }

    /// <summary>
    /// Postgres: <typeparamref name="TEntity"/>'s history rows: its columns (by property name, as in
    /// <c>EF.Property&lt;string&gt;(h, "TeamName")</c>) and Valid, the <see cref="NpgsqlRange{T}"/> of
    /// time each version was current.
    /// </summary>
    public static IQueryable<Dictionary<string, object>> HistoryOf<TEntity>(this DbContext context) where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))!;
        var history = entityType.FindHistory()
            ?? throw new InvalidOperationException($"{entityType.DisplayName()} has no history");
        return context.Set<Dictionary<string, object>>(history.Name).AsNoTracking();
    }

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
