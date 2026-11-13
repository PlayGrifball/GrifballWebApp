using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Telemetry.Query;

/// <summary>
/// The LogQL, PromQL and TraceQL the Telemetry page runs. The server writes every query: the page only picks levels, a
/// time range and a search text, which go in as escaped literals, and each query is pinned to this app's service.name -
/// so a sysadmin sees this app's telemetry, never the rest of what the backends hold.
/// </summary>
public static partial class TelemetryQueries
{
    /// <summary>Loki's detected_level values, from the logs' severity (Serilog's Verbose is trace, Information info).</summary>
    public static readonly IReadOnlyList<string> Levels = ["trace", "debug", "info", "warn", "error", "fatal"];

    /// <summary>Health probes: every few seconds on every pod, left out of the request panels.</summary>
    public const string HealthRoutes = "/health/.*";

    /// <summary>A double-quoted string literal, as LogQL, PromQL and TraceQL read it.</summary>
    public static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>Text matched literally by an RE2 regex: its metacharacters escaped (.NET's Regex.Escape also escapes spaces and #, which RE2 rejects).</summary>
    public static string Re2Escape(string text) => Re2Meta().Replace(text, @"\$0");

    /// <summary>Trace ids are 32 hex digits; Tempo drops leading zeros in its search results, so fewer are padded back.</summary>
    public static string? NormalizeTraceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        id = id.Trim().ToLowerInvariant();
        return HexId().IsMatch(id) ? id.PadLeft(32, '0') : null;
    }

    /// <summary>The service's logs: some levels only (none: all of them), lines containing <paramref name="search"/> (any case), one trace's only.</summary>
    public static string Logs(string serviceName, IEnumerable<string>? levels, string? search, string? traceId)
    {
        var query = new StringBuilder("{service_name=").Append(Quote(serviceName)).Append('}');
        var picked = levels?.Distinct().ToList() ?? [];
        if (picked.Count > 0 && picked.Count < Levels.Count)
            query.Append(" | detected_level=~").Append(Quote(string.Join('|', picked)));
        if (!string.IsNullOrWhiteSpace(search))
            query.Append(" |~ ").Append(Quote("(?i)" + Re2Escape(search.Trim())));
        if (traceId is not null)
            query.Append(" | trace_id=").Append(Quote(traceId));
        return query.ToString();
    }

    /// <summary>The label matcher every metric query starts from.</summary>
    public static string Selector(string serviceName) => "service_name=" + Quote(serviceName);

    /// <summary>A PromQL duration in seconds.</summary>
    public static string Duration(TimeSpan span) => ((long)Math.Max(1, span.TotalSeconds)).ToString(CultureInfo.InvariantCulture) + "s";

    /// <summary>
    /// The Overview's charts. <paramref name="window"/> is each rate's window: wider than the step, so no point falls
    /// between two of the app's pushes (every 30 s).
    /// </summary>
    public static IReadOnlyList<MetricPanelQuery> Panels(string serviceName, TimeSpan window)
    {
        var s = Selector(serviceName);
        var w = Duration(window);
        var requests = $"{s},http_route!~{Quote(HealthRoutes)}";
        return
        [
            new("requests", "Requests", "req/s", $"sum(rate(http_server_request_duration_seconds_count{{{requests}}}[{w}]))"),
            new("errors", "Server errors (5xx)", "req/s", $"sum(rate(http_server_request_duration_seconds_count{{{requests},http_response_status_code=~\"5..\"}}[{w}]))"),
            new("latency", "Response time (p95)", "ms", $"histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{{{requests}}}[{w}]))) * 1000"),
            new("database", "Database calls (p95)", "ms", $"histogram_quantile(0.95, sum by (le) (rate(db_client_operation_duration_seconds_bucket{{{s}}}[{w}]))) * 1000"),
            new("exceptions", "Exceptions", "/min", $"sum(rate(dotnet_exceptions_total{{{s}}}[{w}])) * 60"),
            new("memory", "Memory (working set)", "MB", $"sum by (k8s_pod_name) (dotnet_process_memory_working_set_bytes{{{s}}}) / 1048576", "k8s_pod_name"),
            new("cpu", "CPU", "cores", $"sum by (k8s_pod_name) (rate(dotnet_process_cpu_time_seconds_total{{{s}}}[{w}]))", "k8s_pod_name"),
            // SignalR's connections (the team builder's, the Late League queue's), by transport: WebSockets, or a fallback
            new("hubConnections", "Hub connections", "connections", $"sum by (signalr_transport) (signalr_server_active_connections{{{s}}})", "signalr_transport"),
            // The app's server (grifballwebapp.client/src/server.telemetry.ts), which shares this service.name
            new("render", "Page render on the server (p95)", "ms", $"histogram_quantile(0.95, sum by (le) (rate(grif_frontend_render_duration_seconds_bucket{{{s},outcome=\"server\"}}[{w}]))) * 1000"),
            new("renderFailures", "Pages left to the browser after failing", "/min", $"sum(rate(grif_frontend_render_duration_seconds_count{{{s},outcome=\"failed\"}}[{w}])) * 60"),
            new("frontendMemory", "Frontend memory (resident)", "MB", $"sum by (k8s_pod_name) (grif_frontend_memory_rss_bytes{{{s}}}) / 1048576", "k8s_pod_name"),
            // The browser's Web Vitals (Browser/BrowserMetrics), at p75 as Google rates them
            new("lcp", "Largest Contentful Paint (p75)", "ms", $"histogram_quantile(0.75, sum by (le) (rate(grif_browser_lcp_seconds_bucket{{{s}}}[{w}]))) * 1000"),
            new("inp", "Interaction to Next Paint (p75)", "ms", $"histogram_quantile(0.75, sum by (le) (rate(grif_browser_inp_seconds_bucket{{{s}}}[{w}]))) * 1000"),
            new("cls", "Cumulative Layout Shift (p75)", "score", $"histogram_quantile(0.75, sum by (le) (rate(grif_browser_cls_bucket{{{s}}}[{w}])))"),
        ];
    }

