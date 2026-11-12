using OpenTelemetry;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Telemetry.Browser;

/// <summary>
/// The resource the browser's spans and logs go under: this app's (its service.name, environment and attributes), as
/// <c>service.component=browser</c>, without this pod's instance id (a browser is no pod).
/// </summary>
public sealed class ClientTelemetryResource(TelemetryOptions telemetry)
{
    public const string Component = "browser";

    public IReadOnlyDictionary<string, string> Attributes { get; } = Build(telemetry);

    private static Dictionary<string, string> Build(TelemetryOptions telemetry)
    {
        var attributes = telemetry.ResourceAttributes
            .Where(a => a.Key != "service.instance.id" && a.Key != "service.version")
            .ToDictionary(a => a.Key, a => FormattableString.Invariant($"{a.Value}"));
        attributes["service.component"] = Component;
        return attributes;
    }
}

/// <summary>
/// The browser's attributes as they may be recorded: at most <see cref="MaxAttributes"/>, named like OpenTelemetry's
/// (lower case, dots), strings bounded and URLs' sensitive query values redacted, numbers and booleans as they are.
/// Nothing may claim a resource's name (service.*).
/// </summary>
public static partial class ClientAttributes
{
    public const int MaxAttributes = 32;
    public const int MaxValueLength = 1000;

    public static List<KeyValuePair<string, object>> Clean(IReadOnlyDictionary<string, JsonElement>? attributes)
    {
        var clean = new List<KeyValuePair<string, object>>();
        foreach (var (key, value) in attributes ?? new Dictionary<string, JsonElement>())
        {
            if (clean.Count == MaxAttributes)
                break;
            if (!Key().IsMatch(key) || key.StartsWith("service.", StringComparison.Ordinal))
                continue;
            object? kept = value.ValueKind switch
            {
                JsonValueKind.String => Text(key, value.GetString()!),
                JsonValueKind.Number when value.TryGetInt64(out var whole) => whole,
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
            if (kept is not null)
                clean.Add(new(key, kept));
        }
        return clean;
    }

    private static string Text(string key, string value)
    {
        var text = value.Length > MaxValueLength ? value[..MaxValueLength] : value;
        return key.StartsWith("url.", StringComparison.Ordinal) ? QueryRedaction.Redact(text) : text;
    }

    [GeneratedRegex("^[a-z][a-z0-9_.]{0,63}$")]
    private static partial Regex Key();
}

/// <summary>
/// The values of a URL's sensitive query parameters as REDACTED, as the browser does (grifballwebapp.client's
/// redactUrl.ts), again here: tokens, codes, keys, email, user, state...
/// </summary>
public static partial class QueryRedaction
{
    public const string Redacted = "REDACTED";

    public static string Redact(string url)
    {
        var question = url.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
            return url;
        var fragment = url.IndexOf('#', question);
        var query = fragment < 0 ? url[(question + 1)..] : url[(question + 1)..fragment];
        var pairs = query.Split('&').Select(pair =>
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? pair : pair[..equals];
            return equals >= 0 && Sensitive().IsMatch(Uri.UnescapeDataString(name)) ? $"{name}={Redacted}" : pair;
        });
        return url[..(question + 1)] + string.Join('&', pairs) + (fragment < 0 ? "" : url[fragment..]);
    }

    [GeneratedRegex("token|code|secret|passw|pwd|key|auth|session|sig|credential|cookie|email|user|state|nonce|otp|ticket|hash|jwt", RegexOptions.IgnoreCase)]
    private static partial Regex Sensitive();
}

/// <summary>OTLP/JSON (what a collector's /v1/traces and /v1/logs take) for a browser's batch.</summary>
public static class OtlpJson
{
    public const string Scope = "grifballwebapp.browser";

    private static readonly Dictionary<string, (int Number, string Text)> Severities = new()
    {
        ["debug"] = (5, "DEBUG"),
        ["info"] = (9, "INFO"),
        ["warn"] = (13, "WARN"),
        ["error"] = (17, "ERROR"),
    };

