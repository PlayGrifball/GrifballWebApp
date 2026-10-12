using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using NpgsqlTypes;

namespace GrifballWebApp.Database.Interceptors;

/// <summary>
/// Postgres: for each row with history (<see cref="RowHistory"/>) that a SaveChanges updates or deletes,
/// adds its old version to the history table, Valid from its PeriodStart to the transaction's time, and
/// moves PeriodStart to that time. The history rows are saved with the change, in its transaction: both
/// or neither. Every Postgres context has it (<see cref="DatabaseProviderExtensions.UseGrifballDatabase(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, DatabaseProvider, string)"/>).
/// <para>
/// Times are SQL Server's: every change in a transaction has the transaction's time, the database's
/// transaction_timestamp() (an insert's PeriodStart is its default). So a version that began in the same
/// transaction lasted no time, and isn't recorded: FOR SYSTEM_TIME leaves such versions out, and the
/// history's WITHOUT OVERLAPS refuses empty ranges. A version that began after the transaction's time (a
/// later transaction replaced it first) can't be replaced: the save fails, as SQL Server's does.
/// </para>
/// <para>
/// What a SaveChanges doesn't do, it doesn't see: ExecuteUpdate, ExecuteDelete (see
/// <see cref="RowHistoryExtensions.ExecuteDeleteWithHistoryAsync"/>) and raw SQL change rows with no
/// history. What the database does on its own as part of a SaveChanges it is told about: rows deleted
/// by ON DELETE CASCADE, which it reads before the delete.
/// </para>
/// </summary>
public sealed class RowHistoryInterceptor : SaveChangesInterceptor
{
    public static RowHistoryInterceptor Instance { get; } = new();

    private RowHistoryInterceptor()
    {
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
            RecordAsync(context, async: false, default).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
            await RecordAsync(context, async: true, cancellationToken);
        return result;
    }

    private static async Task RecordAsync(DbContext context, bool async, CancellationToken cancellationToken)
    {
        // What SaveChanges would cascade to tracked entities, now, so they're among the entries.
        context.ChangeTracker.CascadeChanges();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        // Inserts take the database's default, which is the transaction's time: only updates and deletes
        // (which may cascade to rows with history) need it here.
        var now = entries.Any(e => e.State == EntityState.Deleted || (e.State == EntityState.Modified && e.Metadata.FindHistory() is not null))
            ? await TransactionTimeAsync(context, async, cancellationToken)
            : default;

        // Rows recorded or to be: each once, though two deletes may cascade to it.
        var recorded = new HashSet<string>(context.ChangeTracker.Entries()
            .Select(e => RowKey(e.Metadata, p => e.Property(p.Name).CurrentValue)));

        foreach (var entry in entries)
        {
            var history = entry.Metadata.FindHistory();
            if (entry.State == EntityState.Added)
            {
                // Whatever the app set: the database's default, transaction_timestamp(), read back by EF.
                if (history is not null)
                    entry.Property(RowHistory.PeriodStart).CurrentValue = default(DateTime);
                continue;
            }

            var values = entry.OriginalValues;
            if (history is not null && (DateTime)values[RowHistory.PeriodStart]! == default)
            {
                // Attached rather than loaded (Attach, Update, Remove of an entity from elsewhere: Identity's
                // UserStore.UpdateAsync does this), so its original values are whatever it was given; what's
                // in the database is what's being replaced. No row: the save fails, as it would anyway.
                var databaseValues = async
                    ? await entry.GetDatabaseValuesAsync(cancellationToken)
                    : entry.GetDatabaseValues();
                if (databaseValues is null)
                    continue;
                values = databaseValues;
            }

            if (history is not null)
            {
                Record(context, entry.Metadata, history, p => values[p], now);
                if (entry.State == EntityState.Modified)
                    entry.Property(RowHistory.PeriodStart).CurrentValue = now;
            }

            if (entry.State == EntityState.Deleted)
                await RecordCascadeAsync(context, entry.Metadata, p => values[p], now, recorded, async, cancellationToken);
        }
    }

