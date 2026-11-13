using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// UseRequestTelemetry in a pipeline: each request logged once by Serilog's request logging, at the level and with the
/// properties RequestTelemetryExtensions gives it. The pipeline built over services with an ApplicationBuilder, its Serilog writing to a list; no host or server.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RequestTelemetryPipelineUnitTests
{
    [TestCase(StatusCodes.Status200OK, LogEventLevel.Information)]
    [TestCase(StatusCodes.Status503ServiceUnavailable, LogEventLevel.Error)]
    public async Task UseRequestTelemetry_LogsEachRequest_AtItsLevel_WithItsHost(int status, LogEventLevel level)
    {
        var sink = new LoggingExtensionsTestData.CollectingSink();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSerilog(new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger(), dispose: true)
            .BuildServiceProvider();
        // The pipeline built over the services, as the app builds it; no host
        var app = new ApplicationBuilder(services);
        app.UseRequestTelemetry();
        app.Run(context =>
        {
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var request = new DefaultHttpContext { RequestServices = services };
        request.Request.Method = "GET";
        request.Request.Path = "/seasons";
        request.Request.Host = new HostString("grif.test");

        await pipeline(request);

        var logged = sink.Events.Single(e => e.Properties.ContainsKey("StatusCode"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logged.Level, Is.EqualTo(level));
            Assert.That(logged.Properties["RequestPath"].ToString(), Is.EqualTo("\"/seasons\""));
            Assert.That(logged.Properties["RequestHost"].ToString(), Is.EqualTo("\"grif.test\""));
        }
    }
}