    public static JsonObject Traces(IReadOnlyDictionary<string, string> resource, IEnumerable<ClientSpanDto> spans) => new()
    {
        ["resourceSpans"] = new JsonArray(new JsonObject
        {
            ["resource"] = Resource(resource),
            ["scopeSpans"] = new JsonArray(new JsonObject
            {
                ["scope"] = new JsonObject { ["name"] = Scope },
                ["spans"] = new JsonArray(spans.Select(Span).ToArray<JsonNode?>()),
            }),
        }),
    };

    public static JsonObject Logs(IReadOnlyDictionary<string, string> resource, IEnumerable<ClientLogDto> logs) => new()
    {
        ["resourceLogs"] = new JsonArray(new JsonObject
        {
            ["resource"] = Resource(resource),
            ["scopeLogs"] = new JsonArray(new JsonObject
            {
                ["scope"] = new JsonObject { ["name"] = Scope },
                ["logRecords"] = new JsonArray(logs.Select(Log).ToArray<JsonNode?>()),
            }),
        }),
    };

    private static JsonObject Span(ClientSpanDto span)
    {
        var json = new JsonObject
        {
            ["traceId"] = span.TraceId,
            ["spanId"] = span.SpanId,
            ["name"] = span.Name,
            // SPAN_KIND_INTERNAL, SPAN_KIND_CLIENT
            ["kind"] = span.Kind == "client" ? 3 : 1,
            ["startTimeUnixNano"] = Nanos(span.Start),
            ["endTimeUnixNano"] = Nanos(Math.Max(span.Start, span.End)),
            ["attributes"] = Attributes(ClientAttributes.Clean(span.Attributes)),
            // STATUS_CODE_UNSET, OK, ERROR
            ["status"] = new JsonObject { ["code"] = span.Status switch { "ok" => 1, "error" => 2, _ => 0 } },
        };
        if (span.ParentSpanId is not null)
            json["parentSpanId"] = span.ParentSpanId;
        return json;
    }

    private static JsonObject Log(ClientLogDto log)
    {
        var (number, text) = Severities[log.Level];
        var json = new JsonObject
        {
            ["timeUnixNano"] = Nanos(log.Time),
            ["severityNumber"] = number,
            ["severityText"] = text,
            ["body"] = new JsonObject { ["stringValue"] = log.Message },
            ["attributes"] = Attributes(ClientAttributes.Clean(log.Attributes)),
        };
        if (log.TraceId is not null && log.SpanId is not null)
        {
            json["traceId"] = log.TraceId;
            json["spanId"] = log.SpanId;
        }
        return json;
    }

    private static JsonObject Resource(IReadOnlyDictionary<string, string> resource) => new()
    {
        ["attributes"] = Attributes(resource.Select(a => new KeyValuePair<string, object>(a.Key, a.Value))),
    };

    private static JsonArray Attributes(IEnumerable<KeyValuePair<string, object>> attributes) =>
        new(attributes.Select(a => (JsonNode?)new JsonObject { ["key"] = a.Key, ["value"] = Value(a.Value) }).ToArray());

    private static JsonObject Value(object value) => value switch
    {
        bool b => new JsonObject { ["boolValue"] = b },
        // OTLP/JSON writes 64-bit integers as strings
        long l => new JsonObject { ["intValue"] = l.ToString(CultureInfo.InvariantCulture) },
        double d => new JsonObject { ["doubleValue"] = d },
        _ => new JsonObject { ["stringValue"] = Convert.ToString(value, CultureInfo.InvariantCulture) },
    };