    /// <summary>
    /// Adds to the context's changes the history of the rows <paramref name="query"/> reads, and of what
    /// deleting them would cascade to, for a delete SaveChanges won't see (ExecuteDelete).
    /// </summary>
    internal static async Task RecordDeletesAsync<TEntity>(DbContext context, IQueryable<TEntity> query, CancellationToken cancellationToken)
        where TEntity : class
    {
        var now = await TransactionTimeAsync(context, async: true, cancellationToken);
        var entityType = context.Model.FindEntityType(typeof(TEntity))!;
        var history = entityType.FindHistory();
        var properties = entityType.GetProperties().ToList();
        var recorded = new HashSet<string>();

        foreach (var row in await Values(query, entityType, properties).ToListAsync(cancellationToken))
        {
            object? value(IProperty p) => row[properties.IndexOf(p)];
            recorded.Add(RowKey(entityType, value));
            if (history is not null)
                Record(context, entityType, history, value, now);
            await RecordCascadeAsync(context, entityType, value, now, recorded, async: true, cancellationToken);
        }
    }

    // Each transaction's time, read once: a weak table, so it goes with its transaction. By EF's
    // transaction, new for each one, not the DbTransaction, which Npgsql reuses on a pooled connection.
    private static readonly ConditionalWeakTable<IDbContextTransaction, StrongBox<DateTime>> TransactionTimes = new();

    /// <summary>
    /// The time of the transaction the changes are saved in: the database's transaction_timestamp(), as
    /// PeriodStart's default, and as SQL Server stamps every change of a transaction with its start. In
    /// a transaction (the caller's, or ExecuteDeleteWithHistoryAsync's), read once and kept for each of
    /// its SaveChanges. Without one, a SaveChanges is its own transaction, which EF begins after this:
    /// the time is read just before it.
    /// </summary>
    private static async Task<DateTime> TransactionTimeAsync(DbContext context, bool async, CancellationToken cancellationToken)
    {
        var transaction = context.Database.CurrentTransaction;
        if (transaction is not null && TransactionTimes.TryGetValue(transaction, out var known))
            return known.Value;

        var query = context.Database.SqlQueryRaw<DateTime>("SELECT transaction_timestamp() AS \"Value\"");
        var time = UtcDateTimeConverter.ToUtc(async ? await query.SingleAsync(cancellationToken) : query.Single());
        if (transaction is not null)
            TransactionTimes.AddOrUpdate(transaction, new StrongBox<DateTime>(time));
        return time;
    }

    /// <summary>
    /// Adds the version <paramref name="value"/> gives to the history, from its PeriodStart to
    /// <paramref name="now"/>, the transaction's time. One that began in this transaction lasted no time
    /// and isn't recorded. One that began after it was replaced by a later transaction: the save fails,
    /// as SQL Server's does (error 13535), rather than record a version ending before it began.
    /// </summary>
    private static void Record(DbContext context, IEntityType entityType, IEntityType history, Func<IProperty, object?> value, DateTime now)
    {
        // The range has no converter, so its bounds are made UTC here.
        var start = UtcDateTimeConverter.ToUtc((DateTime)value(entityType.FindProperty(RowHistory.PeriodStart)!)!);
        if (start > now)
        {
            throw new DbUpdateException(
                $"Data modification failed on '{entityType.GetSchemaQualifiedTableName()}': the row's current version began at {start:O}, " +
                $"after this transaction's time, {now:O}, so a later transaction changed it. Retry in a new transaction.");
        }
        if (start == now)
            return;

        // The periods are Valid's bounds (the table's PeriodEnd is a generated end of time).
        var row = new Dictionary<string, object>();
        foreach (var property in entityType.GetProperties().Where(p => p.Name is not (RowHistory.PeriodStart or RowHistory.PeriodEnd)))
            row[property.Name] = value(property)!;
        row[RowHistory.Valid] = new NpgsqlRange<DateTime>(start, true, false, now, false, false);

        context.Set<Dictionary<string, object>>(history.Name).Add(row);
    }

