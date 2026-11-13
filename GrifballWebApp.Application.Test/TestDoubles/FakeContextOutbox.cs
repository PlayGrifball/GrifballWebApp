using System.Text.Json;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// IOutbox as production's ContextOutbox (Infrastructure) keeps it: each message an OutboxMessage added to the same context,
/// so it is saved, or rolled back, with the change that queued it. The payload is plain JSON (the real one uses the app's
/// serializer options); tests read the rows' kind, group and order.
/// </summary>
internal sealed class FakeContextOutbox(FakeApplicationDbContext context) : IOutbox
{
    public async Task EnqueueAsync<TPayload>(string kind, TPayload payload, string? groupKey = null, CancellationToken ct = default)
    {
        // A group's messages go in order: after the ones already queued, saved or not (no group: no order)
        var saved = await context.Outbox.Where(x => x.GroupKey == groupKey).MaxAsync(x => (int?)x.Sequence, ct) ?? -1;
        var unsaved = context.Outbox.Local.Where(x => x.GroupKey == groupKey).Select(x => (int?)x.Sequence).Max() ?? -1;
        var sequence = groupKey is null ? 0 : Math.Max(saved, unsaved) + 1;

        context.Outbox.Add(new OutboxMessage
        {
            Kind = kind,
            Payload = JsonSerializer.Serialize(payload),
            GroupKey = groupKey,
            Sequence = sequence,
        });
    }
}
