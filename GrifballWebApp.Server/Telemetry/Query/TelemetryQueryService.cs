using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace GrifballWebApp.Server.Telemetry.Query;

/// <summary>A query the page cannot answer, and the status it answers instead.</summary>
public abstract class TelemetryQueryException(int status, string message, Exception? inner = null) : Exception(message, inner)
{
    public int Status { get; } = status;
}

/// <summary>A backend is unset (<see cref="TelemetryQueryOptions"/>): 404.</summary>
public sealed class TelemetryBackendMissingException(string backend)
    : TelemetryQueryException(StatusCodes.Status404NotFound, $"{backend} is not configured ({TelemetryQueryOptions.Section}:{backend}Url)");

/// <summary>A backend failed or answered something unreadable: 502.</summary>
public sealed class TelemetryBackendException(string message, Exception? inner = null)
    : TelemetryQueryException(StatusCodes.Status502BadGateway, message, inner);

/// <summary>
/// The Telemetry page's reads: this app's logs from Loki, its metrics from Prometheus and its traces from Tempo, each
/// query written by <see cref="TelemetryQueries"/> and scoped to <see cref="TelemetryQueryOptions.ServiceName"/>.
/// </summary>
public sealed class TelemetryQueryService
{
    public const int MaxLogLines = 1000;
    public const int MaxTraces = 100;
    public static readonly TimeSpan MinRange = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(7);
    /// <summary>About this many points per chart.</summary>
    public const int ChartPoints = 150;
    private const int RoutesShown = 15;

    /// <summary>Resource attributes, the same on every line of a pod: left out of a log line's attributes.</summary>
    private static readonly string[] LogNoisePrefixes = ["k8s_", "service_", "telemetry_sdk_"];
    private static readonly HashSet<string> LogNoise =
    [
        "cluster", "deployment_environment_name", "observed_timestamp", "severity_number", "severity_text",
        "detected_level", "trace_id", "span_id", "scope_name", "message_template_text", "SourceContext",
    ];

    private readonly HttpClient _http;
    private readonly TelemetryQueryOptions _options;
    private readonly TimeProvider _time;

    public TelemetryQueryService(HttpClient http, IOptions<TelemetryQueryOptions> options, TimeProvider time)
    {
        _http = http;
        _options = options.Value;
        _time = time;
    }

    public TelemetryStatusDto Status() =>
        new(_options.ServiceName, _options.Loki is not null, _options.Prometheus is not null, _options.Tempo is not null);

    /// <summary>The range ending at <paramref name="end"/> (default now), <paramref name="minutes"/> long, clamped to <see cref="MinRange"/>-<see cref="MaxRange"/>.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) Range(int minutes, DateTimeOffset? end = null)
    {
        var now = _time.GetUtcNow();
        var to = end is { } e && e < now ? e : now;
        var length = TimeSpan.FromMinutes(Math.Clamp(minutes, MinRange.TotalMinutes, MaxRange.TotalMinutes));
        return (to - length, to);
    }

    public async Task<LogsDto> Logs(int minutes, DateTimeOffset? end, IEnumerable<string>? levels, string? search, string? traceId, int limit, CancellationToken ct)
    {
        var loki = _options.Loki ?? throw new TelemetryBackendMissingException("Loki");
        var (from, to) = Range(minutes, end);
        limit = Math.Clamp(limit, 1, MaxLogLines);
        var query = TelemetryQueries.Logs(_options.ServiceName, levels, search, TelemetryQueries.NormalizeTraceId(traceId));
        var url = Url(loki, "loki/api/v1/query_range",
            ("query", query), ("start", UnixNanos(from)), ("end", UnixNanos(to)), ("limit", limit.ToString(CultureInfo.InvariantCulture)), ("direction", "backward"));

        using var doc = await Get("Loki", url, ct);
        var entries = new List<LogEntryDto>();
        foreach (var stream in doc.RootElement.GetProperty("data").GetProperty("result").EnumerateArray())
        {
            var labels = stream.GetProperty("stream").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            foreach (var value in stream.GetProperty("values").EnumerateArray())
                entries.Add(LogEntry(labels, long.Parse(value[0].GetString()!, CultureInfo.InvariantCulture), value[1].GetString() ?? ""));
        }

        entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return new LogsDto(entries.Take(limit).ToList(), entries.Count >= limit);
    }

