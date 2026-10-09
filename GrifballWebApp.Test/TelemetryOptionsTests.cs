using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenTelemetry.Resources;

namespace GrifballWebApp.Test;

[TestFixture]
public class TelemetryOptionsTests
{
    private static IHostEnvironment Environment(string name = "Production")
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        environment.ApplicationName.Returns("GrifballWebApp.Server");
        return environment;
    }

    private static TelemetryOptions From(Dictionary<string, string?> values, string? version = null, string environment = "Production") =>
        TelemetryOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), Environment(environment), version);

    [Test]
    public void Defaults_WithNothingConfigured_ExportOff()
    {
        var options = From(new() { ["HOSTNAME"] = "grif-backend-abc" });

        Assert.Multiple(() =>
        {
            Assert.That(options.ServiceName, Is.EqualTo("GrifballWebApp.Server"));
            Assert.That(options.Environment, Is.EqualTo("production"));
            Assert.That(options.ServiceInstanceId, Is.EqualTo("grif-backend-abc"));
            Assert.That(options.ServiceVersion, Is.Null);
            Assert.That(options.OtlpEndpoint, Is.Null);
            Assert.That(options.ExportEnabled, Is.False);
            Assert.That(options.Protocol, Is.EqualTo(OtlpWireProtocol.Grpc));
            Assert.That(options.MetricExportInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.SignalEndpoint("traces"), Is.Null);
            Assert.That(options.ResourceAttributes.ContainsKey("service.version"), Is.False);
        });
    }

    [Test]
    public void InstanceId_FallsBackToMachineName()
    {
        Assert.That(From(new()).ServiceInstanceId, Is.EqualTo(System.Environment.MachineName));
    }

    [Test]
    public void StandardVariables_SetNameEnvironmentEndpointAndResource()
    {
        var options = From(new()
        {
            ["OTEL_SERVICE_NAME"] = "grif-prod",
            ["OTLP_RESOURCE_NAME"] = "ignored",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "deployment.environment.name=prod, team=grif%20ball,broken,=novalue,empty=",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://otel-collector.opentelemetry.svc.cluster.local:4317",
            ["OTLP_ENDPOINT_URL"] = "http://ignored:4317",
            ["HOSTNAME"] = "grif-backend-1",
        }, version: "abc1234");

        Assert.Multiple(() =>
        {
            Assert.That(options.ServiceName, Is.EqualTo("grif-prod"));
            Assert.That(options.Environment, Is.EqualTo("prod"));
            Assert.That(options.ServiceVersion, Is.EqualTo("abc1234"));
            Assert.That(options.OtlpEndpoint, Is.EqualTo(new Uri("http://otel-collector.opentelemetry.svc.cluster.local:4317")));
            Assert.That(options.ResourceAttributes, Is.EquivalentTo(new Dictionary<string, object>
            {
                ["service.name"] = "grif-prod",
                ["service.instance.id"] = "grif-backend-1",
                ["service.version"] = "abc1234",
                ["deployment.environment.name"] = "prod",
                ["team"] = "grif ball",
            }));
        });
    }

    [Test]
    public void LegacyVariables_StillWork()
    {
        var options = From(new()
        {
            ["OTLP_RESOURCE_NAME"] = "grif-test",
            ["OTLP_ENDPOINT_URL"] = "http://otel-collector:4317",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "deployment.environment=test",
        });

        Assert.Multiple(() =>
        {
            Assert.That(options.ServiceName, Is.EqualTo("grif-test"));
            Assert.That(options.OtlpEndpoint, Is.EqualTo(new Uri("http://otel-collector:4317")));
            Assert.That(options.Environment, Is.EqualTo("test"));
            // The deprecated key is normalised to the current one, not sent twice.
            Assert.That(options.ResourceAttributes.ContainsKey("deployment.environment"), Is.False);
            Assert.That(options.ResourceAttributes["deployment.environment.name"], Is.EqualTo("test"));
        });
    }

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("not a uri", null)]
    [TestCase("ftp://collector:4317", null)]
    [TestCase("https://collector:4318", "https://collector:4318/")]
    public void ParseEndpoint(string? value, string? expected)
    {
        Assert.That(TelemetryOptions.ParseEndpoint(value)?.AbsoluteUri, Is.EqualTo(expected));
    }

    [TestCase(null, OtlpWireProtocol.Grpc)]
    [TestCase("grpc", OtlpWireProtocol.Grpc)]
    [TestCase(" HTTP/Protobuf ", OtlpWireProtocol.HttpProtobuf)]
    [TestCase("http/json", OtlpWireProtocol.Grpc)]
    public void ParseProtocol(string? value, OtlpWireProtocol expected)
    {
        Assert.That(TelemetryOptions.ParseProtocol(value), Is.EqualTo(expected));
    }

    [TestCase(null, 30_000)]
    [TestCase("not a number", 30_000)]
    [TestCase("NaN", 30_000)]
    [TestCase("1000", 5_000)]
    [TestCase("-5", 5_000)]
    [TestCase("15000", 15_000)]
    [TestCase("600000", 300_000)]
    [TestCase("Infinity", 300_000)]
    public void MetricExportInterval_IsClampedToFiveSecondsToFiveMinutes(string? value, double expectedMilliseconds)
    {
        Assert.That(TelemetryOptions.ParseMetricExportInterval(value).TotalMilliseconds, Is.EqualTo(expectedMilliseconds));
    }

    [Test]
    public void MetricExportInterval_FromConfiguration()
    {
        Assert.That(From(new() { ["OTEL_METRIC_EXPORT_INTERVAL"] = "10000" }).MetricExportInterval, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void SignalEndpoint_Grpc_IsTheBaseEndpoint()
    {
        var options = From(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317" });
        Assert.That(options.SignalEndpoint("logs"), Is.EqualTo(new Uri("http://collector:4317")));
    }

    [Test]
    public void SignalEndpoint_HttpProtobuf_AppendsTheSignalPath()
    {
        var options = From(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4318/",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
        });

        Assert.Multiple(() =>
        {
            Assert.That(options.SignalEndpoint("traces"), Is.EqualTo(new Uri("http://collector:4318/v1/traces")));
            Assert.That(options.SignalEndpoint("metrics"), Is.EqualTo(new Uri("http://collector:4318/v1/metrics")));
            Assert.That(options.SignalEndpoint("logs"), Is.EqualTo(new Uri("http://collector:4318/v1/logs")));
        });
    }

    [Test]
    public void ConfigureResource_MatchesTheLogResource()
    {
        var options = From(new()
        {
            ["OTEL_SERVICE_NAME"] = "grif-staging",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "deployment.environment.name=staging",
            ["HOSTNAME"] = "pod-1",
        }, version: "deadbee");

        var resource = options.ConfigureResource(ResourceBuilder.CreateEmpty()).Build();
        var attributes = resource.Attributes.ToDictionary(a => a.Key, a => a.Value);

        Assert.That(attributes, Is.EquivalentTo(options.ResourceAttributes));
    }

    [Test]
    public void ParseResourceAttributes_Blank_IsEmpty()
    {
        Assert.That(TelemetryOptions.ParseResourceAttributes("  "), Is.Empty);
    }
}
