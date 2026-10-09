using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GrifballWebApp.Test;

[TestFixture]
[NonParallelizable] // AddSerilogLogging replaces the static Log.Logger.
public class LoggingExtensionsTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        environment.ApplicationName.Returns("GrifballWebApp.Server");
        return environment;
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static TelemetryOptions Telemetry(IConfiguration configuration, string environment = "Production") =>
        TelemetryOptions.FromConfiguration(configuration, Environment(environment));

    // What the grif manifests set before this change; Serilog.Sinks.Grafana.Loki is no longer shipped.
    private static readonly Dictionary<string, string?> OldLokiSinkSettings = new()
    {
        ["Serilog:MinimumLevel:Default"] = "Information",
        ["Serilog:Using:1"] = "Serilog.Sinks.Grafana.Loki",
        ["Serilog:WriteTo:1:Name"] = "GrafanaLoki",
        ["Serilog:WriteTo:1:Args:uri"] = "http://loki-gateway.loki.svc.cluster.local",
    };

    [Test]
    public void CreateBootstrapLogger_WritesWithoutThrowing()
    {
        var logger = LoggingExtensions.CreateBootstrapLogger();
        Assert.DoesNotThrow(() => logger.Information("Starting up"));
        logger.Dispose();
    }

    [Test]
    public void SerilogSettingsOnly_KeepsLevelsAndDropsSinks()
    {
        var settings = LoggingExtensions.SerilogSettingsOnly(Configuration(new(OldLokiSinkSettings)
        {
            ["Serilog:MinimumLevel:Override:GrifballWebApp"] = "Debug",
            ["Serilog:Properties:Application"] = "grif",
        }));

        Assert.Multiple(() =>
        {
            Assert.That(settings["Serilog:MinimumLevel:Default"], Is.EqualTo("Information"));
            Assert.That(settings["Serilog:MinimumLevel:Override:GrifballWebApp"], Is.EqualTo("Debug"));
            Assert.That(settings["Serilog:Properties:Application"], Is.EqualTo("grif"));
            Assert.That(settings["Serilog:Using:1"], Is.Null);
            Assert.That(settings["Serilog:WriteTo:1:Name"], Is.Null);
        });
    }

    [Test]
    public void ConfigureLogging_OldLokiSinkSettings_DoNotStopStartup()
    {
        var configuration = Configuration(OldLokiSinkSettings);

        using var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration))
            .CreateLogger();

        Assert.DoesNotThrow(() => logger.Information("still running"));
    }

    [TestCase("Development", null, null)]
    [TestCase("Production", null, null)]
    [TestCase("Production", "http://127.0.0.1:9", null)]
    [TestCase("Production", "http://127.0.0.1:9", "http/protobuf")]
    public void ConfigureLogging_EachBranchBuildsAWorkingLogger(string environment, string? endpoint, string? protocol)
    {
        var configuration = Configuration(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Information",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol,
        });
        var sink = new CollectingSink();

        using var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment(environment), Telemetry(configuration, environment))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("hello {Name}", "grif");
        logger.Debug("filtered by the configured minimum level");

        Assert.That(sink.Events.Select(e => e.MessageTemplate.Text), Is.EqualTo(new[] { "hello {Name}" }));
    }

    [Test]
    public void ConfigureLogging_ReadsLevelsFromConfiguration_AndEnrichesFromLogContext()
    {
        var configuration = Configuration(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:GrifballWebApp"] = "Debug",
        });
        var sink = new CollectingSink();

        using var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration), new ServiceCollection().BuildServiceProvider())
            .WriteTo.Sink(sink)
            .CreateLogger();

        using (Serilog.Context.LogContext.PushProperty("ClientIp", "203.0.113.7"))
        {
            logger.ForContext(Constants.SourceContextPropertyName, "GrifballWebApp.Server.Thing").Debug("app debug");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Information("framework info");
        }

        Assert.That(sink.Events, Has.Count.EqualTo(1));
        Assert.That(sink.Events[0].Properties["ClientIp"].ToString(), Is.EqualTo("\"203.0.113.7\""));
    }

    [Test]
    public void AddSerilogLogging_ReplacesTheLoggerProvider()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(OldLokiSinkSettings);

        builder.AddSerilogLogging(TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment));

        using var app = builder.Build();
        var factory = app.Services.GetRequiredService<ILoggerFactory>();

        Assert.That(factory.GetType().FullName, Does.Contain("Serilog"));
        Assert.DoesNotThrow(() => factory.CreateLogger("test").LogInformation("hello"));
    }
}
