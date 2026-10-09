using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetCord.Gateway;

namespace GrifballWebApp.Server.Telemetry;

/// <summary>
/// Healthy while the Discord gateway session is up (after Ready or Resume, until a Disconnect).
/// Registered as Degraded-on-failure, outside the readiness checks. A singleton, started as a hosted
/// service: a check created per run (the AddCheck<T> default) would miss the Ready event.
/// </summary>
public sealed class DiscordGatewayHealthCheck : IHealthCheck, IHostedService
{
    private volatile bool _connected;

    [ActivatorUtilitiesConstructor]
    public DiscordGatewayHealthCheck(GatewayClient client)
    {
        client.Ready += _ => SetConnected(true);
        client.Resume += () => SetConnected(true);
        client.Disconnect += _ => SetConnected(false);
    }

    public DiscordGatewayHealthCheck(bool connected)
    {
        _connected = connected;
    }

    public ValueTask SetConnected(bool connected)
    {
        _connected = connected;
        return ValueTask.CompletedTask;
    }

    // Hosted only so the singleton is created at startup, subscribed before the gateway connects.
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_connected
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "Discord gateway is not connected"));
}
