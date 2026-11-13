using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrifballWebApp.Server.Telemetry.Query;

/// <summary>
/// Where the Telemetry page reads this app's own logs, metrics and traces back from (TelemetryQuery:*): the HTTP APIs of
/// Loki, Prometheus and Tempo. Each one unset turns its part of the page off.
/// </summary>
public sealed class TelemetryQueryOptions
{
    public const string Section = "TelemetryQuery";

    /// <summary>Loki (or its gateway), ex. <c>http://loki-gateway.loki</c>.</summary>
    public string? LokiUrl { get; set; }
    /// <summary>Prometheus or anything with its query API (Thanos), ex. <c>http://prometheus.monitoring:9090</c>.</summary>
    public string? PrometheusUrl { get; set; }
    /// <summary>Tempo's HTTP port, ex. <c>http://tempo.tempo:3200</c>.</summary>
    public string? TempoUrl { get; set; }

    /// <summary>
    /// The services in front of this app whose spans a trace shows with its own, comma-separated service.names: the ingress
    /// (ex. <c>traefik</c>) and Cloudflare's (<c>edge_processing,origin_fetch</c>). Only in a trace that has this app's spans.
    /// </summary>
    public string? EdgeServices { get; set; }

    /// <summary>service.name (<see cref="TelemetryOptions.ServiceName"/>, not bound): every query is scoped to it.</summary>
    public string ServiceName { get; set; } = "";

    public Uri? Loki => TelemetryOptions.ParseEndpoint(LokiUrl);
    public Uri? Prometheus => TelemetryOptions.ParseEndpoint(PrometheusUrl);
    public Uri? Tempo => TelemetryOptions.ParseEndpoint(TempoUrl);

    /// <summary><see cref="EdgeServices"/>, each one.</summary>
    public IReadOnlySet<string> EdgeServiceNames =>
        (EdgeServices ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
}

public static class TelemetryQueryServiceCollectionExtensions
{
    /// <summary>The Telemetry page: its options (scoped to <paramref name="serviceName"/>) and the client that queries the backends.</summary>
    public static IServiceCollection AddTelemetryQuery(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.AddOptions<TelemetryQueryOptions>()
            .Bind(configuration.GetSection(TelemetryQueryOptions.Section))
            .PostConfigure(o => o.ServiceName = serviceName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<TelemetryQueryService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 32 * 1024 * 1024;
        });
        return services;
    }
}
