using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Serilog;
using System.Diagnostics;
using System.Net;
using static GrifballWebApp.Server.Test.RequestTelemetryUnitTests;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[NonParallelizable] // The pipeline test points the static Log.Logger, which Serilog request logging writes to, at its sink.
public class RequestTelemetryTests
{
    [Test]
    public async Task Pipeline_LogsOneLinePerRequest_WithTheRealClient_AndSkipsHealth()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16";
        builder.Configuration["ForwardedHeaders:ForwardedForHeaderName"] = "X-Real-IP";
        builder.Services.ConfigureAppForwardedHeaders(builder.Configuration);
        // Only the request log. UseSerilogRequestLogging writes to the static logger, as in Program.
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByIncludingOnly(Serilog.Filters.Matching.FromSource("Serilog.AspNetCore.RequestLoggingMiddleware"))
            .WriteTo.Sink(sink)
            .CreateLogger();
        try
        {
            builder.Services.AddSerilog();
            var telemetry = TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment);
            builder.Services.AddMetricsAndTracing(telemetry, builder.Configuration);

            await using var app = builder.Build();
            app.UseForwardedHeaders();
            app.UseRequestTelemetry();
            app.MapGet("/api/hello", () => "hi");
            app.MapGet("/health", () => "Healthy");
            await app.StartAsync();

            var server = app.GetTestServer();
            var response = await server.SendAsync(c =>
            {
                c.Request.Path = "/api/hello";
                c.Connection.RemoteIpAddress = IPAddress.Parse("10.42.1.245");
                c.Request.Headers["X-Real-IP"] = "68.41.224.97";
            });
            await server.SendAsync(c => c.Request.Path = "/health");

            // TestServer hands back the response before the middleware unwinds and writes its line.
            for (var i = 0; i < 100 && sink.Events.Count == 0; i++)
                await Task.Delay(50);
            await Task.Delay(100); // room for a wrongly logged /health line to show up too

            var requestLog = sink.Events.Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Response.Headers.ContainsKey(RequestTelemetryMiddleware.TraceResponseHeader), Is.True);
                Assert.That(requestLog.Properties["RequestPath"].ToString(), Is.EqualTo("\"/api/hello\""));
                Assert.That(requestLog.Properties["StatusCode"].ToString(), Is.EqualTo("200"));
                Assert.That(requestLog.Properties["ClientIp"].ToString(), Is.EqualTo("\"68.41.224.97\""));
                Assert.That(requestLog.TraceId?.ToHexString(), Is.EqualTo(response.Response.Headers["traceresponse"].ToString().Split('-')[1]));
            }
        }
        finally
        {
            (Log.Logger as IDisposable)?.Dispose();
            Log.Logger = previous;
        }
    }
}
