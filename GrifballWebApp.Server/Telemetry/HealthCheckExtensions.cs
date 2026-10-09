using GrifballWebApp.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GrifballWebApp.Server.Telemetry;

public static class HealthCheckExtensions
{
    /// <summary>Checks that must pass before the pod receives traffic.</summary>
    public const string ReadyTag = "ready";

    public const string HealthPath = "/health";
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>Default for <c>HealthChecks:MaxAllocatedMegabytes</c>: below the 512Mi container limit, above the ~240Mi peak.</summary>
    public const int DefaultMaxAllocatedMegabytes = 384;

    public static bool IsHealthPath(PathString path) => path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <list type="bullet">
    /// <item><b>ready</b>: the database, and the host having started and not begun stopping.</item>
    /// <item><b>not gating</b>: the Discord gateway and managed memory, reported as Degraded only. A Discord
    /// outage must not take the website out of service.</item>
    /// </list>
    /// Every result is also published as <c>dotnet.health_check.*</c> metrics every 30 s.
    /// </summary>
    public static IHealthChecksBuilder AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var maxAllocated = configuration.GetValue("HealthChecks:MaxAllocatedMegabytes", DefaultMaxAllocatedMegabytes);

        services.TryAddSingleton<DiscordGatewayHealthCheck>();
        services.AddHostedService(sp => sp.GetRequiredService<DiscordGatewayHealthCheck>());

        var checks = services.AddHealthChecks()
            .AddApplicationLifecycleHealthCheck(ReadyTag)
            .AddDbContextCheck<GrifballContext>("database", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<DiscordGatewayHealthCheck>("discord", HealthStatus.Degraded)
            .AddProcessAllocatedMemoryHealthCheck(maxAllocated, "memory", HealthStatus.Degraded);

        services.AddTelemetryHealthCheckPublisher(options => options.LogOnlyUnhealthy = true);
        return checks;
    }

    /// <summary>
    /// <c>/health/live</c> runs no checks: it answers while the process can serve requests, so a database
    /// outage never restarts the pod. <c>/health/ready</c> runs the <see cref="ReadyTag"/> checks.
    /// <c>/health</c> runs all of them. All three return only the status word, never details: the
    /// frontend proxies <c>/api/*</c> here, so they are reachable from the internet.
    /// </summary>
    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        endpoints.MapHealthChecks(HealthPath);
        return endpoints;
    }
}
