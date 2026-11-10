using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using static GrifballWebApp.Server.Test.LoggingExtensionsUnitTests;
using static GrifballWebApp.Server.Test.LoggingExtensionsTestData;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[NonParallelizable] // AddSerilogLogging replaces the static Log.Logger.
public class LoggingExtensionsTests
{
    [Test]
    public void ConfigureLogging_SinkFromConfiguration_ReceivesEvents_AlongsideTheCodeSinks()
    {
        var key = Guid.NewGuid().ToString("N");
        var configuration = Configuration(new(ConfiguredSinkSettings(key))
        {
            ["Serilog:Properties:Application"] = "grif",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:9",
        });
        var codeSink = new CollectingSink();

        using (var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration))
            .WriteTo.Sink(codeSink)
            .CreateLogger())
        {
            logger.Information("hello {Name}", "grif");
            logger.Debug("below the configured minimum");
        }

        var configured = ConfiguredSinkExtensions.Sink(key).Events;
        Assert.Multiple(() =>
        {
            Assert.That(configured.Select(e => e.MessageTemplate.Text), Is.EqualTo(new[] { "hello {Name}" }));
            Assert.That(configured[0].Properties["Application"].ToString(), Is.EqualTo("\"grif\""));
            Assert.That(codeSink.Events, Has.Count.EqualTo(1), "configuration adds sinks, it does not replace them");
        });
    }

    [TestCase("Development", "http://127.0.0.1:9", null)]
    [TestCase("Production", "http://127.0.0.1:9", null)]
    [TestCase("Production", "http://127.0.0.1:9", "http/protobuf")]
    public void ConfigureLogging_WithAnOtlpEndpoint_BuildsAWorkingLogger(string environment, string endpoint, string? protocol) =>
        AssertEachBranchBuildsAWorkingLogger(environment, endpoint, protocol);

    [Test]
    public void AddSerilogLogging_ReplacesTheLoggerProvider()
    {
        var key = Guid.NewGuid().ToString("N");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(ConfiguredSinkSettings(key));

        builder.AddSerilogLogging(TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment));

        using (var app = builder.Build())
        {
            var factory = app.Services.GetRequiredService<ILoggerFactory>();
            Assert.That(factory.GetType().FullName, Does.Contain("Serilog"));
            factory.CreateLogger("test").LogInformation("hello");
        }

        Assert.That(ConfiguredSinkExtensions.Sink(key).Events.Select(e => e.MessageTemplate.Text), Does.Contain("hello"));
    }
}