    /// <summary>
    /// The rows ON DELETE CASCADE will delete with the one <paramref name="value"/> gives, and theirs,
    /// read from the database, as nothing in the context says what they are.
    /// </summary>
    private static async Task RecordCascadeAsync(DbContext context, IEntityType principalType, Func<IProperty, object?> value, DateTime now,
        HashSet<string> recorded, bool async, CancellationToken cancellationToken)
    {
        foreach (var foreignKey in principalType.GetReferencingForeignKeys().Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade))
        {
            var keyValues = foreignKey.PrincipalKey.Properties.Select(value).ToArray();
            if (keyValues.Any(v => v is null))
                continue;

            var dependentType = foreignKey.DeclaringEntityType;
            var properties = dependentType.GetProperties().ToList();
            var query = Rows(context, dependentType, foreignKey.Properties, keyValues, properties);
            var rows = async ? await query.ToListAsync(cancellationToken) : query.ToList();

            foreach (var row in rows)
            {
                object? dependentValue(IProperty p) => row[properties.IndexOf(p)];
                if (!recorded.Add(RowKey(dependentType, dependentValue)))
                    continue;

                if (dependentType.FindHistory() is { } history)
                    Record(context, dependentType, history, dependentValue, now);
                await RecordCascadeAsync(context, dependentType, dependentValue, now, recorded, async, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Each <paramref name="entityType"/> row whose <paramref name="filter"/> columns are
    /// <paramref name="filterValues"/>, as <see cref="Values"/>.
    /// </summary>
    private static IQueryable<object?[]> Rows(DbContext context, IEntityType entityType, IReadOnlyList<IProperty> filter, object?[] filterValues,
        IReadOnlyList<IProperty> properties)
    {
        var set = (IQueryable)SetMethod.MakeGenericMethod(entityType.ClrType).Invoke(context, null)!;
        var row = Expression.Parameter(entityType.ClrType, "row");

        // Values from a closure, not constants, so EF sends them as parameters and reuses the query.
        var predicate = filter
            .Select((p, i) => (Expression)Expression.Equal(
                PropertyOf(row, p),
                Expression.Field(Expression.Constant(Activator.CreateInstance(typeof(StrongBox<>).MakeGenericType(p.ClrType), filterValues[i])), "Value")))
            .Aggregate(Expression.AndAlso);

        var query = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [entityType.ClrType],
            set.Expression, Expression.Quote(Expression.Lambda(predicate, row)));
        return Values(set.Provider.CreateQuery(query), entityType, properties);
    }

    /// <summary>
    /// The rows of <paramref name="query"/> as their <paramref name="properties"/>' values: shadow ones
    /// too, which an entity read untracked doesn't have.
    /// </summary>
    private static IQueryable<object?[]> Values(IQueryable query, IEntityType entityType, IReadOnlyList<IProperty> properties)
    {
        var row = Expression.Parameter(entityType.ClrType, "row");
        var selector = Expression.NewArrayInit(typeof(object),
            properties.Select(p => Expression.Convert(PropertyOf(row, p), typeof(object))));
        return query.Provider.CreateQuery<object?[]>(Expression.Call(typeof(Queryable), nameof(Queryable.Select), [entityType.ClrType, typeof(object?[])],
            query.Expression, Expression.Quote(Expression.Lambda(selector, row))));
    }

    private static readonly System.Reflection.MethodInfo SetMethod =
        typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;

    private static Expression PropertyOf(ParameterExpression row, IProperty property)
    {
        return Expression.Call(typeof(EF), nameof(EF.Property), [property.ClrType], row, Expression.Constant(property.Name));
    }

    private static string RowKey(IReadOnlyEntityType entityType, Func<IProperty, object?> value)
    {
        var key = entityType.FindPrimaryKey()!.Properties.Select(p => Convert.ToString(value((IProperty)p), CultureInfo.InvariantCulture));
        return entityType.Name + "|" + string.Join("|", key);
    }
}
