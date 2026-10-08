using GrifballWebApp.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GrifballWebApp.Server.Telemetry;

public static class HealthCheckExtensions
{
    /// <summary>Checks that must pass before the pod receives traffic.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Fallback when <c>HealthChecks:MaxAllocatedMegabytes</c> is unset (appsettings.json sets the same value;
    /// deployments set it per container limit): below a 512Mi limit, above grif's ~240Mi peak.
    /// </summary>
    public const int DefaultMaxAllocatedMegabytes = 384;

    /// <summary>Whether the request is for a health endpoint, at the paths <see cref="AddAppHealthChecks"/> registered.</summary>
    public static bool IsHealthPath(HttpContext context) => HealthCheckPaths.From(context.RequestServices).IsHealthPath(context.Request.Path);

    /// <summary>
    /// <list type="bullet">
    /// <item><b>ready</b>: the database, and the host having started and not begun stopping.</item>
    /// <item><b>not gating</b>: the Discord gateway, the Halo Infinite API (probed at most every few minutes,
    /// see <see cref="HaloInfiniteHealthCheck"/>) and managed memory, reported as Degraded only. A Discord or
    /// Halo outage must not take the website out of service.</item>
    /// </list>
    /// Every result is also published as <c>dotnet.health_check.*</c> metrics every 30 s.
    /// With <paramref name="externalServices"/> false (preview mode, which runs without Discord or Halo
    /// Infinite: no gateway client to watch, no credentials to probe with), the Discord and Halo checks
    /// aren't registered at all.
    /// </summary>
    public static IHealthChecksBuilder AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration, bool externalServices = true)
    {
        var maxAllocated = configuration.GetValue("HealthChecks:MaxAllocatedMegabytes", DefaultMaxAllocatedMegabytes);
        services.TryAddSingleton(HealthCheckPaths.FromConfiguration(configuration));

        var checks = services.AddHealthChecks()
            .AddApplicationLifecycleHealthCheck(ReadyTag)
            .AddDbContextCheck<GrifballContext>("database", HealthStatus.Unhealthy, [ReadyTag]);

        if (externalServices)
        {
            // Needs the Discord gateway client, which preview mode doesn't register.
            services.TryAddSingleton<DiscordGatewayHealthCheck>();
            services.AddHostedService(sp => sp.GetRequiredService<DiscordGatewayHealthCheck>());
            // A singleton, so the cached probe result outlives each run.
            services.TryAddSingleton(sp => HaloInfiniteHealthCheck.FromConfiguration(sp, configuration));

            checks
                .AddCheck<DiscordGatewayHealthCheck>("discord", HealthStatus.Degraded)
                .AddCheck<HaloInfiniteHealthCheck>("halo_infinite", HealthStatus.Degraded);
        }

        checks.AddProcessAllocatedMemoryHealthCheck(maxAllocated, "memory", HealthStatus.Degraded);

        services.AddTelemetryHealthCheckPublisher(options => options.LogOnlyUnhealthy = true);
        return checks;
    }

    /// <summary>
    /// <c>/health/live</c> runs no checks: it answers while the process can serve requests, so a database
    /// outage never restarts the pod. <c>/health/ready</c> runs the <see cref="ReadyTag"/> checks.
    /// <c>/health</c> runs all of them. All three return only the status word, never details: the
    /// frontend proxies <c>/api/*</c> here, so they are reachable from the internet. Those are the
    /// default paths; see <see cref="HealthCheckPaths"/> to move them.
    /// </summary>
    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        var paths = endpoints.ServiceProvider.GetService<HealthCheckPaths>()
            ?? HealthCheckPaths.FromConfiguration(endpoints.ServiceProvider.GetRequiredService<IConfiguration>());
        endpoints.MapHealthChecks(paths.Live, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(paths.Ready, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        endpoints.MapHealthChecks(paths.Health);
        return endpoints;
    }
}
