using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using static GrifballWebApp.Server.Test.MetricsAndTracingExtensionsTestData;

namespace GrifballWebApp.Server.Test;

[TestFixture]
public class MetricsAndTracingExtensionsTests
{
    private static readonly string[] EfCoreAndHealthChecks = ["Microsoft.EntityFrameworkCore", "Microsoft.Extensions.Diagnostics.HealthChecks"];

    [Test]
    public void Providers_BuildWithoutAnOtlpEndpoint() => AssertProvidersBuild(null, null, false);

    [TestCase(OtlpWireProtocol.Grpc, OtlpExportProtocol.Grpc)]
    [TestCase(OtlpWireProtocol.HttpProtobuf, OtlpExportProtocol.HttpProtobuf)]
    public void ConfigureExporter_SetsEndpointAndProtocol(OtlpWireProtocol protocol, OtlpExportProtocol expected)
    {
        var exporter = new OtlpExporterOptions();
        var endpoint = new Uri("http://collector:4317");

        MetricsAndTracingExtensions.ConfigureExporter(exporter, endpoint, protocol);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exporter.Endpoint, Is.EqualTo(endpoint));
            Assert.That(exporter.Protocol, Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task Redis_IsTraced_OnTheAppsConnection_OnceItIsMade()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        await using var redis = new AppRedis(_ => Task.FromResult(multiplexer));
        using var provider = Build(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null }, services => services.AddSingleton(redis));

        Assert.That(provider.GetService<TracerProvider>(), Is.Not.Null);
        multiplexer.DidNotReceiveWithAnyArgs().RegisterProfiler(default!);

        await redis.ConnectAsync();

        multiplexer.ReceivedWithAnyArgs(1).RegisterProfiler(default!);
    }

    [Test]
    public void Tracing_BuildsWithoutRedis()
    {
        using var provider = Build(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null });

        Assert.That(provider.GetService<TracerProvider>(), Is.Not.Null);
    }

    [Test]
    public void SignalRServerSource_IsAspNetCores()
    {
        Assert.That(MetricsAndTracingExtensions.SignalRServerSource, Is.EqualTo("Microsoft.AspNetCore.SignalR.Server"));
    }

    [Test]
    public void AdditionalMeters_IncludeEfCoreAndHealthChecks()
    {
        Assert.That(MetricsAndTracingExtensions.AdditionalMeters, Is.SupersetOf(EfCoreAndHealthChecks));
    }
}

/// <summary>MetricsAndTracingExtensionsTests' helpers, shared with its integration tests.</summary>
internal static class MetricsAndTracingExtensionsTestData
{
    internal static ServiceProvider Build(Dictionary<string, string?> values, Action<IServiceCollection>? more = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        environment.ApplicationName.Returns("GrifballWebApp.Server");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(environment);
        services.AddSingleton<IConfiguration>(configuration);
        more?.Invoke(services);
        services.AddMetricsAndTracing(TelemetryOptions.FromConfiguration(configuration, environment), configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>The meter and tracer providers build for this configuration (an endpoint here is exported to when they are
    /// disposed: GrifballWebApp.Integration.Test's MetricsAndTracingExportTests).</summary>
    internal static void AssertProvidersBuild(string? endpoint, string? protocol, bool console)
    {
        using var provider = Build(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol,
            ["OTEL_METRIC_EXPORT_INTERVAL"] = "1000",
            ["OTLP_CONSOLE_EXPORTER_ENABLED"] = console ? "true" : null,
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.GetService<MeterProvider>(), Is.Not.Null);
            Assert.That(provider.GetService<TracerProvider>(), Is.Not.Null);
        }
    }
}
