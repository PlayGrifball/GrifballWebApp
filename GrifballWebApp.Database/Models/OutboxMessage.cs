namespace GrifballWebApp.Database.Models;

/// <summary>
/// A side effect on Discord, saved with the database change it belongs to (the transactional outbox) and sent
/// afterwards by the outbox dispatcher. A rollback of the change drops the message with it.
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }

    /// <summary>What to do: picks the handler that sends it.</summary>
    public required string Kind { get; set; }

    /// <summary>What the handler needs, as JSON.</summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Messages of one group (e.g. <c>match:42</c>) are sent one at a time, in <see cref="Sequence"/> order: one is
    /// not sent while an earlier one of its group is still to send. Null: on its own.
    /// </summary>
    public string? GroupKey { get; set; }

    public int Sequence { get; set; }

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    /// <summary>Times the dispatcher has taken it to send, counted as it takes it: more than 1 is a retry.</summary>
    public int Attempts { get; set; }

    /// <summary>Not sent before then: a retry's backoff, or the lease of the dispatcher sending it.</summary>
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When it was sent, skipped or dead-lettered.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>What sending it made (message and thread ids), as JSON; kept as it goes, for a retry to pick up.</summary>
    public string? Result { get; set; }
}

public enum OutboxStatus
{
    /// <summary>Not tried yet.</summary>
    Pending = 0,
    /// <summary>Failed, tried again at <see cref="OutboxMessage.NextAttemptAt"/>.</summary>
    Failed = 1,
    Sent = 2,
    /// <summary>Not sent, and won't be: Discord is off (preview), or an earlier message of its group was dead-lettered.</summary>
    Skipped = 3,
    /// <summary>Given up on after too many attempts or too long.</summary>
    DeadLettered = 4,
}
