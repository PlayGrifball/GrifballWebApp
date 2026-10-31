using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GrifballWebApp.Server.Outbox;

/// <summary>
/// Queues Discord side effects with the database change they belong to: the message is added to the scope's
/// <see cref="GrifballContext"/> and nothing is saved here. The caller's SaveChanges (and its transaction, if any)
/// commits the change and the message together, and a rollback drops both. <see cref="OutboxDispatcher"/> sends it.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Adds a message of <paramref name="kind"/>. With a <paramref name="groupKey"/>, after the group's messages
    /// already queued (saved or not): it is not sent until they are.
    /// </summary>
    Task EnqueueAsync<TPayload>(string kind, TPayload payload, string? groupKey = null, CancellationToken ct = default);
}

internal static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed class ContextOutbox(GrifballContext context) : IOutbox
{

    public async Task EnqueueAsync<TPayload>(string kind, TPayload payload, string? groupKey = null, CancellationToken ct = default)
    {
        var sequence = 0;
        if (groupKey is not null)
        {
            var saved = await context.Outbox.Where(x => x.GroupKey == groupKey).MaxAsync(x => (int?)x.Sequence, ct) ?? -1;
            var unsaved = context.Outbox.Local.Where(x => x.GroupKey == groupKey).Select(x => (int?)x.Sequence).Max() ?? -1;
            sequence = Math.Max(saved, unsaved) + 1;
        }

        var now = DateTime.UtcNow;
        context.Outbox.Add(new OutboxMessage
        {
            Kind = kind,
            Payload = JsonSerializer.Serialize(payload, OutboxJson.Options),
            GroupKey = groupKey,
            Sequence = sequence,
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }
}
