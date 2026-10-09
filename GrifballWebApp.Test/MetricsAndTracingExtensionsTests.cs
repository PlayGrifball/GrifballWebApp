using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace GrifballWebApp.Test;

[TestFixture]
public class MetricsAndTracingExtensionsTests
{
    private static ServiceProvider Build(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        environment.ApplicationName.Returns("GrifballWebApp.Server");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(environment);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMetricsAndTracing(TelemetryOptions.FromConfiguration(configuration, environment), configuration);
        return services.BuildServiceProvider();
    }

    [TestCase(null, null, false)]
    [TestCase("http://127.0.0.1:9", null, false)]
    [TestCase("http://127.0.0.1:9", "http/protobuf", true)]
    public void Providers_BuildForEachConfiguration(string? endpoint, string? protocol, bool console)
    {
        using var provider = Build(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol,
            ["OTEL_METRIC_EXPORT_INTERVAL"] = "1000",
            ["OTLP_CONSOLE_EXPORTER_ENABLED"] = console ? "true" : null,
        });

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetService<MeterProvider>(), Is.Not.Null);
            Assert.That(provider.GetService<TracerProvider>(), Is.Not.Null);
        });
    }

    [TestCase(OtlpWireProtocol.Grpc, OtlpExportProtocol.Grpc)]
    [TestCase(OtlpWireProtocol.HttpProtobuf, OtlpExportProtocol.HttpProtobuf)]
    public void ConfigureExporter_SetsEndpointAndProtocol(OtlpWireProtocol protocol, OtlpExportProtocol expected)
    {
        var exporter = new OtlpExporterOptions();
        var endpoint = new Uri("http://collector:4317");

        MetricsAndTracingExtensions.ConfigureExporter(exporter, endpoint, protocol);

        Assert.Multiple(() =>
        {
            Assert.That(exporter.Endpoint, Is.EqualTo(endpoint));
            Assert.That(exporter.Protocol, Is.EqualTo(expected));
        });
    }

    [Test]
    public void AdditionalMeters_IncludeEfCoreAndHealthChecks()
    {
        Assert.That(MetricsAndTracingExtensions.AdditionalMeters, Is.SupersetOf(new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.Diagnostics.HealthChecks",
        }));
    }
}
