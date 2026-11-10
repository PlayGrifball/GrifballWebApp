using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GrifballWebApp.Infrastructure.Outbox;

public static class OutboxJson
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
