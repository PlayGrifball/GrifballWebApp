using Microsoft.AspNetCore.Http;
using GrifballWebApp.Server.Telemetry.Query;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;
using System.Web;

namespace GrifballWebApp.Server.Test;

[TestFixture]
public class TelemetryQueriesTests
{
    [Test]
    public void Quote_escapes_what_ends_or_breaks_a_string()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TelemetryQueries.Quote("a\"b\\c\nd"), Is.EqualTo("\"a\\\"b\\\\c\\nd\""));
            Assert.That(TelemetryQueries.Quote("\r\t"), Is.EqualTo("\"\\r\\t\""));
        }
    }

    [Test]
    public void Re2Escape_escapes_metacharacters_but_not_spaces()
    {
        Assert.That(TelemetryQueries.Re2Escape("GET /api/x?y=(1) #2"), Is.EqualTo(@"GET /api/x\?y=\(1\) #2"));
    }

    [TestCase("D8885AD9A3A1121898CDAAAAF596F993", "d8885ad9a3a1121898cdaaaaf596f993")]
    [TestCase("556214b423d5a640fb17a50481aa1fd", "0556214b423d5a640fb17a50481aa1fd")]
    [TestCase(" abc ", "00000000000000000000000000000abc")]
    [TestCase("not-hex", null)]
    [TestCase("", null)]
    [TestCase("123456789012345678901234567890123", null)]
    public void NormalizeTraceId_pads_and_rejects(string id, string? expected)
    {
        Assert.That(TelemetryQueries.NormalizeTraceId(id), Is.EqualTo(expected));
    }

    [Test]
    public void Logs_with_nothing_picked_is_the_service_only()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TelemetryQueries.Logs("grif-prod", null, " ", null), Is.EqualTo("{service_name=\"grif-prod\"}"));
            Assert.That(TelemetryQueries.Logs("grif-prod", TelemetryQueries.Levels, null, null), Is.EqualTo("{service_name=\"grif-prod\"}"));
        }
    }

    [Test]
    public void Logs_filters_levels_search_and_trace()
    {
        var query = TelemetryQueries.Logs("grif-prod", ["warn", "error", "error"], "a \"quoted\" (x)", "d8885ad9a3a1121898cdaaaaf596f993");

        Assert.That(query, Is.EqualTo(
            "{service_name=\"grif-prod\"} | detected_level=~\"warn|error\" |~ \"(?i)a \\\"quoted\\\" \\\\(x\\\\)\" | trace_id=\"d8885ad9a3a1121898cdaaaaf596f993\""));
    }

    [Test]
    public void Search_cannot_leave_the_service()
    {
        var query = TelemetryQueries.Logs("grif-prod", null, "\"} or {service_name=~\".+", null);

        Assert.That(query, Does.StartWith("{service_name=\"grif-prod\"} |~ \"(?i)\\\"\\\\}"));
        Assert.That(query, Does.EndWith("\\\\.\\\\+\""), "the whole search stays in one string");
    }

    [Test]
    public void Every_metric_query_is_scoped_to_the_service()
    {
        var queries = TelemetryQueries.Panels("grif-prod", TimeSpan.FromMinutes(2)).Select(p => p.Query).ToList();
        var (requests, errors, p95) = TelemetryQueries.Routes("grif-prod", TimeSpan.FromHours(1));
        queries.AddRange([requests, errors, p95]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queries, Has.All.Contains("service_name=\"grif-prod\""));
            Assert.That(requests, Does.Contain("[3600s]"));
        }
        Assert.That(queries[0], Does.Contain("[120s]").And.Contain("http_route!~\"/health/.*\""));
    }

    [Test]
    public void Frontend_panels_read_the_app_servers_metrics()
    {
        var panels = TelemetryQueries.Panels("grif-prod", TimeSpan.FromMinutes(2)).ToDictionary(p => p.Key);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(panels["render"].Query, Does.Contain("grif_frontend_render_duration_seconds_bucket{service_name=\"grif-prod\",outcome=\"server\"}[120s]"));
            Assert.That(panels["renderFailures"].Query, Does.Contain("grif_frontend_render_duration_seconds_count{service_name=\"grif-prod\",outcome=\"failed\"}[120s]"));
            Assert.That(panels["frontendMemory"].Query, Does.Contain("grif_frontend_memory_rss_bytes{service_name=\"grif-prod\"}"));
            Assert.That(panels["frontendMemory"].SeriesLabel, Is.EqualTo("k8s_pod_name"));
        }
    }

    private static Dictionary<string, string> Span(params (string Key, string Value)[] attributes) => attributes.ToDictionary(a => a.Key, a => a.Value);

    [Test]
    public void A_page_row_is_the_pages_own_whatever_its_render_called()
    {
        var row = TelemetryQueries.TraceRow("GET /*", [
            Span(("http.route", "Season/Get"), ("http.response.status_code", "200")),
            Span(("grif.request.kind", "page"), ("http.route", "/*"), ("http.response.status_code", "500")),
        ]);

        Assert.That(row, Is.EqualTo(("GET /*", (string?)"/*", (int?)500)));
    }

    [Test]
    public void A_browser_navigation_row_is_the_navigations_own()
    {
        var row = TelemetryQueries.TraceRow("navigation season/:seasonID", [
            Span(("grif.request.kind", "api"), ("http.route", "/api/*")),
            Span(("http.route", "Season/Get"), ("http.response.status_code", "200")),
            Span(("grif.request.kind", "navigation"), ("http.route", "season/:seasonID")),
        ]);

        Assert.That(row, Is.EqualTo(("navigation season/:seasonID", (string?)"season/:seasonID", (int?)null)));
    }

    [Test]
    public void An_api_calls_row_takes_the_backends_method_route_and_status()
    {
        var row = TelemetryQueries.TraceRow("GET /api/*", [
            Span(("grif.request.kind", "api"), ("http.route", "/api/*"), ("http.request.method", "GET"), ("http.response.status_code", "502")),
            Span(("http.route", "Teams/GetTeams"), ("http.request.method", "GET"), ("http.response.status_code", "500")),
        ]);

        Assert.That(row, Is.EqualTo(("GET Teams/GetTeams", (string?)"Teams/GetTeams", (int?)500)));
    }

    [Test]
    public void An_api_calls_row_without_the_backends_span_keeps_the_root()
    {
        var row = TelemetryQueries.TraceRow("GET /api/*", [
            Span(("grif.request.kind", "api"), ("http.route", "/api/*"), ("http.response.status_code", "504")),
        ]);

        Assert.That(row, Is.EqualTo(("GET /api/*", (string?)"/api/*", (int?)504)));
    }

    [Test]
    public void Any_other_row_takes_the_first_route_and_status()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TelemetryQueries.TraceRow("GET Home/Events", [Span(("http.route", "Home/Events"), ("http.response.status_code", "200"))]),
                Is.EqualTo(("GET Home/Events", (string?)"Home/Events", (int?)200)));
            Assert.That(TelemetryQueries.TraceRow("job", [Span(("http.response.status_code", "503"))]),
                Is.EqualTo(("job", (string?)null, (int?)503)));
            Assert.That(TelemetryQueries.TraceRow("PATCH", []), Is.EqualTo(("PATCH", (string?)null, (int?)null)));
        }
    }

    [Test]
    public void Traces_adds_errors_and_duration()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TelemetryQueries.Traces("grif-prod", false, null),
                    Is.EqualTo("{resource.service.name=\"grif-prod\"} | select(span.http.route, span.http.response.status_code, span.http.request.method, span.grif.request.kind)"));
            Assert.That(TelemetryQueries.Traces("grif-prod", false, 0), Does.Not.Contain("duration"));
            Assert.That(TelemetryQueries.Traces("grif-prod", true, 250),
                Is.EqualTo("{resource.service.name=\"grif-prod\" && status=error && duration>=250ms} | select(span.http.route, span.http.response.status_code, span.http.request.method, span.grif.request.kind)"));
        }
    }
}

