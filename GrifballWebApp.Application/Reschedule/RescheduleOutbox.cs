using GrifballWebApp.Application.Outbox;

namespace GrifballWebApp.Application.Reschedule;

/// <summary>
/// A reschedule request's Discord messages, sent through the outbox in the request's group: its thread made in three
/// steps (the request post in the reschedules channel, the thread from it, the captains and commissioners added to
/// it), then the decision told in the thread.
/// </summary>
public static class RescheduleOutbox
{
    public const string RequestPost = "reschedule.request-post";
    public const string ThreadCreate = "reschedule.thread-create";
    public const string ThreadInvite = "reschedule.thread-invite";
    public const string DecisionPost = "reschedule.decision-post";

    public static string Group(int rescheduleId) => $"reschedule:{rescheduleId}";

    public static async Task EnqueueThreadAsync(this IOutbox outbox, int rescheduleId, string content, string threadName, CancellationToken ct)
    {
        var group = Group(rescheduleId);
        await outbox.EnqueueAsync(RequestPost, new RescheduleRequestPostPayload(rescheduleId, content), group, ct);
        await outbox.EnqueueAsync(ThreadCreate, new RescheduleThreadPayload(rescheduleId, threadName), group, ct);
        await outbox.EnqueueAsync(ThreadInvite, new ReschedulePayload(rescheduleId), group, ct);
    }

    public static Task EnqueueDecisionAsync(this IOutbox outbox, int rescheduleId, string content, CancellationToken ct)
        => outbox.EnqueueAsync(DecisionPost, new RescheduleDecisionPayload(rescheduleId, content), Group(rescheduleId), ct);
}

public sealed record ReschedulePayload(int RescheduleId);

public sealed record RescheduleRequestPostPayload(int RescheduleId, string Content);

public sealed record RescheduleThreadPayload(int RescheduleId, string ThreadName);

public sealed record RescheduleDecisionPayload(int RescheduleId, string Content);

public sealed record RescheduleRequestPostResult(ulong MessageId, bool Found);

public sealed record RescheduleInviteResult(bool ErrorsPosted);