    public async Task<MetricsDto> Metrics(int minutes, CancellationToken ct)
    {
        var prometheus = _options.Prometheus ?? throw new TelemetryBackendMissingException("Prometheus");
        var (from, to) = Range(minutes);
        var range = to - from;
        var step = TimeSpan.FromSeconds(Math.Max(15, Math.Ceiling(range.TotalSeconds / ChartPoints)));
        var window = TimeSpan.FromSeconds(Math.Max(120, step.TotalSeconds * 2));

        var panels = TelemetryQueries.Panels(_options.ServiceName, window).Select(async panel =>
        {
            var url = Url(prometheus, "api/v1/query_range",
                ("query", panel.Query), ("start", UnixSeconds(from)), ("end", UnixSeconds(to)), ("step", TelemetryQueries.Duration(step)));
            using var doc = await Get("Prometheus", url, ct);
            var series = Result(doc).Select(r => new MetricSeriesDto(
                SeriesName(r, panel),
                r.GetProperty("values").EnumerateArray()
                    .Select(v => (Time: v[0].GetDouble(), Value: ParseValue(v[1])))
                    .Where(v => v.Value is not null)
                    .Select(v => new[] { Math.Round(v.Time * 1000), v.Value!.Value })
                    .ToList())).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
            return new MetricPanelDto(panel.Key, panel.Title, panel.Unit, series);
        }).ToList();

        var (requests, errors, p95) = TelemetryQueries.Routes(_options.ServiceName, range);
        var routeQueries = new[] { requests, errors, p95 }.Select(async q =>
        {
            using var doc = await Get("Prometheus", Url(prometheus, "api/v1/query", ("query", q), ("time", UnixSeconds(to))), ct);
            return Result(doc).ToDictionary(
                r => r.GetProperty("metric").TryGetProperty("http_route", out var route) ? route.GetString() ?? "" : "",
                r => ParseValue(r.GetProperty("value")[1]));
        }).ToList();

        var results = await Task.WhenAll(panels);
        var byRoute = await Task.WhenAll(routeQueries);
        var routes = byRoute[0]
            .Where(r => r.Value is > 0)
            .OrderByDescending(r => r.Value)
            .Take(RoutesShown)
            .Select(r => new RouteStatsDto(
                r.Key.Length == 0 ? "(no route)" : r.Key,
                Math.Round(r.Value!.Value),
                Math.Round(byRoute[1].GetValueOrDefault(r.Key) ?? 0),
                byRoute[2].GetValueOrDefault(r.Key) is { } ms ? Math.Round(ms, 1) : null))
            .ToList();

        return new MetricsDto(from, to, (int)step.TotalSeconds, results, routes);
    }