[TestFixture]
public class TelemetryQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Loki, Prometheus and Tempo as the tests serve them: answers by path, and every request remembered.</summary>
    private sealed class FakeBackends(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        private readonly List<Uri> _asked = [];

        public List<Uri> Asked { get { lock (_asked) return [.. _asked]; } }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_asked)
                _asked.Add(request.RequestUri!);
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static TelemetryQueryService Service(FakeBackends backends, bool configured = true) =>
        new(new HttpClient(backends), Options.Create(new TelemetryQueryOptions
        {
            ServiceName = "grif-prod",
            LokiUrl = configured ? "http://loki.test" : null,
            PrometheusUrl = configured ? "http://prometheus.test:9090" : null,
            TempoUrl = configured ? "http://tempo.test:3200" : null,
        }), new FakeTimeProvider(Now));

    private static string Query(Uri uri, string key) => HttpUtility.ParseQueryString(uri.Query)[key]!;

    [Test]
    public void Status_says_what_is_configured()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Service(new FakeBackends(_ => Json("{}")), configured: false).Status(),
                    Is.EqualTo(new TelemetryStatusDto("grif-prod", false, false, false)));
            Assert.That(Service(new FakeBackends(_ => Json("{}"))).Status(),
                Is.EqualTo(new TelemetryStatusDto("grif-prod", true, true, true)));
        }
    }

    [Test]
    public void Range_is_clamped_and_never_ends_in_the_future()
    {
        var service = Service(new FakeBackends(_ => Json("{}")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.Range(1), Is.EqualTo((Now.AddMinutes(-5), Now)));
            Assert.That(service.Range(100_000), Is.EqualTo((Now.AddDays(-7), Now)));
            Assert.That(service.Range(60, Now.AddHours(1)), Is.EqualTo((Now.AddHours(-1), Now)));
            Assert.That(service.Range(60, Now.AddHours(-2)), Is.EqualTo((Now.AddHours(-3), Now.AddHours(-2))));
        }
    }

    [Test]
    public void Unconfigured_backends_throw_404s()
    {
        var service = Service(new FakeBackends(_ => Json("{}")), configured: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => service.Logs(60, null, null, null, null, 10, default), Throws.TypeOf<TelemetryBackendMissingException>().With.Property("Status").EqualTo(404));
            Assert.That(() => service.Metrics(60, default), Throws.TypeOf<TelemetryBackendMissingException>());
            Assert.That(() => service.Traces(60, false, null, 10, default), Throws.TypeOf<TelemetryBackendMissingException>());
            Assert.That(() => service.Trace("abc", default), Throws.TypeOf<TelemetryBackendMissingException>());
        }
    }

    [Test]
    public void A_failing_backend_is_a_502_with_its_answer()
    {
        var service = Service(new FakeBackends(_ => Json("parse error: unexpected", HttpStatusCode.BadRequest)));

        Assert.That(() => service.Logs(60, null, null, null, null, 10, default),
            Throws.TypeOf<TelemetryBackendException>().With.Property("Status").EqualTo(502).And.Message.EqualTo("Loki answered 400: parse error: unexpected"));
    }

    [Test]
    public void An_unreachable_backend_is_a_502()
    {
        var service = Service(new FakeBackends(_ => throw new HttpRequestException("connection refused")));

        Assert.That(() => service.Metrics(60, default),
            Throws.TypeOf<TelemetryBackendException>().With.Message.EqualTo("Prometheus could not be reached: connection refused"));
    }

    private const string LokiAnswer = """
        {"status":"success","data":{"resultType":"streams","result":[
          {"stream":{"service_name":"grif-prod","k8s_pod_name":"grif-backend-1","detected_level":"info","severity_text":"Information",
                     "trace_id":"d8885ad9a3a1121898cdaaaaf596f993","span_id":"8b8578a5df7f95eb","scope_name":"Serilog.AspNetCore.RequestLoggingMiddleware",
                     "RequestPath":"/Home/Events","StatusCode":"200","telemetry_sdk_name":"serilog","cluster":"home"},
           "values":[["1793684838529599600","HTTP GET /Home/Events responded 200 in 2.9872 ms"],["1793684830000000000","older"]]},
          {"stream":{"service_name":"grif-prod","service_instance_id":"grif-worker-1","severity_text":"Error","SourceContext":"GrifballWebApp.Server.Jobs.X",
                     "exception_type":"System.InvalidOperationException"},
           "values":[["1793684835000000000","Job failed"]]}
        ]}}
        """;

    [Test]
    public async Task Logs_are_merged_newest_first_without_resource_noise()
    {
        var backends = new FakeBackends(_ => Json(LokiAnswer));

        var logs = await Service(backends).Logs(60, null, ["info", "error"], "events", null, 200, default);

        var asked = backends.Asked.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(asked.AbsolutePath, Is.EqualTo("/loki/api/v1/query_range"));
            Assert.That(Query(asked, "query"), Is.EqualTo("{service_name=\"grif-prod\"} | detected_level=~\"info|error\" |~ \"(?i)events\""));
            Assert.That(Query(asked, "end"), Is.EqualTo((Now.ToUnixTimeMilliseconds() * 1_000_000).ToString()));
            Assert.That(Query(asked, "direction"), Is.EqualTo("backward"));
            Assert.That(logs.Truncated, Is.False);
            Assert.That(logs.Entries.Select(e => e.Message), Is.EqualTo(expected));
        }

        var first = logs.Entries[0];
        var failed = logs.Entries[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Timestamp, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1793684838529).AddTicks(5996)));
            Assert.That(first.Level, Is.EqualTo("info"));
            Assert.That(first.TraceId, Is.EqualTo("d8885ad9a3a1121898cdaaaaf596f993"));
            Assert.That(first.SpanId, Is.EqualTo("8b8578a5df7f95eb"));
            Assert.That(first.Source, Is.EqualTo("Serilog.AspNetCore.RequestLoggingMiddleware"));
            Assert.That(first.Pod, Is.EqualTo("grif-backend-1"));
            Assert.That(first.Attributes, Is.EqualTo(new Dictionary<string, string> { ["RequestPath"] = "/Home/Events", ["StatusCode"] = "200" }));
            Assert.That(failed.Level, Is.EqualTo("error"), "severity_text when Loki detected no level");
            Assert.That(failed.Source, Is.EqualTo("GrifballWebApp.Server.Jobs.X"));
            Assert.That(failed.Pod, Is.EqualTo("grif-worker-1"));
            Assert.That(failed.Attributes.Keys, Is.EqualTo(expectedArray));
        }
    }

    [Test]
    public async Task Logs_at_the_limit_are_truncated_and_the_limit_is_clamped()
    {
        var backends = new FakeBackends(_ => Json(LokiAnswer));

        var logs = await Service(backends).Logs(60, null, null, null, "D8885AD9A3A1121898CDAAAAF596F993", 2, default);
        await Service(backends).Logs(60, null, null, null, null, 50_000, default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(logs.Truncated, Is.True);
            Assert.That(logs.Entries, Has.Count.EqualTo(2));
            Assert.That(Query(backends.Asked[0], "query"), Does.EndWith("| trace_id=\"d8885ad9a3a1121898cdaaaaf596f993\""));
            Assert.That(Query(backends.Asked[1], "limit"), Is.EqualTo(TelemetryQueryService.MaxLogLines.ToString()));
        }
    }

    [Test]
    public async Task Metrics_chart_each_panel_and_rank_routes()
    {
        var backends = new FakeBackends(request =>
        {
            var query = Query(request.RequestUri!, "query");
            if (request.RequestUri!.AbsolutePath == "/api/v1/query")
            {
                var value = query.StartsWith("histogram_quantile") ? ("12.345", "NaN") : query.Contains("5..") ? ("1", "0") : ("40.4", "3");
                return Json($$$"""
                    {"status":"success","data":{"resultType":"vector","result":[
                    {"metric":{"http_route":"Season/Get"},"value":[1793700000,"{{{value.Item1}}}"]},
                    {"metric":{"http_route":"Home/Events"},"value":[1793700000,"{{{value.Item2}}}"]},
                    {"metric":{},"value":[1793700000,"0"]}]}}
                    """);
            }
            return query.Contains("k8s_pod_name")
                ? Json("""
                    {"status":"success","data":{"resultType":"matrix","result":[
                    {"metric":{"k8s_pod_name":"grif-worker-1"},"values":[[1793700000,"200"]]},
                    {"metric":{"k8s_pod_name":"grif-backend-1"},"values":[[1793700000,"150.5"],[1793700024,"NaN"]]}]}}
                    """)
                : Json("""{"status":"success","data":{"resultType":"matrix","result":[{"metric":{},"values":[[1793700000.5,"0.25"]]}]}}""");
        });

        var metrics = await Service(backends).Metrics(60, default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.Start, Is.EqualTo(Now.AddHours(-1)));
            Assert.That(metrics.StepSeconds, Is.EqualTo(24), "an hour in 150 points");
            Assert.That(metrics.Panels.Select(p => p.Key), Is.EqualTo(expectedArray0));
            Assert.That(metrics.Panels[0].Series.Single().Name, Is.EqualTo("Requests"));
            Assert.That(metrics.Panels[0].Series.Single().Points.Single(), Is.EqualTo(expectedArray1));
            var memory = metrics.Panels.Single(p => p.Key == "memory");
            Assert.That(memory.Series.Select(s => s.Name), Is.EqualTo(expectedArray2));
            Assert.That(memory.Series[0].Points, Has.Count.EqualTo(1), "NaN is no point");
            Assert.That(metrics.Routes, Is.EqualTo(new[]
            {
                new RouteStatsDto("Season/Get", 40, 1, 12.3),
                new RouteStatsDto("Home/Events", 3, 0, null),
            }));
        }

        var ranged = backends.Asked.Where(u => u.AbsolutePath == "/api/v1/query_range").ToList();
        Assert.That(ranged, Has.Count.EqualTo(10));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ranged.Select(u => Query(u, "step")), Has.All.EqualTo("24s"));
            Assert.That(ranged.Select(u => Query(u, "query")).Where(q => q.Contains("rate(")), Has.All.Contains("[120s]"));
        }
    }

    [Test]
    public async Task Traces_search_reads_route_and_status_from_the_selected_attributes()
    {
        var backends = new FakeBackends(_ => Json("""
            {"traces":[
              {"traceID":"556214b423d5a640fb17a50481aa1fd","rootServiceName":"grif-prod","rootTraceName":"PATCH","startTimeUnixNano":"1793684370991514100","durationMs":411},
              {"traceID":"432816159241389fd3a112f2e194cb12","rootServiceName":"grif-prod","rootTraceName":"GET Season/GetCurrentSeasonID","startTimeUnixNano":"1793684837982413700","durationMs":2,
               "spanSet":{"spans":[{"spanID":"aa09a5c5f8d54419","attributes":[
                 {"key":"service.name","value":{"stringValue":"grif-prod"}},
                 {"key":"http.route","value":{"stringValue":"Season/GetCurrentSeasonID"}},
                 {"key":"http.response.status_code","value":{"intValue":"200"}}]}],"matched":1}}
            ]}
            """));

        var traces = await Service(backends).Traces(30, true, 100, 500, default);

        var asked = backends.Asked.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(asked.AbsolutePath, Is.EqualTo("/api/search"));
            Assert.That(Query(asked, "q"), Does.StartWith("{resource.service.name=\"grif-prod\" && status=error && duration>=100ms}"));
            Assert.That(Query(asked, "limit"), Is.EqualTo(TelemetryQueryService.MaxTraces.ToString()));
            Assert.That(Query(asked, "start"), Is.EqualTo(Now.AddMinutes(-30).ToUnixTimeSeconds().ToString()));
            Assert.That(traces.Select(t => t.TraceId), Is.EqualTo(expectedArray3));
            Assert.That(traces[0].Route, Is.EqualTo("Season/GetCurrentSeasonID"));
            Assert.That(traces[0].StatusCode, Is.EqualTo(200));
            Assert.That(traces[1].Route, Is.Null);
            Assert.That(traces[1].DurationMs, Is.EqualTo(411));
        }
    }

    [Test]
    public async Task Traces_search_asks_for_enough_spans_to_find_the_backends_route()
    {
        var backends = new FakeBackends(_ => Json("""{"traces":[]}"""));

        await Service(backends).Traces(30, false, null, 500, default);

        // One span a trace is only the root: the app server's /api/* when a browser's call started it
        Assert.That(int.Parse(Query(backends.Asked.Single(), "spss"), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(3));
    }

    // A page's trace and a browser call's, as the app's server (grif.request.kind) and the backend make them; the
    // backend's spans come first, as Tempo may list them
    private const string FrontendTraces = """
        {"traces":[
          {"traceID":"11111111111111111111111111111111","rootServiceName":"grif-prod","rootTraceName":"GET /*","startTimeUnixNano":"1793684837982413700","durationMs":40,
           "spanSet":{"spans":[
             {"spanID":"b000000000000001","attributes":[
               {"key":"http.route","value":{"stringValue":"Season/Get"}},
               {"key":"http.request.method","value":{"stringValue":"GET"}},
               {"key":"http.response.status_code","value":{"intValue":"200"}}]},
             {"spanID":"f000000000000001","attributes":[
               {"key":"grif.request.kind","value":{"stringValue":"page"}},
               {"key":"http.route","value":{"stringValue":"/*"}},
               {"key":"http.request.method","value":{"stringValue":"GET"}},
               {"key":"http.response.status_code","value":{"intValue":"500"}}]}],"matched":2}},
          {"traceID":"22222222222222222222222222222222","rootServiceName":"grif-prod","rootTraceName":"POST /api/*","startTimeUnixNano":"1793684837000000000","durationMs":12,
           "spanSet":{"spans":[
             {"spanID":"f000000000000002","attributes":[
               {"key":"grif.request.kind","value":{"stringValue":"api"}},
               {"key":"http.route","value":{"stringValue":"/api/*"}},
               {"key":"http.request.method","value":{"stringValue":"POST"}},
               {"key":"http.response.status_code","value":{"intValue":"502"}}]},
             {"spanID":"b000000000000002","attributes":[
               {"key":"http.route","value":{"stringValue":"files/{*path}"}},
               {"key":"http.request.method","value":{"stringValue":"POST"}},
               {"key":"http.response.status_code","value":{"intValue":"500"}}]}],"matched":2}}
        ]}
        """;

    [Test]
    public async Task A_page_trace_shows_the_pages_own_route_and_status_not_a_call_its_render_made()
    {
        var traces = await Service(new FakeBackends(_ => Json(FrontendTraces))).Traces(30, false, null, 500, default);

        var page = traces.Single(t => t.TraceId == "11111111111111111111111111111111");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.RootName, Is.EqualTo("GET /*"));
            Assert.That(page.Route, Is.EqualTo("/*"));
            Assert.That(page.StatusCode, Is.EqualTo(500));
        }
    }

    [Test]
    public async Task A_browser_calls_trace_shows_the_backends_route_and_status_whatever_the_route_holds()
    {
        var traces = await Service(new FakeBackends(_ => Json(FrontendTraces))).Traces(30, false, null, 500, default);

        var call = traces.Single(t => t.TraceId == "22222222222222222222222222222222");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(call.RootName, Is.EqualTo("POST files/{*path}"));
            Assert.That(call.Route, Is.EqualTo("files/{*path}"));
            Assert.That(call.StatusCode, Is.EqualTo(500));
        }
    }

    [Test]
    public async Task Traces_search_selects_which_part_of_the_app_each_span_is_from()
    {
        var backends = new FakeBackends(_ => Json("""{"traces":[]}"""));

        await Service(backends).Traces(30, false, null, 500, default);

        Assert.That(Query(backends.Asked.Single(), "q"), Does.Contain("span.grif.request.kind").And.Contain("span.http.request.method"));
    }

    [Test]
    public async Task Traces_search_with_no_traces_is_empty()
    {
        Assert.That(await Service(new FakeBackends(_ => Json("{}"))).Traces(60, false, null, 10, default), Is.Empty);
    }

    // Tempo's v2 JSON: base64 ids, nanosecond strings, another service's spans alongside.
    private const string TempoTrace = """
        {"trace":{"resourceSpans":[
          {"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"grif-prod"}},{"key":"k8s.pod.name","value":{"stringValue":"grif-backend-1"}}]},
           "scopeSpans":[
             {"scope":{"name":"Microsoft.AspNetCore"},"spans":[
               {"traceId":"BVYhS0I9WmQPsXpQSBqh/Q==","spanId":"X1TOba0p9Co=","name":"GET Season/Get","kind":"SPAN_KIND_SERVER",
                "startTimeUnixNano":"1000000000","endTimeUnixNano":"1050000000",
                "attributes":[{"key":"http.route","value":{"stringValue":"Season/Get"}},{"key":"http.response.status_code","value":{"intValue":"500"}},{"key":"retry","value":{"boolValue":true}},
                              {"key":"tags","value":{"arrayValue":{"values":[{"stringValue":"a"},{"intValue":"2"}]}}}],
                "status":{"code":"STATUS_CODE_ERROR","message":"boom"},
                "events":[{"timeUnixNano":"1040000000","name":"exception","attributes":[{"key":"exception.type","value":{"stringValue":"System.Exception"}}]}]}]},
             {"scope":{"name":"Microsoft.EntityFrameworkCore"},"spans":[
               {"traceId":"BVYhS0I9WmQPsXpQSBqh/Q==","spanId":"AAAAAAAAAAE=","parentSpanId":"X1TOba0p9Co=","name":"SELECT","kind":"SPAN_KIND_CLIENT",
                "startTimeUnixNano":"1010000000","endTimeUnixNano":"1012500000","status":{}}]}]},
          {"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"traefik"}}]},
           "scopeSpans":[{"scope":{"name":"traefik"},"spans":[
             {"traceId":"BVYhS0I9WmQPsXpQSBqh/Q==","spanId":"AAAAAAAAAAI=","name":"router","kind":"SPAN_KIND_SERVER",
              "startTimeUnixNano":"900000000","endTimeUnixNano":"1100000000","attributes":[{"key":"secret","value":{"stringValue":"not grif's"}}]}]}]}
        ]}}
        """;
    private static readonly string[] expected = new[] { "HTTP GET /Home/Events responded 200 in 2.9872 ms", "Job failed", "older" };
    private static readonly string[] expectedArray = new[] { "exception_type" };
    private static readonly string[] expectedArray0 = new[] { "requests", "errors", "latency", "database", "exceptions", "memory", "cpu", "render", "renderFailures", "frontendMemory" };
    private static readonly double[] expectedArray1 = new[] { 1793700000500d, 0.25 };
    private static readonly string[] expectedArray2 = new[] { "grif-backend-1", "grif-worker-1" };
    private static readonly string[] expectedArray3 = new[] { "432816159241389fd3a112f2e194cb12", "0556214b423d5a640fb17a50481aa1fd" };
    private static readonly string[] expectedArray4 = new[] { "GET Season/Get", "SELECT" };

    [Test]
    public async Task Trace_keeps_this_services_spans_only_with_hex_ids()
    {
        var backends = new FakeBackends(_ => Json(TempoTrace));

        var trace = await Service(backends).Trace("556214B423D5A640FB17A50481AA1FD", default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(backends.Asked.Single().AbsolutePath, Is.EqualTo("/api/v2/traces/0556214b423d5a640fb17a50481aa1fd"));
            Assert.That(trace, Is.Not.Null);
        }
        using (Assert.EnterMultipleScope())
        {
            Assert.That(trace!.TraceId, Is.EqualTo("0556214b423d5a640fb17a50481aa1fd"));
            Assert.That(trace.Start, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1)));
            Assert.That(trace.DurationMs, Is.EqualTo(50), "the other service's wider span is not counted");
            Assert.That(trace.Spans.Select(s => s.Name), Is.EqualTo(expectedArray4));
        }

        var (server, query) = (trace!.Spans[0], trace.Spans[1]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(server.SpanId, Is.EqualTo("5f54ce6dad29f42a"));
            Assert.That(server.ParentSpanId, Is.Null);
            Assert.That(server.Kind, Is.EqualTo("Server"));
            Assert.That(server.Scope, Is.EqualTo("Microsoft.AspNetCore"));
            Assert.That(server.Pod, Is.EqualTo("grif-backend-1"));
            Assert.That(server.Status, Is.EqualTo("Error"));
            Assert.That(server.StatusMessage, Is.EqualTo("boom"));
            Assert.That(server.Attributes, Is.EqualTo(new Dictionary<string, string>
            {
                ["http.route"] = "Season/Get",
                ["http.response.status_code"] = "500",
                ["retry"] = "true",
                ["tags"] = "[a, 2]",
            }));
            Assert.That(server.Events.Single(), Has.Property("Name").EqualTo("exception").And.Property("OffsetMs").EqualTo(40));
            Assert.That(server.Events[0].Attributes["exception.type"], Is.EqualTo("System.Exception"));
            Assert.That(query.ParentSpanId, Is.EqualTo("5f54ce6dad29f42a"));
            Assert.That(query.SpanId, Is.EqualTo("0000000000000001"));
            Assert.That(query.OffsetMs, Is.EqualTo(10));
            Assert.That(query.DurationMs, Is.EqualTo(2.5));
            Assert.That(query.Status, Is.EqualTo("Unset"));
            Assert.That(query.Kind, Is.EqualTo("Client"));
        }
    }

    [TestCase("""{"trace":{"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"traefik"}}]},"scopeSpans":[{"scope":{"name":"t"},"spans":[{"spanId":"AQ==","name":"x","startTimeUnixNano":"1","endTimeUnixNano":"2"}]}]}]}}""")]
    [TestCase("{}")]
    public async Task A_trace_without_this_services_spans_is_null(string answer)
    {
        Assert.That(await Service(new FakeBackends(_ => Json(answer))).Trace("abc", default), Is.Null);
    }

    [Test]
    public async Task An_unknown_trace_is_null()
    {
        var backends = new FakeBackends(_ => Json("trace not found", HttpStatusCode.NotFound));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Service(backends).Trace("abc", default), Is.Null);
            Assert.That(await Service(backends).Trace("not-hex", default), Is.Null);
            Assert.That(backends.Asked, Has.Count.EqualTo(1), "a bad id is never asked for");
        }
    }

    [Test]
    public void A_backend_too_slow_is_a_502()
    {
        var service = Service(new FakeBackends(_ => throw new TaskCanceledException("timed out")));

        Assert.That(() => service.Logs(60, null, null, null, null, 10, default),
            Throws.TypeOf<TelemetryBackendException>().With.Message.EqualTo("Loki did not answer in time"));
    }

    [Test]
    public async Task A_line_without_a_level_is_unknown()
    {
        var backends = new FakeBackends(_ => Json("""
            {"status":"success","data":{"result":[{"stream":{"service_name":"grif-prod"},"values":[["1000000","plain"]]}]}}
            """));

        var entry = (await Service(backends).Logs(60, null, null, null, null, 10, default)).Entries.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo("unknown"));
            Assert.That(entry.Pod, Is.Null);
            Assert.That(entry.Source, Is.Null);
            Assert.That(entry.Attributes, Is.Empty);
        }
    }

    [Test]
    public async Task Requests_without_a_route_are_listed_as_such()
    {
        var backends = new FakeBackends(request => request.RequestUri!.AbsolutePath == "/api/v1/query"
            ? Json("""{"status":"success","data":{"resultType":"vector","result":[{"metric":{},"value":[1,"5"]}]}}""")
            : Json("""{"status":"success","data":{"resultType":"matrix","result":[]}}"""));

        var metrics = await Service(backends).Metrics(60, default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.Routes, Is.EqualTo(new[] { new RouteStatsDto("(no route)", 5, 5, 5) }));
            Assert.That(metrics.Panels.SelectMany(p => p.Series), Is.Empty);
        }
    }

    [Test]
    public async Task Odd_spans_still_read()
    {
        var backends = new FakeBackends(_ => Json("""
            {"trace":{"resourceSpans":[
              {"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"grif-prod"}},{"key":"service.instance.id","value":{"stringValue":"grif-worker-1"}}]},
               "scopeSpans":[{"spans":[
                 {"spanId":"not base64!","name":"job","startTimeUnixNano":"1000000","endTimeUnixNano":"3000000",
                  "attributes":[{"key":"ratio","value":{"doubleValue":1.5}},{"key":"map","value":{"kvlistValue":{"values":[]}}},{"key":"count","value":{"intValue":3}}]}]}]}
            ]}}
            """));

        var span = (await Service(backends).Trace("abc", default))!.Spans.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(span.SpanId, Is.EqualTo("not base64!"));
            Assert.That(span.Kind, Is.EqualTo("Internal"));
            Assert.That(span.Status, Is.EqualTo("Unset"));
            Assert.That(span.Scope, Is.Empty);
            Assert.That(span.Pod, Is.EqualTo("grif-worker-1"));
            Assert.That(span.Events, Is.Empty);
            Assert.That(span.DurationMs, Is.EqualTo(2));
            Assert.That(span.Attributes, Is.EqualTo(new Dictionary<string, string> { ["ratio"] = "1.5", ["map"] = """{"values":[]}""", ["count"] = "3" }));
        }
    }

    [Test]
    public void A_long_failure_is_cut_short()
    {
        var service = Service(new FakeBackends(_ => Json(new string('x', 400), HttpStatusCode.InternalServerError)));

        Assert.That(() => service.Traces(60, false, null, 10, default),
            Throws.TypeOf<TelemetryBackendException>().With.Message.EqualTo("Tempo answered 500: " + new string('x', 300) + "…"));
    }

    [Test]
    public async Task Empty_ids_kinds_and_values_read_as_empty()
    {
        var backends = new FakeBackends(_ => Json("""
            {"trace":{"resourceSpans":[
              {"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"grif-prod"}}]},
               "scopeSpans":[{"scope":{},"spans":[
                 {"spanId":"","parentSpanId":"","name":"odd","kind":"SPAN_KIND_","startTimeUnixNano":"1","endTimeUnixNano":"2","status":{"code":"custom"},
                  "attributes":[{"key":"nothing","value":{}}]}]}]}
            ]}}
            """));

        var span = (await Service(backends).Trace("abc", default))!.Spans.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(span.SpanId, Is.Empty);
            Assert.That(span.ParentSpanId, Is.Null);
            Assert.That(span.Kind, Is.Empty);
            Assert.That(span.Status, Is.EqualTo("Custom"));
            Assert.That(span.Scope, Is.Empty);
            Assert.That(span.Pod, Is.Null);
            Assert.That(span.Attributes["nothing"], Is.Empty);
        }
    }

    [Test]
    public void Not_json_is_a_502()
    {
        Assert.That(() => Service(new FakeBackends(_ => Json("<html>"))).Traces(60, false, null, 10, default),
            Throws.TypeOf<TelemetryBackendException>().With.Message.EqualTo("Tempo answered something that is not JSON"));
    }
}

