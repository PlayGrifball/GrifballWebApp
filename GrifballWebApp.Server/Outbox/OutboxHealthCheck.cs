using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GrifballWebApp.Server.Outbox;

/// <summary>
/// Degraded (as registered) while the outbox holds dead letters: Discord messages given up on, which someone should
/// look at. They are deleted after <see cref="OutboxOptions.DeadLetterRetention"/>.
/// </summary>
public sealed class OutboxHealthCheck(GrifballContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        var deadLetters = await context.Outbox.CountAsync(x => x.Status == OutboxStatus.DeadLettered, cancellationToken);
        return deadLetters == 0
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(healthContext.Registration.FailureStatus, $"{deadLetters} Discord outbox messages were dead-lettered",
                data: new Dictionary<string, object> { ["dead_letters"] = deadLetters });
    }
}
