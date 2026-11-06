using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrifballWebApp.Database;

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
    public static TimeSpan RetryEvery { get; set; } = TimeSpan.FromMilliseconds(100);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SqliteLocks = new();

    /// <summary>
    /// Waits for the lock <paramref name="name"/>, then holds it until the returned handle is disposed. Not reentrant:
    /// a second acquire of the same name waits for the first to be released, on any context. Cancelling stops the wait.
    /// </summary>
    public static async Task<IAsyncDisposable> AcquireLockAsync(this DatabaseFacade database, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
                await Task.Delay(RetryEvery, ct);
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
                await ExecuteAsync(database, sqlServer
                    ? "EXEC sp_releaseapplock @Resource = @name, @LockOwner = 'Session'"
                    : "SELECT pg_advisory_unlock(@key)", name, CancellationToken.None);
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
        var result = await ExecuteAsync(database, sqlServer
            // 0: granted; 1: granted after a wait; -1: timed out (someone holds it); below: an error
            ? """
              DECLARE @result int;
              EXEC @result = sp_getapplock @Resource = @name, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
              SELECT @result;
              """
            : "SELECT pg_try_advisory_lock(@key)", name, ct);
        if (!sqlServer)
            return (bool)result!;
        var code = Convert.ToInt32(result);
        if (code < -1)
            throw new InvalidOperationException($"sp_getapplock refused the lock '{name}' ({code})");
        return code >= 0;
    }

    private static async Task<object?> ExecuteAsync(DatabaseFacade database, string sql, string name, CancellationToken ct)
    {
        var connection = database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = database.CurrentTransaction?.GetDbTransaction();
        AddParameter(command, "name", name);
        AddParameter(command, "key", Key(name));
        return await command.ExecuteScalarAsync(ct);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        if (!command.CommandText.Contains('@' + name, StringComparison.Ordinal))
            return;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@" + name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
