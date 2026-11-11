using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GrifballWebApp.Infrastructure.Outbox;

/// <summary>
/// Degraded (as registered) while the outbox holds dead letters: Discord messages given up on, which someone should
/// look at. They are deleted after <see cref="OutboxOptions.DeadLetterRetention"/>.
/// </summary>
public sealed class OutboxHealthCheck(GrifballContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var deadLetters = await db.Outbox.CountAsync(x => x.Status == OutboxStatus.DeadLettered, cancellationToken);
        return deadLetters == 0
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, $"{deadLetters} Discord outbox messages were dead-lettered",
                data: new Dictionary<string, object> { ["dead_letters"] = deadLetters });
    }
}
