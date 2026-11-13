using System.Text.Json;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// IOutbox as production's ContextOutbox (Infrastructure) is, line for line: each message an OutboxMessage added to the
/// same context, so it is saved, or rolled back, with the change that queued it; the payload as OutboxJson writes it
/// (JsonSerializerDefaults.Web), so a test reading it sees what the dispatcher reads.
/// </summary>
internal sealed class FakeContextOutbox(FakeApplicationDbContext context) : IOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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
            Payload = JsonSerializer.Serialize(payload, Json),
            GroupKey = groupKey,
            Sequence = sequence,
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }
}
