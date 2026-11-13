using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrifballWebApp.Infrastructure;

/// <summary>
/// A named lock held across every process using the database, for work that must run one at a time whichever pod
/// starts it. SQL Server: an application lock (<c>sp_getapplock</c>), PostgreSQL: an advisory lock, each owned by the
/// session: the context's connection stays open until the lock is released, and a process that dies takes its locks
/// with its connection. Both are scoped to the database, so environments sharing a server keep their own. SQLite is a
/// file one process opens (docs/sqlite.md): the lock is that process's, per file.
/// </summary>
public static class DatabaseLocks
{
    /// <summary>How often a lock someone else holds is tried again.</summary>
    public static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(100);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SqliteLocks = new();

    /// <summary>
    /// Waits for the lock <paramref name="name"/>, then holds it until the returned handle is disposed. Not reentrant:
    /// a second acquire of the same name waits for the first to be released, on any context. Cancelling stops the wait.
    /// </summary>
    public static Task<IAsyncDisposable> AcquireLockAsync(this DatabaseFacade database, string name, CancellationToken ct = default)
        => database.AcquireLockAsync(name, TimeProvider.System, ct);

    /// <inheritdoc cref="AcquireLockAsync(DatabaseFacade, string, CancellationToken)"/>
    /// <param name="time">The clock the waits between tries go by.</param>
    public static async Task<IAsyncDisposable> AcquireLockAsync(this DatabaseFacade database, string name, TimeProvider time, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(time);
        if (database.IsSqlite())
        {
            var semaphore = SqliteLocks.GetOrAdd($"{database.GetConnectionString()}\n{name}", _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(ct);
            return new Handle(() => { semaphore.Release(); return Task.CompletedTask; });
        }

        // SQL Server, else PostgreSQL
        var sqlServer = database.IsSqlServer();

        await database.OpenConnectionAsync(ct);
        try
        {
            while (!await TryAsync(database, sqlServer, name, ct))
                await Task.Delay(RetryEvery, time, ct);
        }
        catch
        {
            await database.CloseConnectionAsync();
            throw;
        }
        return new Handle(async () =>
        {
            try
            {
                if (sqlServer)
                    await ExecuteAsync(database, "EXEC sp_releaseapplock @Resource = @lock, @LockOwner = 'Session'", name, CancellationToken.None);
                else
                    await ExecuteAsync(database, "SELECT pg_advisory_unlock(@lock)", Key(name), CancellationToken.None);
            }
            finally
            {
                await database.CloseConnectionAsync();
            }
        });
    }

    /// <summary>Takes the lock if no one holds it, at once (no wait on the server, so no command timeout).</summary>
    private static async Task<bool> TryAsync(DatabaseFacade database, bool sqlServer, string name, CancellationToken ct)
    {
        // Each says 0 or more when granted, -1 when someone holds it. sp_getapplock's below -1 are errors: thrown.
        var result = sqlServer
            ? await ExecuteAsync(database, """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = @lock, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
                IF @result < -1 THROW 50000, 'sp_getapplock refused the lock', 1;
                SELECT @result;
                """, name, ct)
            : await ExecuteAsync(database, "SELECT CASE WHEN pg_try_advisory_lock(@lock) THEN 0 ELSE -1 END", Key(name), ct);
        return Convert.ToInt32(result) >= 0;
    }

    private static async Task<object?> ExecuteAsync(DatabaseFacade database, string sql, object lockParameter, CancellationToken ct)
    {
        var connection = database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = database.CurrentTransaction?.GetDbTransaction();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@lock";
        parameter.Value = lockParameter;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(ct);
    }

    /// <summary>PostgreSQL's advisory locks take a number: the name's, from its SHA-256, the same in every process.</summary>
    public static long Key(string name) => BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(name)), 0);

    private sealed class Handle(Func<Task> release) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                await release();
        }
    }
}