[TestFixture]
public class TelemetryControllerTests
{
    private static TelemetryController Controller(Func<HttpRequestMessage, HttpResponseMessage> answer, bool configured = true) =>
        new(new TelemetryQueryService(new HttpClient(new Answering(answer)), Options.Create(new TelemetryQueryOptions
        {
            ServiceName = "grif-prod",
            LokiUrl = configured ? "http://loki.test" : null,
            PrometheusUrl = configured ? "http://prometheus.test" : null,
            TempoUrl = configured ? "http://tempo.test" : null,
        }), new FakeTimeProvider(DateTimeOffset.UtcNow)), Microsoft.Extensions.Logging.Abstractions.NullLogger<TelemetryController>.Instance);

    private sealed class Answering(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static readonly Func<HttpRequestMessage, HttpResponseMessage> Never = _ => throw new AssertionException("no backend call expected");

    [Test]
    public void Only_sysadmins()
    {
        var authorize = typeof(TelemetryController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single();
        Assert.That(authorize.Roles, Is.EqualTo("Sysadmin"));
    }

    [Test]
    public async Task Bad_input_is_a_400_before_any_query()
    {
        var controller = Controller(Never);

        using (Assert.EnterMultipleScope())
        {
            (await controller.Logs(level: ["loud"])).Result.AssertProblem(StatusCodes.Status400BadRequest);
            (await controller.Logs(traceId: "xyz")).Result.AssertProblem(StatusCodes.Status400BadRequest);
            (await controller.Logs(search: new string('a', TelemetryController.MaxSearchLength + 1))).Result.AssertProblem(StatusCodes.Status400BadRequest);
            (await controller.Trace("xyz")).Result.AssertProblem(StatusCodes.Status400BadRequest);
        }
    }

    [Test]
    public async Task Unconfigured_is_a_404_and_failing_a_502()
    {
        var unconfigured = Controller(Never, configured: false);
        var failing = Controller(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("down") });

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await unconfigured.Metrics()).Result, Is.TypeOf<ObjectResult>().With.Property("StatusCode").EqualTo(404));
            Assert.That((await unconfigured.Logs()).Result, Is.TypeOf<ObjectResult>().With.Property("StatusCode").EqualTo(404));
            Assert.That((await unconfigured.Traces()).Result, Is.TypeOf<ObjectResult>().With.Property("StatusCode").EqualTo(404));
            Assert.That((await unconfigured.Trace("abc")).Result, Is.TypeOf<ObjectResult>().With.Property("StatusCode").EqualTo(404));
            // The backend's own answer stays in the log
            Assert.That((await failing.Traces()).AssertProblem(502).Detail, Is.EqualTo(TelemetryController.BackendFailedDetail));
            Assert.That((await unconfigured.Traces()).AssertProblem(404).Detail, Does.Contain("Tempo is not configured"));
        }
    }

    [Test]
    public async Task Answers_pass_through()
    {
        var controller = Controller(request => request.RequestUri!.AbsolutePath.StartsWith("/api/v2/traces/")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Ok("""{"status":"success","data":{"result":[]}}"""));

        var found = Controller(_ => Ok("""{"trace":{"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"grif-prod"}}]},"scopeSpans":[{"spans":[{"spanId":"AQ==","name":"x","startTimeUnixNano":"1","endTimeUnixNano":"2"}]}]}]}}"""));
        Assert.That((await found.Trace("abc")).Value!.Spans.Single().SpanId, Is.EqualTo("01"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(controller.Status().Logs, Is.True);
            Assert.That((await controller.Logs(level: ["error"])).Value!.Entries, Is.Empty);
            (await controller.Trace("abc")).Result.AssertProblem(StatusCodes.Status404NotFound);
        }
    }
}