    public async Task<List<TraceSummaryDto>> Traces(int minutes, bool errorsOnly, int? minDurationMs, int limit, CancellationToken ct)
    {
        var tempo = _options.Tempo ?? throw new TelemetryBackendMissingException("Tempo");
        var (from, to) = Range(minutes);
        var url = Url(tempo, "api/search",
            ("q", TelemetryQueries.Traces(_options.ServiceName, errorsOnly, minDurationMs)),
            ("start", from.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ("end", to.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ("limit", Math.Clamp(limit, 1, MaxTraces).ToString(CultureInfo.InvariantCulture)),
            // A few spans a trace: the root alone is the app's server's /api/* when a browser's call started it
            ("spss", "3"));

        using var doc = await Get("Tempo", url, ct);
        if (!doc.RootElement.TryGetProperty("traces", out var traces))
            return [];

        return traces.EnumerateArray().Select(TraceSummary).OrderByDescending(t => t.Start).ToList();
    }

    /// <summary>A trace as Tempo's search lists it, with its span set's attributes (the first value of each).</summary>
    private static TraceSummaryDto TraceSummary(JsonElement t)
    {
        var values = t.TryGetProperty("spanSet", out var set) && set.TryGetProperty("spans", out var spans)
            ? spans.EnumerateArray().SelectMany(s => s.TryGetProperty("attributes", out var a) ? a.EnumerateArray() : Enumerable.Empty<JsonElement>())
                .GroupBy(a => a.GetProperty("key").GetString()!).ToDictionary(g => g.Key, g => g.Select(a => AttributeValue(a.GetProperty("value"))).ToList())
            : new Dictionary<string, List<string>>();
        var attributes = values.ToDictionary(v => v.Key, v => v.Value[0]);
        // The backend's route (Season/Get) over the app's server's (/api/*), which is the root of a browser's call
        var route = TelemetryQueries.PreferredRoute(values.GetValueOrDefault("http.route") ?? []);
        var rootName = t.TryGetProperty("rootTraceName", out var name) ? name.GetString() ?? "" : "";
        return new TraceSummaryDto(
            TelemetryQueries.NormalizeTraceId(t.GetProperty("traceID").GetString()) ?? t.GetProperty("traceID").GetString()!,
            TelemetryQueries.TraceName(rootName, route),
            t.TryGetProperty("rootServiceName", out var service) ? service.GetString() : null,
            FromUnixNanos(long.Parse(t.GetProperty("startTimeUnixNano").GetString()!, CultureInfo.InvariantCulture)),
            t.TryGetProperty("durationMs", out var duration) ? duration.GetDouble() : 0,
            route,
            int.TryParse(attributes.GetValueOrDefault("http.response.status_code"), out var status) ? status : null);
    }

    /// <summary>A trace's spans from this service; null when it has none (unknown trace, or another service's).</summary>
    public async Task<TraceDto?> Trace(string traceId, CancellationToken ct)
    {
        var tempo = _options.Tempo ?? throw new TelemetryBackendMissingException("Tempo");
        var id = TelemetryQueries.NormalizeTraceId(traceId);
        if (id is null)
            return null;

        using var response = await Send("Tempo", new Uri(tempo, $"api/v2/traces/{id}"), ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        using var doc = await Read("Tempo", response, ct);
        if (!doc.RootElement.TryGetProperty("trace", out var trace) || !trace.TryGetProperty("resourceSpans", out var resourceSpans))
            return null;

        var spans = resourceSpans.EnumerateArray().SelectMany(ServiceSpans).ToList();
        if (spans.Count == 0)
            return null;

        var start = spans.Min(s => s.Start);
        var end = spans.Max(s => s.End);
        return new TraceDto(
            id,
            FromUnixNanos(start),
            Millis(end - start),
            spans.OrderBy(s => s.Start).Select(s => new SpanDto(
                s.SpanId, s.Parent, s.Name, Title(s.Kind), s.Scope, s.Pod, Millis(s.Start - start), Millis(s.End - s.Start),
                Title(s.Status), string.IsNullOrEmpty(s.StatusMessage) ? null : s.StatusMessage, s.SpanAttributes,
                s.Events.Select(e => new SpanEventDto(e.Name, Millis(e.Time - start), e.Attributes)).ToList())).ToList());
    }

    /// <summary>A trace's span as Tempo's v2 JSON has it, its times in Unix nanoseconds.</summary>
    private sealed record RawSpan(long Start, long End, string SpanId, string? Parent, string Name, string Kind, string Scope, string? Pod, string Status,
        string? StatusMessage, Dictionary<string, string> SpanAttributes, List<(string Name, long Time, Dictionary<string, string> Attributes)> Events);

    /// <summary>A resource's spans when it is this service (none for another's), with its pod and each span's scope.</summary>
    private IEnumerable<RawSpan> ServiceSpans(JsonElement resource)
    {
        var resourceAttributes = Attributes(resource.GetProperty("resource"));
        if (resourceAttributes.GetValueOrDefault("service.name") != _options.ServiceName)
            return [];
        var pod = resourceAttributes.GetValueOrDefault("k8s.pod.name") ?? resourceAttributes.GetValueOrDefault("service.instance.id");
        return resource.GetProperty("scopeSpans").EnumerateArray().SelectMany(scopeSpans =>
        {
            var scope = scopeSpans.TryGetProperty("scope", out var s) && s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            return scopeSpans.GetProperty("spans").EnumerateArray().Select(span => Span(span, scope, pod));
        });
    }

    private static RawSpan Span(JsonElement span, string scope, string? pod)
    {
        var status = span.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Object ? st : (JsonElement?)null;
        return new RawSpan(
            long.Parse(span.GetProperty("startTimeUnixNano").GetString()!, CultureInfo.InvariantCulture),
            long.Parse(span.GetProperty("endTimeUnixNano").GetString()!, CultureInfo.InvariantCulture),
            Hex(span.GetProperty("spanId").GetString()),
            span.TryGetProperty("parentSpanId", out var parent) && parent.GetString() is { Length: > 0 } p ? Hex(p) : null,
            span.GetProperty("name").GetString() ?? "",
            Trim(span.TryGetProperty("kind", out var kind) ? kind.GetString() : null, "SPAN_KIND_") ?? "Internal",
            scope,
            pod,
            Trim(status?.TryGetProperty("code", out var code) is true ? code.GetString() : null, "STATUS_CODE_") ?? "Unset",
            status?.TryGetProperty("message", out var message) is true ? message.GetString() : null,
            Attributes(span),
            span.TryGetProperty("events", out var events)
                ? events.EnumerateArray().Select(e => (
                    e.GetProperty("name").GetString() ?? "",
                    long.Parse(e.GetProperty("timeUnixNano").GetString()!, CultureInfo.InvariantCulture),
                    Attributes(e))).ToList()
                : []);
    }

    private static LogEntryDto LogEntry(Dictionary<string, string> labels, long nanos, string line)
    {
        var attributes = labels
            .Where(l => !LogNoise.Contains(l.Key) && !LogNoisePrefixes.Any(p => l.Key.StartsWith(p, StringComparison.Ordinal)))
            .OrderBy(l => l.Key, StringComparer.Ordinal)
            .ToDictionary(l => l.Key, l => l.Value);
        return new LogEntryDto(
            FromUnixNanos(nanos),
            labels.GetValueOrDefault("detected_level") ?? labels.GetValueOrDefault("severity_text")?.ToLowerInvariant() ?? "unknown",
            line,
            labels.GetValueOrDefault("trace_id"),
            labels.GetValueOrDefault("span_id"),
            labels.GetValueOrDefault("SourceContext") ?? labels.GetValueOrDefault("scope_name"),
            labels.GetValueOrDefault("k8s_pod_name") ?? labels.GetValueOrDefault("service_instance_id"),
            attributes);
    }

    private static string SeriesName(JsonElement result, MetricPanelQuery panel) =>
        panel.SeriesLabel is { } label && result.GetProperty("metric").TryGetProperty(label, out var value) && value.GetString() is { Length: > 0 } name
            ? name
            : panel.Title;

    private static JsonElement.ArrayEnumerator Result(JsonDocument doc) =>
        doc.RootElement.GetProperty("data").GetProperty("result").EnumerateArray();

    /// <summary>A Prometheus sample's value; NaN and infinities (an empty histogram, a division by zero) are no value.</summary>
    private static double? ParseValue(JsonElement value) =>
        double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;

    private static Dictionary<string, string> Attributes(JsonElement owner)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (owner.TryGetProperty("attributes", out var attributes))
            foreach (var a in attributes.EnumerateArray())
                result[a.GetProperty("key").GetString()!] = AttributeValue(a.GetProperty("value"));
        return result;
    }

    /// <summary>An OTLP AnyValue as text.</summary>
    private static string AttributeValue(JsonElement value) =>
        value.EnumerateObject().Select(p => p.Name switch
        {
            "stringValue" => p.Value.GetString() ?? "",
            "intValue" => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(),
            "doubleValue" or "boolValue" => p.Value.GetRawText(),
            "arrayValue" => "[" + string.Join(", ", p.Value.TryGetProperty("values", out var values) ? values.EnumerateArray().Select(AttributeValue) : Enumerable.Empty<string>()) + "]",
            _ => p.Value.GetRawText(),
        }).FirstOrDefault() ?? "";

    /// <summary>Tempo's v2 JSON carries ids in base64 (protobuf bytes); hex is what the logs and the search use.</summary>
    private static string Hex(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
            return "";
        try
        {
            return Convert.ToHexStringLower(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            return base64;
        }
    }

    private static string? Trim(string? value, string prefix) =>
        value is null ? null : value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;

    /// <summary>SERVER to Server, ERROR to Error.</summary>
    private static string Title(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    private static double Millis(long nanos) => Math.Round(nanos / 1_000_000d, 3);

    private static DateTimeOffset FromUnixNanos(long nanos) =>
        DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000).AddTicks(nanos % 1_000_000 / 100);

    private static string UnixNanos(DateTimeOffset time) =>
        ((time.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100).ToString(CultureInfo.InvariantCulture);

    private static string UnixSeconds(DateTimeOffset time) =>
        (time.ToUnixTimeMilliseconds() / 1000d).ToString("0.###", CultureInfo.InvariantCulture);

    private static Uri Url(Uri baseUri, string path, params (string Key, string Value)[] query) =>
        new(baseUri, path + "?" + string.Join('&', query.Select(q => q.Key + "=" + Uri.EscapeDataString(q.Value))));

    private async Task<JsonDocument> Get(string backend, Uri url, CancellationToken ct)
    {
        using var response = await Send(backend, url, ct);
        return await Read(backend, response, ct);
    }

    private async Task<HttpResponseMessage> Send(string backend, Uri url, CancellationToken ct)
    {
        try
        {
            return await _http.GetAsync(url, ct);
        }
        catch (HttpRequestException e)
        {
            throw new TelemetryBackendException($"{backend} could not be reached: {e.Message}", e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new TelemetryBackendException($"{backend} did not answer in time", e);
        }
    }

    private static async Task<JsonDocument> Read(string backend, HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new TelemetryBackendException($"{backend} answered {(int)response.StatusCode}: {Shorten(body)}");
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException e)
        {
            throw new TelemetryBackendException($"{backend} answered something that is not JSON", e);
        }
    }

    private static string Shorten(string body)
    {
        body = body.Trim();
        return body.Length <= 300 ? body : body[..300] + "…";
    }
}
