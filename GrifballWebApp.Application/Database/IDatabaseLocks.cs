namespace GrifballWebApp.Application.Database;

/// <summary>
/// Locks the use cases take in the database, each in its own provider's SQL (Infrastructure's ContextDatabaseLocks), on
/// the scope's context and its transaction.
/// </summary>
public interface IDatabaseLocks
{
    /// <summary>
    /// Locks a season's row until the current transaction ends, so two draft changes of a season run one after the
    /// other. SQLite takes nothing: its one writer already runs them so.
    /// </summary>
    Task LockSeasonRowAsync(int seasonID, CancellationToken ct = default);
}
