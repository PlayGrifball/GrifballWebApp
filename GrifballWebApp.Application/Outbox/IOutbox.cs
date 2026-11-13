namespace GrifballWebApp.Application.Outbox;

/// <summary>
/// Queues Discord side effects with the database change they belong to: the message is added to the scope's
/// <see cref="Database.IApplicationDbContext"/> and nothing is saved here. The caller's SaveChanges (and its transaction, if any)
/// commits the change and the message together, and a rollback drops both. <c>OutboxDispatcher</c> sends it.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Adds a message of <paramref name="kind"/>. With a <paramref name="groupKey"/>, after the group's messages
    /// already queued (saved or not): it is not sent until they are.
    /// </summary>
    Task EnqueueAsync<TPayload>(string kind, TPayload payload, string? groupKey = null, CancellationToken ct = default);
}