    /// <summary>
    /// Epoch milliseconds (with a fraction) as OTLP's nanoseconds, a string. In integers: the product as a double
    /// (about 1.8e18, past 2^53) would be off by tens of nanoseconds.
    /// </summary>
    public static string Nanos(double epochMs)
    {
        var whole = Math.Floor(epochMs);
        var nanos = (long)whole * 1_000_000 + (long)Math.Round((epochMs - whole) * 1_000_000);
        return nanos.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>Sends a browser's spans and logs on to the collector (ClientTelemetry:OtlpEndpoint), OTLP/JSON over HTTP.</summary>
public partial class ClientTelemetryForwarder(HttpClient http, ClientTelemetryResource resource,
    Microsoft.Extensions.Options.IOptions<ClientTelemetryOptions> options, ILogger<ClientTelemetryForwarder> logger)
{
    public async Task Forward(ClientTelemetryBatchDto batch, string userAgent, CancellationToken ct)
    {
        var endpoint = options.Value.OtlpUri;
        if (endpoint is null || (batch.Spans.Count == 0 && batch.Logs.Count == 0))
            return;

        var attributes = resource.Attributes.ToDictionary(a => a.Key, a => a.Value);
        if (!string.IsNullOrWhiteSpace(userAgent))
            attributes["user_agent.original"] = userAgent.Length > ClientAttributes.MaxValueLength ? userAgent[..ClientAttributes.MaxValueLength] : userAgent;

        // Not this request's own HttpClient span: every batch would make one
        using var suppressed = SuppressInstrumentationScope.Begin();
        if (batch.Spans.Count > 0)
            await Post(endpoint, "traces", OtlpJson.Traces(attributes, batch.Spans), ct);
        if (batch.Logs.Count > 0)
            await Post(endpoint, "logs", OtlpJson.Logs(attributes, batch.Logs), ct);
    }

    private async Task Post(Uri endpoint, string signal, JsonObject body, CancellationToken ct)
    {
        var url = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/v1/" + signal);
        try
        {
            using var content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
                LogRefused(logger, signal, (int)response.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Telemetry is best effort: the browser's batch is dropped, the request still succeeds
            LogFailed(logger, e, signal);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The collector refused the browser's {Signal} ({Status})")]
    private static partial void LogRefused(ILogger logger, string signal, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending the browser's {Signal} to the collector failed")]
    private static partial void LogFailed(ILogger logger, Exception e, string signal);
}

/// <summary>
/// The browser's Web Vitals as this app's metrics: grif.browser.lcp, .inp, .fcp and .ttfb in seconds, grif.browser.cls
/// unitless. The Telemetry page charts their p75 (Google's mark). Over the whole site, with no label a browser sets:
/// anyone may send a batch, and each new label value would be a new series in Prometheus.
/// </summary>
public sealed class BrowserMetrics : IDisposable
{
    public const string MeterName = "GrifballWebApp.Browser";

    private static readonly double[] SecondsBuckets = [0.05, 0.1, 0.2, 0.3, 0.5, 0.75, 1, 1.5, 2, 2.5, 3, 4, 5, 7.5, 10];
    private static readonly double[] ShiftBuckets = [0.01, 0.025, 0.05, 0.1, 0.15, 0.25, 0.5, 1];

    private readonly Meter _meter = new(MeterName);
    private readonly Dictionary<string, Histogram<double>> _histograms;

    public BrowserMetrics()
    {
        Histogram<double> Seconds(string name, string description) =>
            _meter.CreateHistogram(name, "s", description, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = SecondsBuckets });
        _histograms = new()
        {
            ["LCP"] = Seconds("grif.browser.lcp", "Largest Contentful Paint"),
            ["INP"] = Seconds("grif.browser.inp", "Interaction to Next Paint"),
            ["FCP"] = Seconds("grif.browser.fcp", "First Contentful Paint"),
            ["TTFB"] = Seconds("grif.browser.ttfb", "Time to First Byte"),
            ["CLS"] = _meter.CreateHistogram("grif.browser.cls", null, "Cumulative Layout Shift",
                advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ShiftBuckets }),
        };
    }

    public void Record(ClientVitalDto vital)
    {
        if (!_histograms.TryGetValue(vital.Name, out var histogram))
            return;
        // web-vitals reports milliseconds, CLS as a score
        var value = vital.Name == "CLS" ? vital.Value : vital.Value / 1000;
        histogram.Record(value);
    }

    public void Dispose() => _meter.Dispose();
}