    /// <summary>The Overview's routes table, each over the whole range: requests, 5xx responses, p95 in ms.</summary>
    public static (string Requests, string Errors, string P95) Routes(string serviceName, TimeSpan range)
    {
        var requests = $"{Selector(serviceName)},http_route!~{Quote(HealthRoutes)}";
        var r = Duration(range);
        return (
            $"sum by (http_route) (increase(http_server_request_duration_seconds_count{{{requests}}}[{r}]))",
            $"sum by (http_route) (increase(http_server_request_duration_seconds_count{{{requests},http_response_status_code=~\"5..\"}}[{r}]))",
            $"histogram_quantile(0.95, sum by (le, http_route) (rate(http_server_request_duration_seconds_bucket{{{requests}}}[{r}]))) * 1000");
    }

    /// <summary>
    /// The span attribute the app's server (and the browser) set on the spans of a request they handle: page, navigation,
    /// api, hub, signin (grifballwebapp.client's REQUEST_KIND_ATTRIBUTE). The backend's own spans have none.
    /// </summary>
    public const string RequestKindAttribute = "grif.request.kind";

    /// <summary>
    /// The Traces tab's search: the service's traces, failed ones only, slower than a minimum. Selects each matched span's
    /// route, status code, method and request kind, which <see cref="TraceRow"/> reads. <paramref name="requestSpansOnly"/>:
    /// only the spans of a request (a route or a request kind), the rows' second search - a busy trace's first matched
    /// spans may be all database calls.
    /// </summary>
    public static string Traces(string serviceName, bool errorsOnly, int? minDurationMs, bool requestSpansOnly = false)
    {
        var query = new StringBuilder("{resource.service.name=").Append(Quote(serviceName));
        if (requestSpansOnly)
            query.Append(" && (span.http.route != nil || span.").Append(RequestKindAttribute).Append(" != nil)");
        if (errorsOnly)
            query.Append(" && status=error");
        if (minDurationMs is > 0)
            query.Append(" && duration>=").Append(minDurationMs.Value.ToString(CultureInfo.InvariantCulture)).Append("ms");
        return query.Append("} | select(span.http.route, span.http.response.status_code, span.http.request.method, span.")
            .Append(RequestKindAttribute).Append(')').ToString();
    }

    /// <summary>
    /// A trace's row in the list, from its matched spans' attributes (by what each span is, never by its name):
    /// <list type="bullet">
    /// <item>A page or a navigation (a span whose request kind says so): its root's name, and that span's route and status.
    /// The backend calls its render made are only parts of it.</item>
    /// <item>An API call passed on by the app's server (request kind api): its method and the backend's route, and the
    /// backend's status - the frontend only knows /api/*.</item>
    /// <item>Anything else (a job, a call straight to the API): its root's name, and the first route and status.</item>
    /// </list>
    /// </summary>
    public static (string Name, string? Route, int? Status) TraceRow(string rootName, IReadOnlyList<IReadOnlyDictionary<string, string>> spans)
    {
        static string? Kind(IReadOnlyDictionary<string, string> span) => span.GetValueOrDefault(RequestKindAttribute);
        static int? Status(IReadOnlyDictionary<string, string>? span) =>
            int.TryParse(span?.GetValueOrDefault("http.response.status_code"), CultureInfo.InvariantCulture, out var status) ? status : null;

        var page = spans.FirstOrDefault(s => Kind(s) is "page" or "navigation");
        if (page is not null)
            return (rootName, page.GetValueOrDefault("http.route"), Status(page));

        var call = spans.FirstOrDefault(s => Kind(s) == "api");
        var backend = spans.FirstOrDefault(s => Kind(s) is null && s.ContainsKey("http.route"));
        if (call is not null && backend is not null)
        {
            var route = backend["http.route"];
            var method = backend.GetValueOrDefault("http.request.method") ?? call.GetValueOrDefault("http.request.method");
            return (method is null ? route : $"{method} {route}", route, Status(backend));
        }

        var routed = spans.FirstOrDefault(s => s.ContainsKey("http.route"));
        return (rootName, routed?.GetValueOrDefault("http.route"), Status(routed ?? spans.FirstOrDefault(s => s.ContainsKey("http.response.status_code"))));
    }

    [GeneratedRegex(@"[\\.+*?()|\[\]{}^$]")]
    private static partial Regex Re2Meta();

    [GeneratedRegex("^[0-9a-f]{1,32}$")]
    private static partial Regex HexId();
}

/// <summary>One Overview chart: one series, or one per <paramref name="SeriesLabel"/> value.</summary>
public sealed record MetricPanelQuery(string Key, string Title, string Unit, string Query, string? SeriesLabel = null);
