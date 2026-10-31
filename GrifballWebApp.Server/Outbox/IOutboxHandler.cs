using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using System.Text.Json;

namespace GrifballWebApp.Server.Outbox;

/// <summary>
/// Sends the outbox messages of one <see cref="Kind"/>. Delivery is at least once: a message can be handled again
/// after it (partly) went out, when saving that failed or the process stopped. A handler finds what an earlier try
/// did, from the domain row it writes back to or <see cref="OutboxMessage.Result"/>, and doesn't do it again.
/// </summary>
public interface IOutboxHandler
{
    string Kind { get; }

    /// <summary>Dead-lettered when still failing this long after it was queued (or after the dispatcher's attempts).</summary>
    TimeSpan MaxAge { get; }

    /// <summary>
    /// Whether the later messages of its group are skipped when it is dead-lettered: true when they depend on it (a
    /// thread can't be made from a post that was never made), false when they can go ahead without it.
    /// </summary>
    bool DeadLetterSkipsGroup { get; }

    /// <summary>
    /// Sends it. Changes to tracked entities of <paramref name="context"/> (ids written back to the domain row) are
    /// saved with the message marked sent. Throwing is a failed attempt: those changes are thrown away, and it is tried
    /// again later.
    /// </summary>
    Task<OutboxOutcome> HandleAsync(OutboxMessage message, GrifballContext context, CancellationToken ct);

    /// <summary>
    /// The message was given up on: changes made here (and messages queued on <paramref name="outbox"/>) are saved with
    /// it marked dead-lettered, after the later messages of its group are skipped (when <see cref="DeadLetterSkipsGroup"/>).
    /// </summary>
    Task OnDeadLetteredAsync(OutboxMessage message, GrifballContext context, IOutbox outbox, CancellationToken ct);
}

/// <summary>What became of a handled message.</summary>
public readonly record struct OutboxOutcome(DateTime? ContinueAt)
{
    /// <summary>Sent.</summary>
    public static OutboxOutcome Done { get; } = new(null);

    /// <summary>Not finished, by design rather than failure (e.g. a wait between two steps): handled again at <paramref name="at"/>.</summary>
    public static OutboxOutcome ContinueLater(DateTime at) => new(at);
}

/// <summary>A handler with a typed payload and result.</summary>
public abstract class OutboxHandler<TPayload> : IOutboxHandler
{
    public abstract string Kind { get; }

    public virtual TimeSpan MaxAge => TimeSpan.FromHours(6);

    public virtual bool DeadLetterSkipsGroup => true;

    public Task<OutboxOutcome> HandleAsync(OutboxMessage message, GrifballContext context, CancellationToken ct)
        => HandleAsync(message, Payload(message), context, ct);

    public Task OnDeadLetteredAsync(OutboxMessage message, GrifballContext context, IOutbox outbox, CancellationToken ct)
        => OnDeadLetteredAsync(message, Payload(message), context, outbox, ct);

    protected abstract Task<OutboxOutcome> HandleAsync(OutboxMessage message, TPayload payload, GrifballContext context, CancellationToken ct);

    protected virtual Task OnDeadLetteredAsync(OutboxMessage message, TPayload payload, GrifballContext context, IOutbox outbox, CancellationToken ct)
        => Task.CompletedTask;

    protected static TPayload Payload(OutboxMessage message)
        => JsonSerializer.Deserialize<TPayload>(message.Payload, OutboxJson.Options) ?? throw new InvalidOperationException($"Outbox message {message.Id} has no payload");

    protected static TResult? GetResult<TResult>(OutboxMessage message)
        => message.Result is null ? default : JsonSerializer.Deserialize<TResult>(message.Result, OutboxJson.Options);

    protected static void SetResult<TResult>(OutboxMessage message, TResult result)
        => message.Result = JsonSerializer.Serialize(result, OutboxJson.Options);
}
