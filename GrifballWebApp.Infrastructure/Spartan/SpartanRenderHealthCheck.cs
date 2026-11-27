using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using PlayGrifball.InfiniteSpartanRender.Client;

namespace GrifballWebApp.Infrastructure.Spartan;

/// <summary>
/// Healthy while the render service answers its catalog (<c>SpartanRender:BaseAddress</c>): the network path, the
/// NetworkPolicy and the service itself. Registered as Degraded-on-failure, outside the readiness checks: a render
/// outage must not take the website out of service. The result is cached for <see cref="Lifetime"/>, since
/// <c>/health</c> is public and the check runs every 30 s.
/// </summary>
public sealed class SpartanRenderHealthCheck(InfiniteSpartanRenderClient client, IMemoryCache cache, ILogger<SpartanRenderHealthCheck> logger) : IHealthCheck
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const string CacheKey = "spartan-render-health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out HealthCheckResult known))
            return known;

        var result = await Probe(context.Registration.FailureStatus, cancellationToken);
        cache.Set(CacheKey, result, Lifetime);
        return result;
    }

    private async Task<HealthCheckResult> Probe(HealthStatus failureStatus, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            var catalog = await client.GetCatalogAsync(timeout.Token);
            return HealthCheckResult.Healthy($"Spartan render service answered, {catalog.Cores.Count} cores");
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "The Spartan render service health probe failed");
            var why = e is OperationCanceledException ? $"no answer within {Timeout.TotalSeconds:0.#} s" : e.GetType().Name;
            return new HealthCheckResult(failureStatus, $"Spartan render service unreachable ({why})", e);
        }
    }
}
