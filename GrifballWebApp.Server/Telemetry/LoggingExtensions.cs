using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Settings.Configuration;
using Serilog.Sinks.OpenTelemetry;

namespace GrifballWebApp.Server.Telemetry;

public static class LoggingExtensions
{
    /// <summary>Configuration keys under <c>Serilog</c> that are read; sinks are code-owned (see <see cref="ConfigureLogging"/>).</summary>
    private static readonly string[] ConfigurableSections = ["MinimumLevel", "Properties", "Enrich"];

    /// <summary>
    /// The logger used until the host is built, and for a crash before that: console only, compact JSON.
    /// </summary>
    public static Serilog.Extensions.Hosting.ReloadableLogger CreateBootstrapLogger() => new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter())
        .CreateBootstrapLogger();

    /// <summary>Replaces the bootstrap logger with the configured one once the host is built.</summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder, TelemetryOptions telemetry)
    {
        builder.Services.AddSerilog((services, configuration) => configuration
            .ConfigureLogging(builder.Configuration, builder.Environment, telemetry, services));
        return builder;
    }

    /// <summary>
    /// Compact JSON on the console in every environment (what kubectl logs and Alloy see), plus the debug
    /// output in Development; when an endpoint is set, OTLP to the collector with the same resource as
    /// the metrics and traces (in Development too, e.g. a local Aspire dashboard). Each event carries its
    /// trace and span id natively, so local output shows them as @tr and @sp.
    /// </summary>
    /// <remarks>
    /// Only <c>Serilog:MinimumLevel</c>, <c>Serilog:Properties</c> and <c>Serilog:Enrich</c> are read from
    /// configuration. <c>Serilog:Using</c> and <c>Serilog:WriteTo</c> are ignored on purpose: sinks are
    /// defined here, and a leftover <c>Using</c> naming an assembly that is no longer shipped (the old
    /// Grafana Loki sink) would otherwise stop the app at startup.
    /// </remarks>
    public static LoggerConfiguration ConfigureLogging(this LoggerConfiguration logger, IConfiguration configuration,
        IHostEnvironment environment, TelemetryOptions telemetry, IServiceProvider? services = null)
    {
        logger.ReadFrom.Configuration(SerilogSettingsOnly(configuration), new ConfigurationReaderOptions { SectionName = "Serilog" });
        if (services is not null)
            logger.ReadFrom.Services(services);

        logger.Enrich.FromLogContext();

        // JSON everywhere, Development included, so every line carries its trace and span ids (@tr, @sp).
        var json = new RenderedCompactJsonFormatter();
        logger.WriteTo.Console(json);
        if (environment.IsDevelopment())
            logger.WriteTo.Debug(json);

        var endpoint = telemetry.SignalEndpoint("logs");
        if (endpoint is not null)
        {
            logger.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = endpoint.AbsoluteUri;
                options.Protocol = telemetry.Protocol == OtlpWireProtocol.Grpc ? OtlpProtocol.Grpc : OtlpProtocol.HttpProtobuf;
                options.ResourceAttributes = new Dictionary<string, object>(telemetry.ResourceAttributes);
                // The sink's own export requests would otherwise show up as HttpClient spans.
                options.OnBeginSuppressInstrumentation = OpenTelemetry.SuppressInstrumentationScope.Begin;
            }, ignoreEnvironment: true);
        }

        return logger;
    }

    /// <summary>A copy of the configuration holding only the <see cref="ConfigurableSections"/> of <c>Serilog</c>.</summary>
    public static IConfiguration SerilogSettingsOnly(IConfiguration configuration)
    {
        var serilog = configuration.GetSection("Serilog");
        var values = ConfigurableSections
            .SelectMany(name => serilog.GetSection(name).AsEnumerable())
            .Where(kv => kv.Value is not null);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
