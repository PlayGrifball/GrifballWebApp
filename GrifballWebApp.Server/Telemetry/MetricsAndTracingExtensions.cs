using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Quartz.Diagnostics;

namespace GrifballWebApp.Server.Telemetry;

public static class MetricsAndTracingExtensions
{
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
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => !healthPaths.IsHealthPath(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddNpgsql()
                    .AddEntityFrameworkCoreInstrumentation()
                    // A trace per job run (JobTracing), without the scheduler loop's own store calls
                    .AddSource(QuartzInstrumentation.ActivitySourceName)
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
    /// had no root (GrifballWebApp#237).
    /// </summary>
    public static void ConfigureTraceBatches(BatchExportProcessorOptions<System.Diagnostics.Activity> batches)
    {
        batches.MaxQueueSize = 32768;
        batches.MaxExportBatchSize = 4096;
        batches.ScheduledDelayMilliseconds = 2000;
    }

    public static void ConfigureExporter(OtlpExporterOptions exporter, Uri endpoint, OtlpWireProtocol protocol)
    {
        exporter.Endpoint = endpoint;
        exporter.Protocol = protocol == OtlpWireProtocol.Grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
    }
}
