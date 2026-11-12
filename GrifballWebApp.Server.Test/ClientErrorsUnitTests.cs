using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;

namespace GrifballWebApp.Server.Test;

/// <summary>The browser's error reports: logged in the trace they name, off unless enabled, limited only while signed out.</summary>
[TestFixture]
[NonParallelizable] // The ActivityListener sees every ClientErrors activity in the process.
public class ClientErrorsUnitTests
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string Traceparent = $"00-{TraceId}-b7ad6b7169203331-01";
    private static readonly int[] TwoThenRefused = [204, 204, 429];

    private RequestTelemetryUnitTests.CollectingSink _sink = null!;
    private Serilog.Core.Logger _serilog = null!;
    private ActivityListener _listener = null!;
    private readonly List<Activity> _activities = [];

    [SetUp]
    public void SetUp()
    {
        _sink = new RequestTelemetryUnitTests.CollectingSink();
        _serilog = new LoggerConfiguration().MinimumLevel.Verbose().Enrich.FromLogContext().WriteTo.Sink(_sink).CreateLogger();
        _activities.Clear();
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ClientErrorsController.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Add,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
        _serilog.Dispose();
    }

    private ClientErrorsController Controller(bool enabled = true)
    {
        var factory = new SerilogLoggerFactory(_serilog);
        return new ClientErrorsController(Options.Create(new ClientErrorsOptions { Enabled = enabled }), factory.CreateLogger<ClientErrorsController>());
    }

    private static ClientErrorDto ScriptError(string? traceparent = Traceparent) => new()
    {
        Kind = "error",
        Message = "TypeError: x is undefined",
        Stack = "at f (main.js:1:2)",
        Page = "/season/12",
        Traceparent = traceparent,
    };

    [Test]
    public void Report_Disabled_IsNotFoundAndLogsNothing()
    {
        var result = Controller(enabled: false).Report(ScriptError());

        result.AssertProblem(StatusCodes.Status404NotFound, "Browser error reports are off here");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sink.Events, Is.Empty);
            Assert.That(_activities, Is.Empty);
        }
    }

    [Test]
    public void Report_ScriptError_LogsAWarningInTheTraceItNames()
    {
        var result = Controller().Report(ScriptError());

        var logged = _sink.Events.Single();
        var span = _activities.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(logged.Level, Is.EqualTo(LogEventLevel.Warning));
            Assert.That(logged.RenderMessage(), Is.EqualTo("Browser error on \"/season/12\": \"TypeError: x is undefined\""));
            Assert.That(logged.Properties["Stack"].ToString(), Is.EqualTo("\"at f (main.js:1:2)\""));
            Assert.That(logged.TraceId?.ToHexString(), Is.EqualTo(TraceId));
            Assert.That(span.TraceId.ToHexString(), Is.EqualTo(TraceId));
            Assert.That(span.ParentSpanId.ToHexString(), Is.EqualTo("b7ad6b7169203331"));
            Assert.That(span.DisplayName, Is.EqualTo("browser error"));
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(span.GetTagItem("url.path"), Is.EqualTo("/season/12"));
        }
    }

    [Test]
    public void Report_FailedCall_LogsTheCallAndItsStatus()
    {
        Controller().Report(new ClientErrorDto
        {
            Kind = "http",
            Message = "Http failure response: 502 Bad Gateway",
            Page = "/teams",
            Traceparent = Traceparent,
            Method = "GET",
            Url = "/api/Teams/GetTeams",
            Status = 502,
        });

        var logged = _sink.Events.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logged.Level, Is.EqualTo(LogEventLevel.Warning));
            Assert.That(logged.RenderMessage(), Is.EqualTo("Browser call \"GET\" \"/api/Teams/GetTeams\" failed (502) on \"/teams\": \"Http failure response: 502 Bad Gateway\""));
            Assert.That(_activities.Single().DisplayName, Is.EqualTo("browser http"));
        }
    }

    [TestCase(null)]
    [TestCase("not-a-traceparent")]
    public void Report_WithoutAUsableTraceparent_StillLogsWithoutASpan(string? traceparent)
    {
        var result = Controller().Report(ScriptError(traceparent));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(_sink.Events, Has.Count.EqualTo(1));
            Assert.That(_activities, Is.Empty);
        }
    }

    [Test]
    public void PartitionByClient_SignedOut_LimitsEachAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var partition = ClientErrorsExtensions.PartitionByClient(context, permitsPerMinute: 2);
        using var limiter = partition.Factory(partition.PartitionKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partition.PartitionKey, Is.EqualTo("203.0.113.7"));
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.True);
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.True);
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.False);
        }
    }

    [Test]
    public void PartitionByClient_SignedIn_HasNoLimit()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Identity.Application")),
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var partition = ClientErrorsExtensions.PartitionByClient(context, permitsPerMinute: 1);
        using var limiter = partition.Factory(partition.PartitionKey);

        for (var i = 0; i < 50; i++)
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.True);
    }

    [Test]
    public void PartitionByClient_NoIdentity_IsLimited()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal() };

        var partition = ClientErrorsExtensions.PartitionByClient(context, permitsPerMinute: 1);
        using var limiter = partition.Factory(partition.PartitionKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partition.PartitionKey, Is.Empty);
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.True);
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.False);
        }
    }

    [Test]
    public async Task UseRateLimiter_RefusesASignedOutClientOverTheLimit_AndNeverASignedInUser()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ClientErrors:PermitsPerMinute"] = "2" })
            .Build();
        await using var provider = new ServiceCollection().AddLogging().AddClientErrors(configuration).BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseRateLimiter();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var endpoint = new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute(ClientErrorsController.RateLimitPolicy)), "ReportClientError");

        async Task<int> Send(ClaimsPrincipal? user)
        {
            var context = new DefaultHttpContext { RequestServices = provider };
            if (user is not null)
                context.User = user;
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            context.SetEndpoint(endpoint);
            await pipeline(context);
            return context.Response.StatusCode;
        }

        var signedIn = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Identity.Application"));
        var signedOut = new[] { await Send(null), await Send(null), await Send(null) };
        var signedInAfter = await Send(signedIn);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signedOut, Is.EqualTo(TwoThenRefused));
            Assert.That(signedInAfter, Is.EqualTo(204));
        }
    }

    [Test]
    public void AddClientErrors_BindsTheSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ClientErrors:Enabled"] = "true", ["ClientErrors:PermitsPerMinute"] = "5" })
            .Build();
        using var provider = new ServiceCollection().AddClientErrors(configuration).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ClientErrorsOptions>>().Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Enabled, Is.True);
            Assert.That(options.PermitsPerMinute, Is.EqualTo(5));
        }
    }

    [Test]
    public void AddClientErrors_OffByDefault()
    {
        using var provider = new ServiceCollection().AddClientErrors(new ConfigurationBuilder().Build()).BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IOptions<ClientErrorsOptions>>().Value.Enabled, Is.False);
    }
}
