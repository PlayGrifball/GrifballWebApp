using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using GrifballWebApp.Server.Telemetry.Browser;
using Quartz.Diagnostics;
using GrifballWebApp.Infrastructure.Redis;

namespace GrifballWebApp.Server.Telemetry;

public static class MetricsAndTracingExtensions
{
    /// <summary>SignalR's hub method calls (ASP.NET Core 9 and later).</summary>
    public const string SignalRServerSource = "Microsoft.AspNetCore.SignalR.Server";

    /// <summary>Meters beyond the instrumentation packages' own.</summary>
    public static readonly string[] AdditionalMeters =
    [
        "Microsoft.EntityFrameworkCore",
        // dotnet.health_check.*, from AddTelemetryHealthCheckPublisher.
        "Microsoft.Extensions.Diagnostics.HealthChecks",
        // ASP.NET Core 10: sign-ins, challenges, authorization results.
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization",
        "Microsoft.AspNetCore.Identity",
        // grif.browser.*: the browser's Web Vitals (Browser/BrowserMetrics)
        BrowserMetrics.MeterName,
    ];

    /// <summary>
    /// Metrics and traces, pushed over OTLP to the collector when an endpoint is set; nothing is
    /// scraped from the app. Health probes are left out of the traces.
    /// </summary>
    public static IServiceCollection AddMetricsAndTracing(this IServiceCollection services, TelemetryOptions telemetry, IConfiguration configuration)
    {
        var healthPaths = HealthCheckPaths.FromConfiguration(configuration);
        services.AddOpenTelemetry()
            .ConfigureResource(resource => telemetry.ConfigureResource(resource))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddNpgsqlInstrumentation()
                    .AddMeter(AdditionalMeters)
                    // quartz.job.execution.*, trigger misfires and acquisition, store round trips
                    .AddMeter(QuartzInstrumentation.MeterName);

                if (telemetry.SignalEndpoint("metrics") is { } endpoint)
                {
                    metrics.AddOtlpExporter((exporter, reader) =>
                    {
                        ConfigureExporter(exporter, endpoint, telemetry.Protocol);
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)telemetry.MetricExportInterval.TotalMilliseconds;
                    });
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    // Not the health probes, or the browser's telemetry batches (every few seconds a visitor)
                    .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                        !healthPaths.IsHealthPath(context.Request.Path) && !ClientTelemetryExtensions.IsClientTelemetryPath(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddNpgsql()
                    .AddEntityFrameworkCoreInstrumentation()
                    // Redis commands (the SignalR backplane, the cache, the Halo calls' pace), on the app's one connection
                    // once it's made (AppRedis); none without Redis. No timing events: every Halo call's pacer EVAL would
                    // otherwise add 3 (CreationToEnqueued, EnqueuedToSent, SentToResponse) to its span.
                    .AddRedisInstrumentation(options => options.EnrichActivityWithTimingEvents = false)
                    .ConfigureRedisInstrumentation((provider, redis) =>
                        // Instrumented for the process's life: the registration AddConnection returns is never undone
                        provider.GetService<AppRedis>()?.WhenConnected(connection => _ = redis.AddConnection(connection)))
                    // A span per hub method call; each starts a trace of its own (a hub connection is no request to sit
                    // under). A hub connection's own SUBSCRIBE/UNSUBSCRIBE (the Redis backplane joining/leaving its
                    // group) attaches to that long-lived WebSocket request span instead, and only finishes when the
                    // connection closes.
                    .AddSource(SignalRServerSource)
                    // A trace per job run (JobTracing), without the scheduler loop's own store calls
                    .AddSource(QuartzInstrumentation.ActivitySourceName)
                    // Drops the scheduler loop's own spans and rootless Redis spans (SchedulerLoopSampler)
                    .SetSampler(new SchedulerLoopSampler());

                if (telemetry.SignalEndpoint("traces") is { } endpoint)
                {
                    tracing.AddOtlpExporter(exporter =>
                    {
                        ConfigureExporter(exporter, endpoint, telemetry.Protocol);
                        ConfigureTraceBatches(exporter.BatchExportProcessorOptions);
                    });
                }

                if (configuration.GetValue<bool>("OTLP_CONSOLE_EXPORTER_ENABLED"))
                {
                    tracing.AddConsoleExporter();
                }
            });

        return services;
    }

    /// <summary>
    /// Room for a long job's spans: a season match pull makes thousands a minute (each Halo call and save), and the SDK's
    /// queue of 2048, sent 512 every 5 seconds, dropped the rest, its run's own span (ended last) among them, so its trace
    /// had no root (GrifballWebApp#237). A bigger queue, sent every second; batches stay 512, so one export stays under
    /// the collector's 4 MiB gRPC message limit (EF Core spans carry their SQL).
    /// </summary>
    public static void ConfigureTraceBatches(BatchExportProcessorOptions<System.Diagnostics.Activity> batches)
    {
        batches.MaxQueueSize = 32768;
        batches.MaxExportBatchSize = 512;
        batches.ScheduledDelayMilliseconds = 1000;
    }

    public static void ConfigureExporter(OtlpExporterOptions exporter, Uri endpoint, OtlpWireProtocol protocol)
    {
        exporter.Endpoint = endpoint;
        exporter.Protocol = protocol == OtlpWireProtocol.Grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
    }
}
