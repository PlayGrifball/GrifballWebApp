using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using static GrifballWebApp.Server.Test.LoggingExtensionsTestData;

namespace GrifballWebApp.Server.Test;

/// <summary>LoggingExtensionsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[NonParallelizable] // AddSerilogLogging replaces the static Log.Logger.
public class LoggingExtensionsUnitTests
{
    private static readonly string[] ExpectedLogged = ["app debug", "framework error"];
    [Test]
    public void CreateBootstrapLogger_WritesWithoutThrowing()
    {
        var logger = LoggingExtensions.CreateBootstrapLogger();
        Assert.DoesNotThrow(() => logger.Information("Starting up"));
        logger.Dispose();
    }

    [Test]
    public void ConfigureLogging_SinkFromConfiguration_HonoursMinimumLevelOverrides()
    {
        var key = Guid.NewGuid().ToString("N");
        var configuration = Configuration(new(ConfiguredSinkSettings(key))
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:GrifballWebApp"] = "Debug",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Error",
        });

        using (var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), LoggingExtensionsTestData.Telemetry(configuration))
            .CreateLogger())
        {
            logger.ForContext(Constants.SourceContextPropertyName, "GrifballWebApp.Server.Thing").Debug("app debug");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Warning("framework warning");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Error("framework error");
            logger.ForContext(Constants.SourceContextPropertyName, "NetCord").Information("other info");
        }

        Assert.That(ConfiguredSinkExtensions.Sink(key).Events.Select(e => e.MessageTemplate.Text),
            Is.EqualTo(ExpectedLogged));
    }

    [Test]
    public void ConfigureLogging_UsingAnAssemblyThatIsNotShipped_StopsStartup()
    {
        // Why homelab's grif manifests had to lose the Serilog.Sinks.Grafana.Loki settings before this image.
        var configuration = Configuration(new()
        {
            ["Serilog:Using:1"] = "Serilog.Sinks.Grafana.Loki",
            ["Serilog:WriteTo:1:Name"] = "GrafanaLoki",
            ["Serilog:WriteTo:1:Args:uri"] = "http://loki-gateway.loki.svc.cluster.local",
        });

        Assert.That(() => new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), LoggingExtensionsTestData.Telemetry(configuration))
            .CreateLogger(), Throws.Exception);
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
            .ConfigureLogging(configuration, Environment("Production"), LoggingExtensionsTestData.Telemetry(configuration), new ServiceCollection().BuildServiceProvider())
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
    public void AddSerilogLogging_TheAppsLoggersWriteThroughTheConfiguredSerilog()
    {
        var key = Guid.NewGuid().ToString("N");
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateEmptyBuilder(new());
        builder.Configuration.AddInMemoryCollection(ConfiguredSinkSettings(key));

        builder.AddSerilogLogging(LoggingExtensionsTestData.Telemetry(builder.Configuration));
        // The builder's services alone: no host is built or started
        using (var services = builder.Services.BuildServiceProvider())
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger("GrifballWebApp.Server.Thing").LogInformation("Through the host");
        }

        Assert.That(ConfiguredSinkExtensions.Sink(key).Events.Select(e => e.MessageTemplate.Text), Does.Contain("Through the host"));
    }

    [TestCase("Development")]
    [TestCase("Production")]
    public void ConfigureLogging_EachBranchBuildsAWorkingLogger(string environment) =>
        AssertEachBranchBuildsAWorkingLogger(environment, null, null);

}

/// <summary>A sink Serilog.Settings.Configuration can find by name; events land in <see cref="Sink"/>(key).</summary>
public static class ConfiguredSinkExtensions
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CollectingEventSink> Sinks = new();

    public static CollectingEventSink Sink(string key) => Sinks.GetOrAdd(key, _ => new CollectingEventSink());

    public static LoggerConfiguration Configured(this Serilog.Configuration.LoggerSinkConfiguration writeTo, string key) =>
        writeTo.Sink(Sink(key));
}

public sealed class CollectingEventSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events
    {
        get { lock (_events) return [.. _events]; }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_events) _events.Add(logEvent);
    }
}

/// <summary>LoggingExtensionsTests' helpers, shared with its integration tests.</summary>
internal static class LoggingExtensionsTestData
{
    internal static readonly string[] ExpectedEvents = ["hello {Name}"];

    internal sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    internal static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        environment.ApplicationName.Returns("GrifballWebApp.Server");
        return environment;
    }

    internal static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    internal static TelemetryOptions Telemetry(IConfiguration configuration, string environment = "Production") =>
        TelemetryOptions.FromConfiguration(configuration, Environment(environment));

    /// <summary>
    /// A sink added the way an environment variable would add one: <c>Serilog:Using</c> names this
    /// assembly and <c>Serilog:WriteTo</c> the <see cref="ConfiguredSinkExtensions.Configured"/> method.
    /// </summary>
    internal static Dictionary<string, string?> ConfiguredSinkSettings(string key) => new()
    {
        ["Serilog:MinimumLevel:Default"] = "Information",
        ["Serilog:Using:0"] = typeof(ConfiguredSinkExtensions).Assembly.GetName().Name,
        ["Serilog:WriteTo:0:Name"] = nameof(ConfiguredSinkExtensions.Configured),
        ["Serilog:WriteTo:0:Args:key"] = key,
    };

    /// <summary>The logger <see cref="LoggingExtensions.ConfigureLogging"/> builds for this environment and OTLP endpoint writes
    /// what it is given and leaves out what is below the configured minimum level.</summary>
    internal static void AssertEachBranchBuildsAWorkingLogger(string environment, string? endpoint, string? protocol)
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

        Assert.That(sink.Events.Select(e => e.MessageTemplate.Text), Is.EqualTo(ExpectedEvents));
    }
}
