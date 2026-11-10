using GrifballWebApp.Server.Telemetry.Query;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;

namespace GrifballWebApp.Server.Test;

/// <summary>TelemetryQueryService reading the shapes Tempo leaves fields out of, or nulls: each read as its default.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TelemetryQueryShapesTests
{
    private static readonly string[] TraceIds = ["not a trace id", "0000000000000000000000000000000a", "0000000000000000000000000000000b"];
    private static readonly string[] RootNames = ["", "", "GET x"];
    private static readonly string?[] RootServices = [null, "grif-prod", null];
    private static readonly double[] Durations = [0d, 0d, 5d];
    private static readonly string[] SpanNames = ["", "job"];
    private static readonly string[] Scopes = ["", "Quartz"];
    private static readonly string[] Kinds = ["Internal", "Internal"];
    private static readonly string[] Statuses = ["Unset", "Error"];
    private static readonly string?[] StatusMessages = [null, "boom"];
    private static readonly string?[] Parents = [null, "01"];
    private static readonly string[] EventNames = ["", "retry"];

    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private static TelemetryQueryService Service(string body) =>
        new(new HttpClient(new Answer(body)), Options.Create(new TelemetryQueryOptions
        {
            ServiceName = "grif-prod",
            TempoUrl = "https://tempo.test:3200",
        }), new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)));

    [Test]
    public async Task Search_results_without_their_optional_fields_read_as_empty()
    {
        var traces = await Service("""
            {"traces":[
              {"traceID":"not a trace id","startTimeUnixNano":"3000000"},
              {"traceID":"0a","rootTraceName":null,"rootServiceName":"grif-prod","startTimeUnixNano":"2000000","spanSet":{"matched":0}},
              {"traceID":"0b","rootTraceName":"GET x","startTimeUnixNano":"1000000","durationMs":5,
               "spanSet":{"spans":[{"spanID":"aa"},{"spanID":"bb","attributes":[{"key":"http.response.status_code","value":{"stringValue":"n/a"}}]}]}}
            ]}
            """).Traces(30, false, null, 10, default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(traces.Select(t => t.TraceId), Is.EqualTo(TraceIds));
            Assert.That(traces.Select(t => t.RootName), Is.EqualTo(RootNames));
            Assert.That(traces.Select(t => t.RootService), Is.EqualTo(RootServices));
            Assert.That(traces.Select(t => t.DurationMs), Is.EqualTo(Durations));
            Assert.That(traces.Select(t => t.Route), Is.All.Null);
            Assert.That(traces.Select(t => t.StatusCode), Is.All.Null);
        }
    }

    [Test]
    public async Task Spans_without_their_optional_fields_read_as_their_defaults()
    {
        var trace = await Service("""
            {"trace":{"resourceSpans":[
              {"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"grif-prod"}}]},
               "scopeSpans":[
                 {"scope":{"name":null},"spans":[
                   {"spanId":"AQ==","name":null,"kind":null,"startTimeUnixNano":"1000000","endTimeUnixNano":"2000000","status":"not an object",
                    "events":[{"name":null,"timeUnixNano":"1500000"},{"name":"retry","timeUnixNano":"1600000","attributes":[{"key":"n","value":{"stringValue":null}}]}],
                    "attributes":[{"key":"tags","value":{"arrayValue":{}}},{"key":"ids","value":{"arrayValue":{"values":[{"intValue":"1"},{"stringValue":"b"}]}}}]}]},
                 {"scope":{"name":"Quartz"},"spans":[
                   {"spanId":"Ag==","parentSpanId":"AQ==","name":"job","kind":"SPAN_KIND_INTERNAL","startTimeUnixNano":"1200000","endTimeUnixNano":"1800000",
                    "status":{"code":"STATUS_CODE_ERROR","message":"boom"}}]}]}
            ]}}
            """).Trace("0102", default);

        var spans = trace!.Spans;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(spans.Select(s => s.Name), Is.EqualTo(SpanNames));
            Assert.That(spans.Select(s => s.Scope), Is.EqualTo(Scopes));
            Assert.That(spans.Select(s => s.Kind), Is.EqualTo(Kinds));
            Assert.That(spans.Select(s => s.Status), Is.EqualTo(Statuses));
            Assert.That(spans.Select(s => s.StatusMessage), Is.EqualTo(StatusMessages));
            Assert.That(spans.Select(s => s.ParentSpanId), Is.EqualTo(Parents));
            Assert.That(spans[0].Events.Select(e => e.Name), Is.EqualTo(EventNames));
            Assert.That(spans[0].Events[1].Attributes["n"], Is.Empty);
            Assert.That(spans[0].Attributes, Is.EqualTo(new Dictionary<string, string> { ["tags"] = "[]", ["ids"] = "[1, b]" }));
            Assert.That(spans[1].Events, Is.Empty);
        }
    }
}
