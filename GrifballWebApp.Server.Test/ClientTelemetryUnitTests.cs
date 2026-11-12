using GrifballWebApp.Server.Telemetry;
using GrifballWebApp.Server.Telemetry.Browser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrifballWebApp.Server.Test;

/// <summary>The browser's telemetry: its batches on to the collector as OTLP/JSON, its Web Vitals as metrics, off unless enabled.</summary>
[TestFixture]
[NonParallelizable] // The MeterListener sees every grif.browser.* measurement in the process.
public class ClientTelemetryUnitTests
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private static readonly int[] TwoThenRefused = [204, 204, 429];
    private static readonly string[] TracesThenLogs = ["http://collector:4318/v1/traces", "http://collector:4318/v1/logs"];
    private static readonly (string, double)[] LcpThenCls = [("grif.browser.lcp", 1.25), ("grif.browser.cls", 0.08)];

    private sealed class Collector(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(Uri Url, JsonNode Body)> Posted { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Posted.Add((request.RequestUri!, JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!));
            return new HttpResponseMessage(status);
        }
    }

    /// <summary>A collector that answers only once released.</summary>
    private sealed class Slow : HttpMessageHandler
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class Warnings : Microsoft.Extensions.Logging.ILogger<ClientTelemetryForwarder>
    {
        public int Count { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
                Count++;
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no route to host");
    }

    private static readonly TelemetryOptions Telemetry = new()
    {
        ServiceName = "grif-test",
        ServiceInstanceId = "grif-backend-1",
        Environment = "test",
        ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "grif-test",
            ["service.instance.id"] = "grif-backend-1",
            ["service.version"] = "abc123",
            ["deployment.environment.name"] = "test",
        },
    };

    private static IOptions<ClientTelemetryOptions> Options(bool enabled = true, string? endpoint = "http://collector:4318/") =>
        Microsoft.Extensions.Options.Options.Create(new ClientTelemetryOptions { Enabled = enabled, OtlpEndpoint = endpoint });

    private static ClientTelemetryForwarder Forwarder(HttpMessageHandler handler, IOptions<ClientTelemetryOptions>? options = null) =>
        new(new HttpClient(handler), new ClientTelemetryResource(Telemetry), options ?? Options(), NullLogger<ClientTelemetryForwarder>.Instance);

    private static ClientTelemetryController Controller(HttpMessageHandler handler, bool enabled = true, BrowserMetrics? metrics = null)
    {
        var options = Options(enabled);
        return new ClientTelemetryController(options, Forwarder(handler, options), metrics ?? new BrowserMetrics())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static Dictionary<string, JsonElement> Attributes(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static ClientTelemetryBatchDto Batch() => new()
    {
        Spans =
        [
            new ClientSpanDto
            {
                TraceId = TraceId, SpanId = "b7ad6b7169203331", ParentSpanId = "a7ad6b7169203331", Name = "GET", Kind = "client",
                Start = 1_793_684_837_000.5, End = 1_793_684_837_042.25, Status = "error",
                Attributes = Attributes("""{"http.request.method":"GET","url.path":"/api/Identity/Reset?token=s3cret&page=2","http.response.status_code":502,"grif.cancelled":false,"grif.aborted":true,"grif.ratio":0.25}"""),
            },
        ],
        Logs =
        [
            new ClientLogDto
            {
                Time = 1_793_684_837_100, Level = "error", Message = "TypeError: x is undefined", TraceId = TraceId, SpanId = "b7ad6b7169203331",
                Attributes = Attributes("""{"page":"/season/12","exception.stacktrace":"at f (main.js:1:2)"}"""),
            },
        ],
        Vitals = [new ClientVitalDto { Name = "LCP", Value = 1250 }, new ClientVitalDto { Name = "CLS", Value = 0.08 }],
    };

    private static JsonNode Attribute(JsonNode? attributes, string key) =>
        attributes!.AsArray().Single(a => a!["key"]!.GetValue<string>() == key)!["value"]!;

    [Test]
    public async Task Report_Disabled_IsNotFoundAndSendsNothing()
    {
        var collector = new Collector();

        var result = await Controller(collector, enabled: false).Report(Batch(), "Mozilla/5.0", default);

        result.AssertProblem(StatusCodes.Status404NotFound, "Browser telemetry is off here");
        Assert.That(collector.Posted, Is.Empty);
    }

    [Test]
    public async Task Report_SendsTheSpansAndLogsToTheCollector_AsTheBrowsersOfThisService()
    {
        var collector = new Collector();

        var result = await Controller(collector).Report(Batch(), "Mozilla/5.0", default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(collector.Posted.Select(p => p.Url.AbsoluteUri), Is.EqualTo(TracesThenLogs));
        }

        var traces = collector.Posted[0].Body["resourceSpans"]![0]!;
        var resource = traces["resource"]!["attributes"];
        var span = traces["scopeSpans"]![0]!["spans"]![0]!;
        var log = collector.Posted[1].Body["resourceLogs"]![0]!["scopeLogs"]![0]!["logRecords"]![0]!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Attribute(resource, "service.name")["stringValue"]!.GetValue<string>(), Is.EqualTo("grif-test"));
            Assert.That(Attribute(resource, "service.component")["stringValue"]!.GetValue<string>(), Is.EqualTo("browser"));
            Assert.That(Attribute(resource, "deployment.environment.name")["stringValue"]!.GetValue<string>(), Is.EqualTo("test"));
            Assert.That(resource!.AsArray().Select(a => a!["key"]!.GetValue<string>()), Does.Not.Contain("service.instance.id").And.Not.Contain("service.version"));

            Assert.That(span["traceId"]!.GetValue<string>(), Is.EqualTo(TraceId));
            Assert.That(span["parentSpanId"]!.GetValue<string>(), Is.EqualTo("a7ad6b7169203331"));
            Assert.That(span["kind"]!.GetValue<int>(), Is.EqualTo(3));
            Assert.That(span["startTimeUnixNano"]!.GetValue<string>(), Is.EqualTo("1793684837000500000"));
            Assert.That(span["endTimeUnixNano"]!.GetValue<string>(), Is.EqualTo("1793684837042250000"));
            Assert.That(span["status"]!["code"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(Attribute(span["attributes"], "url.path")["stringValue"]!.GetValue<string>(), Is.EqualTo("/api/Identity/Reset?token=REDACTED&page=2"));
            Assert.That(Attribute(span["attributes"], "http.response.status_code")["intValue"]!.GetValue<string>(), Is.EqualTo("502"));
            Assert.That(Attribute(span["attributes"], "grif.cancelled")["boolValue"]!.GetValue<bool>(), Is.False);
            Assert.That(Attribute(span["attributes"], "grif.aborted")["boolValue"]!.GetValue<bool>(), Is.True);
            Assert.That(Attribute(span["attributes"], "grif.ratio")["doubleValue"]!.GetValue<double>(), Is.EqualTo(0.25));

            Assert.That(log["severityNumber"]!.GetValue<int>(), Is.EqualTo(17));
            Assert.That(log["severityText"]!.GetValue<string>(), Is.EqualTo("ERROR"));
            Assert.That(log["body"]!["stringValue"]!.GetValue<string>(), Is.EqualTo("TypeError: x is undefined"));
            Assert.That(log["traceId"]!.GetValue<string>(), Is.EqualTo(TraceId));
            Assert.That(Attribute(log["attributes"], "page")["stringValue"]!.GetValue<string>(), Is.EqualTo("/season/12"));
        }
    }

    [Test]
    public async Task Report_RecordsTheWebVitals_InSecondsAndCLSAsIs()
    {
        var measured = new List<(string Name, double Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == BrowserMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => measured.Add((instrument.Name, value)));
        listener.Start();
        using var metrics = new BrowserMetrics();

        await Controller(new Collector(), metrics: metrics).Report(Batch(), "Mozilla/5.0", default);

        Assert.That(measured, Is.EqualTo(LcpThenCls));
    }

    [Test]
    public async Task Report_AnswersBeforeTheCollectorDoes()
    {
        var collector = new Slow();
        try
        {
            var report = Controller(collector).Report(Batch(), "Mozilla/5.0", default);

            var finished = await Task.WhenAny(report, Task.Delay(TimeSpan.FromSeconds(2))) == report;

            Assert.That(finished, Is.True, "the browser waited on the collector");
        }
        finally
        {
            collector.Release.TrySetResult();
        }
    }

    [Test]
    public async Task Forward_ACollectorThatIsAway_IsLoggedOnceAMinute_NotEachBatch()
    {
        var warnings = new Warnings();
        var forwarder = new ClientTelemetryForwarder(new HttpClient(new Unreachable()), new ClientTelemetryResource(Telemetry), Options(), warnings);

        for (var i = 0; i < 10; i++)
            await forwarder.Forward(Batch(), "", default);

        Assert.That(warnings.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Report_RedactsSensitiveQueryValuesInALogsMessageAndAttributes()
    {
        var collector = new Collector();
        var batch = new ClientTelemetryBatchDto
        {
            Logs =
            [
                new ClientLogDto
                {
                    Time = 1_793_684_837_100, Level = "error", Message = "failed /reset-password?token=abc123&page=2",
                    Attributes = Attributes("""{"exception.stacktrace":"at https://grifball.xyz/main.js?sig=s3cr3t:1:2"}"""),
                },
            ],
        };

        await Controller(collector).Report(batch, null, default);

        var sent = collector.Posted.Single().Body.ToJsonString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent, Does.Not.Contain("abc123").And.Not.Contain("s3cr3t"));
            Assert.That(sent, Does.Contain("page=2"));
        }
    }

    [Test]
    public async Task Forward_AnEmptyBatchOrNoEndpoint_SendsNothing()
    {
        var collector = new Collector();

        await Forwarder(collector).Forward(new ClientTelemetryBatchDto { Vitals = [new ClientVitalDto { Name = "LCP", Value = 1 }] }, "", default);
        await Forwarder(collector, Options(endpoint: null)).Forward(Batch(), "", default);

        Assert.That(collector.Posted, Is.Empty);
    }

    [Test]
    public async Task Report_WithoutAUserAgent_SendsNone()
    {
        var collector = new Collector();

        await Controller(collector).Report(new ClientTelemetryBatchDto { Logs = Batch().Logs }, null, default);

        var resource = collector.Posted.Single().Body["resourceLogs"]![0]!["resource"]!["attributes"];
        Assert.That(resource!.AsArray().Select(a => a!["key"]!.GetValue<string>()), Does.Not.Contain("user_agent.original"));
    }

    [Test]
    public async Task Forward_PutsTheBrowsersUserAgentOnTheResource()
    {
        var collector = new Collector();

        await Forwarder(collector).Forward(new ClientTelemetryBatchDto { Logs = Batch().Logs }, "Mozilla/5.0", default);

        var resource = collector.Posted.Single().Body["resourceLogs"]![0]!["resource"]!["attributes"];
        Assert.That(Attribute(resource, "user_agent.original")["stringValue"]!.GetValue<string>(), Is.EqualTo("Mozilla/5.0"));
    }

    [Test]
    public async Task Forward_ACollectorThatIsAwayOrRefuses_FailsNothing()
    {
        // Each would throw, failing the test, if a failure got out
        await Forwarder(new Unreachable()).Forward(Batch(), "", default);
        var refusing = new Collector(HttpStatusCode.ServiceUnavailable);
        await Forwarder(refusing).Forward(Batch(), "", default);

        Assert.That(refusing.Posted, Has.Count.EqualTo(2));
    }

    [Test]
    public void Attributes_KeepOnlyNamedLikeOpenTelemetry_BoundedAndNoService()
    {
        var many = string.Join(",", Enumerable.Range(0, 40).Select(i => $"\"a.n{i}\":{i}"));
        var clean = ClientAttributes.Clean(Attributes($$"""
            {"service.name":"spoof","Bad Key":"x","nested":{"a":1},"list":[1],"none":null,"long.text":"{{new string('x', 1500)}}","ratio":0.5,{{many}}}
            """));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clean, Has.Count.EqualTo(ClientAttributes.MaxAttributes));
            Assert.That(clean.Select(a => a.Key), Does.Not.Contain("service.name").And.Not.Contain("Bad Key").And.Not.Contain("nested").And.Not.Contain("list").And.Not.Contain("none"));
            Assert.That((string)clean.Single(a => a.Key == "long.text").Value, Has.Length.EqualTo(ClientAttributes.MaxValueLength));
            Assert.That(clean.Single(a => a.Key == "ratio").Value, Is.EqualTo(0.5));
            Assert.That(clean.Single(a => a.Key == "a.n3").Value, Is.EqualTo(3L));
        }
        Assert.That(ClientAttributes.Clean(null), Is.Empty);
    }

    [TestCase("/api/x?userId=1&code=abc&page=2", "/api/x?userId=REDACTED&code=REDACTED&page=2")]
    [TestCase("/reset?Reset%5FToken=t#top", "/reset?Reset%5FToken=REDACTED#top")]
    [TestCase("/season/12", "/season/12")]
    [TestCase("/x?flag", "/x?flag")]
    public void QueryRedaction_RedactsSensitiveValues(string url, string expected)
    {
        Assert.That(QueryRedaction.Redact(url), Is.EqualTo(expected));
    }

    [Test]
    public void OtlpJson_SpanKindsStatusesAndLevels()
    {
        var resource = new Dictionary<string, string>();
        var span = OtlpJson.Traces(resource, [new ClientSpanDto { TraceId = TraceId, SpanId = "b7ad6b7169203331", Name = "navigation", Start = 2e12, End = 1e12 }])
            ["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]![0]!;
        var log = OtlpJson.Logs(resource, [new ClientLogDto { Time = 2e12, Level = "warn", Message = "m", TraceId = TraceId }])
            ["resourceLogs"]![0]!["scopeLogs"]![0]!["logRecords"]![0]!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(span["kind"]!.GetValue<int>(), Is.EqualTo(1));
            Assert.That(span["status"]!["code"]!.GetValue<int>(), Is.Zero);
            Assert.That(span["parentSpanId"], Is.Null);
            Assert.That(span["endTimeUnixNano"]!.GetValue<string>(), Is.EqualTo(span["startTimeUnixNano"]!.GetValue<string>()), "an end before the start is the start");
            Assert.That(log["severityNumber"]!.GetValue<int>(), Is.EqualTo(13));
            Assert.That(log["traceId"], Is.Null, "a trace id without a span id is no place in a trace");
        }
    }

    [Test]
    public void IsClientTelemetryPath_TheBatchesOnly()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ClientTelemetryExtensions.IsClientTelemetryPath("/client-telemetry"), Is.True);
            Assert.That(ClientTelemetryExtensions.IsClientTelemetryPath("/Client-Telemetry"), Is.True);
            Assert.That(ClientTelemetryExtensions.IsClientTelemetryPath("/telemetry/status"), Is.False);
        }
    }

    [Test]
    public void PartitionByClient_SignedOut_LimitsEachAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var partition = ClientTelemetryExtensions.PartitionByClient(context, permitsPerMinute: 2);
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
    public void PartitionByClient_NoIdentity_IsLimited()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal() };

        var partition = ClientTelemetryExtensions.PartitionByClient(context, permitsPerMinute: 1);
        using var limiter = partition.Factory(partition.PartitionKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partition.PartitionKey, Is.Empty);
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

        var partition = ClientTelemetryExtensions.PartitionByClient(context, permitsPerMinute: 1);
        using var limiter = partition.Factory(partition.PartitionKey);

        for (var i = 0; i < 50; i++)
            Assert.That(limiter.AttemptAcquire().IsAcquired, Is.True);
    }

    [Test]
    public async Task UseRateLimiter_RefusesASignedOutClientOverTheLimit_AndNeverASignedInUser()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ClientTelemetry:PermitsPerMinute"] = "2" })
            .Build();
        await using var provider = new ServiceCollection().AddLogging().AddClientTelemetry(configuration, Telemetry).BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseRateLimiter();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var endpoint = new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute(ClientTelemetryController.RateLimitPolicy)), "ReportClientTelemetry");

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

    private static ServiceProvider Provider(params (string Key, string Value)[] settings) =>
        new ServiceCollection().AddClientTelemetry(
            new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)).Build(), Telemetry)
            .BuildServiceProvider();

    [Test]
    public void AddClientTelemetry_BindsTheSection_AndIsOffByDefault()
    {
        using var configured = Provider(("ClientTelemetry:Enabled", "true"), ("ClientTelemetry:OtlpEndpoint", "http://collector:4318"), ("ClientTelemetry:PermitsPerMinute", "5"));
        using var unset = Provider();

        var options = configured.GetRequiredService<IOptions<ClientTelemetryOptions>>().Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Enabled, Is.True);
            Assert.That(options.OtlpUri, Is.EqualTo(new Uri("http://collector:4318")));
            Assert.That(options.PermitsPerMinute, Is.EqualTo(5));
            Assert.That(unset.GetRequiredService<IOptions<ClientTelemetryOptions>>().Value.Enabled, Is.False);
        }
    }

    [Test]
    public void AddClientTelemetry_RefusesAnInvalidLimit_OrEnabledWithoutAnEndpoint()
    {
        using var badLimit = Provider(("ClientTelemetry:PermitsPerMinute", "0"));
        using var noEndpoint = Provider(("ClientTelemetry:Enabled", "true"));
        using var notHttp = Provider(("ClientTelemetry:Enabled", "true"), ("ClientTelemetry:OtlpEndpoint", "collector:4318"));

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<OptionsValidationException>(() => _ = badLimit.GetRequiredService<IOptions<ClientTelemetryOptions>>().Value);
            Assert.Throws<OptionsValidationException>(() => _ = noEndpoint.GetRequiredService<IOptions<ClientTelemetryOptions>>().Value);
            Assert.Throws<OptionsValidationException>(() => _ = notHttp.GetRequiredService<IOptions<ClientTelemetryOptions>>().Value);
        }
    }
}
