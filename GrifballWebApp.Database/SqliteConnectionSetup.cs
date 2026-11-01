using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Database;

/// <summary>
/// SQLite: each connection, as it opens, puts the file in WAL mode, so reads go on while a write runs
/// (one write at a time, whatever the connection: SQLite locks the whole file), and syncs at each
/// checkpoint rather than each commit, which WAL keeps safe from corruption. WAL is a setting of the
/// file, kept once set, so this changes nothing after the first connection. A write that finds another
/// under way waits for it (Microsoft.Data.Sqlite retries until the command's timeout), and EF's
/// transactions start IMMEDIATE, taking the write lock up front, so a transaction never fails half way
/// on another's lock. EF turns foreign keys on itself.
/// </summary>
public sealed class SqliteConnectionSetup : DbConnectionInterceptor
{
    public static readonly SqliteConnectionSetup Instance = new();

    private const string Pragmas = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
