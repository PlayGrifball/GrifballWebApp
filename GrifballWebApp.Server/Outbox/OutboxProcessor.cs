using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Outbox;

/// <summary>
/// Takes due outbox messages and sends them with their handlers (<see cref="IOutboxHandler"/>), one at a time, each in
/// its own scope. <see cref="OutboxDispatcher"/> runs it; tests call it directly.
/// </summary>
public sealed class OutboxProcessor
{
    public const string PreviewSkipReason = "Discord is disabled in preview mode";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<OutboxOptions> _options;
    private readonly IOptions<DiscordOptions> _discordOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopes, IOptions<OutboxOptions> options, IOptions<DiscordOptions> discordOptions, TimeProvider time, ILogger<OutboxProcessor> logger)
    {
        _scopes = scopes;
        _options = options;
        _discordOptions = discordOptions;
        _time = time;
        _logger = logger;
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    /// <summary>Takes up to a batch of due messages and handles them; returns how many it took.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        List<long> ids;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            ids = await ClaimAsync(context, UtcNow, UtcNow + _options.Value.Lease, _options.Value.BatchSize, ct);
        }

        foreach (var id in ids)
            await ProcessAsync(id, ct);
        return ids.Count;
    }

    /// <summary>
    /// Takes up to <paramref name="batchSize"/> due messages, oldest first: Pending or Failed, due by
    /// <paramref name="now"/>, and with no earlier message of their group still to send (so a group's messages go one
    /// at a time, in order). Each is leased until <paramref name="leaseUntil"/> and its attempt counted, in one
    /// statement. Rows another dispatcher is taking are passed over rather than waited for (UPDLOCK, READPAST on SQL
    /// Server; FOR UPDATE SKIP LOCKED on Postgres), so two dispatchers never take the same message.
    /// </summary>
    public static async Task<List<long>> ClaimAsync(GrifballContext context, DateTime now, DateTime leaseUntil, int batchSize, CancellationToken ct)
    {
        FormattableString sql = context.Database.IsNpgsql()
            ? (FormattableString)$"""
              UPDATE "Discord"."Outbox" AS u SET "NextAttemptAt" = {leaseUntil}, "Attempts" = u."Attempts" + 1
              WHERE u."Id" IN (
                  SELECT o."Id" FROM "Discord"."Outbox" AS o
                  WHERE o."Status" IN (0, 1) AND o."NextAttemptAt" <= {now}
                    AND NOT EXISTS (SELECT 1 FROM "Discord"."Outbox" AS p
                                    WHERE p."GroupKey" = o."GroupKey" AND p."Sequence" < o."Sequence" AND p."Status" IN (0, 1))
                  ORDER BY o."Id"
                  LIMIT {batchSize}
                  FOR UPDATE SKIP LOCKED)
              RETURNING u."Id" AS "Value"
              """
            : (FormattableString)$"""
              WITH due AS (
                  SELECT TOP ({batchSize}) o.[Id], o.[NextAttemptAt], o.[Attempts]
                  FROM [Discord].[Outbox] AS o WITH (UPDLOCK, READPAST, ROWLOCK)
                  WHERE o.[Status] IN (0, 1) AND o.[NextAttemptAt] <= {now}
                    AND NOT EXISTS (SELECT 1 FROM [Discord].[Outbox] AS p
                                    WHERE p.[GroupKey] = o.[GroupKey] AND p.[Sequence] < o.[Sequence] AND p.[Status] IN (0, 1))
                  ORDER BY o.[Id])
              UPDATE due SET [NextAttemptAt] = {leaseUntil}, [Attempts] = [Attempts] + 1
              OUTPUT inserted.[Id] AS [Value];
              """;
        var ids = await context.Database.SqlQuery<long>(sql).ToListAsync(ct);
        ids.Sort();
        return ids;
    }

    private async Task ProcessAsync(long id, CancellationToken ct)
    {
        IOutboxHandler? handler = null;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var message = await context.Outbox.SingleAsync(x => x.Id == id, ct);

            if (_discordOptions.Value.DisableGlobally)
            {
                _logger.LogInformation("Preview without Discord: skipping outbox message {OutboxId} ({Kind})", message.Id, message.Kind);
                Finish(message, OutboxStatus.Skipped, PreviewSkipReason);
                await context.SaveChangesAsync(ct);
                return;
            }

            handler = scope.ServiceProvider.GetServices<IOutboxHandler>().FirstOrDefault(x => x.Kind == message.Kind)
                ?? throw new InvalidOperationException($"No outbox handler for {message.Kind}");

            var outcome = await handler.HandleAsync(message, context, ct);
            if (outcome.ContinueAt is { } at)
            {
                // Not a failure: the attempt counted when it was taken is handed back.
                message.Attempts--;
                message.Status = OutboxStatus.Pending;
                message.NextAttemptAt = at;
            }
            else
            {
                Finish(message, OutboxStatus.Sent, null);
            }
            // With whatever the handler wrote back to the domain, in one transaction.
            await context.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Stopping: the lease runs out and it is taken again.
            throw;
        }
        catch (PreviewDiscordDisabledException ex)
        {
            _logger.LogInformation(ex, "Preview without Discord: skipping outbox message {OutboxId}", id);
            await UpdateAsync(id, (message, _, _) =>
            {
                Finish(message, OutboxStatus.Skipped, PreviewSkipReason);
                return Task.CompletedTask;
            }, ct);
        }
        catch (Exception ex)
        {
            await FailAsync(id, handler, ex, ct);
        }
    }

    private async Task FailAsync(long id, IOutboxHandler? handler, Exception ex, CancellationToken ct)
    {
        await UpdateAsync(id, async (message, context, outbox) =>
        {
            var now = UtcNow;
            message.LastError = Truncate(ex.Message);
            var tooOld = handler is not null && now - message.CreatedAt >= handler.MaxAge;
            if (message.Attempts < _options.Value.MaxAttempts && !tooOld)
            {
                message.Status = OutboxStatus.Failed;
                message.NextAttemptAt = now + _options.Value.RetryDelayAfter(message.Attempts);
                _logger.LogWarning(ex, "Outbox message {OutboxId} ({Kind}) failed on attempt {Attempts}; trying again at {NextAttemptAt:O}",
                    message.Id, message.Kind, message.Attempts, message.NextAttemptAt);
                return;
            }

            Finish(message, OutboxStatus.DeadLettered, message.LastError);
            _logger.LogError(ex, "Outbox message {OutboxId} ({Kind}, group {GroupKey}) was dead-lettered after {Attempts} attempts over {Age}",
                message.Id, message.Kind, message.GroupKey, message.Attempts, now - message.CreatedAt);

            // The rest of its group came after it, and goes with it (unless it can go ahead without it).
            if (message.GroupKey is not null && handler?.DeadLetterSkipsGroup is not false)
            {
                var rest = await context.Outbox
                    .Where(x => x.GroupKey == message.GroupKey && x.Sequence > message.Sequence)
                    .Where(x => x.Status == OutboxStatus.Pending || x.Status == OutboxStatus.Failed)
                    .ToListAsync(ct);
                foreach (var later in rest)
                    Finish(later, OutboxStatus.Skipped, $"Outbox message {message.Id} before it was dead-lettered");
            }

            if (handler is not null)
                await handler.OnDeadLetteredAsync(message, context, outbox, ct);
        }, ct);
    }

    /// <summary>Changes the message in a scope of its own (the failed attempt's changes thrown away), and saves.</summary>
    private async Task UpdateAsync(long id, Func<OutboxMessage, GrifballContext, IOutbox, Task> update, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var message = await context.Outbox.SingleAsync(x => x.Id == id, ct);
        await update(message, context, outbox);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void Finish(OutboxMessage message, OutboxStatus status, string? error)
    {
        message.Status = status;
        message.SentAt = UtcNow;
        message.LastError = error;
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];

    /// <summary>Deletes sent and skipped messages, and dead letters, past their retention; returns how many.</summary>
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var sentBefore = UtcNow - _options.Value.SentRetention;
        var deadBefore = UtcNow - _options.Value.DeadLetterRetention;
        var deleted = await context.Outbox
            .Where(x => ((x.Status == OutboxStatus.Sent || x.Status == OutboxStatus.Skipped) && x.SentAt < sentBefore)
                     || (x.Status == OutboxStatus.DeadLettered && x.SentAt < deadBefore))
            .ExecuteDeleteAsync(ct);
        if (deleted > 0)
            _logger.LogInformation("Deleted {Count} old outbox messages", deleted);
        return deleted;
    }
}
