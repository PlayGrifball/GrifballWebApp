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
            // The app's server (grifballwebapp.client/src/server/telemetry.ts), which shares this service.name
            new("render", "Page render on the server (p95)", "ms", $"histogram_quantile(0.95, sum by (le) (rate(grif_frontend_render_duration_seconds_bucket{{{s},outcome=\"server\"}}[{w}]))) * 1000"),
            new("renderFailures", "Pages left to the browser after failing", "/min", $"sum(rate(grif_frontend_render_duration_seconds_count{{{s},outcome=\"failed\"}}[{w}])) * 60"),
            new("frontendMemory", "Frontend memory (resident)", "MB", $"sum by (k8s_pod_name) (grif_frontend_memory_rss_bytes{{{s}}}) / 1048576", "k8s_pod_name"),
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

    /// <summary>The Traces tab's search: the service's traces, failed ones only, slower than a minimum. Selects each match's route and status code.</summary>
    public static string Traces(string serviceName, bool errorsOnly, int? minDurationMs)
    {
        var query = new StringBuilder("{resource.service.name=").Append(Quote(serviceName));
        if (errorsOnly)
            query.Append(" && status=error");
        if (minDurationMs is > 0)
            query.Append(" && duration>=").Append(minDurationMs.Value.ToString(CultureInfo.InvariantCulture)).Append("ms");
        return query.Append("} | select(span.http.route, span.http.response.status_code)").ToString();
    }

    [GeneratedRegex(@"[\\.+*?()|\[\]{}^$]")]
    private static partial Regex Re2Meta();

    [GeneratedRegex("^[0-9a-f]{1,32}$")]
    private static partial Regex HexId();
}

/// <summary>One Overview chart: one series, or one per <paramref name="SeriesLabel"/> value.</summary>
public sealed record MetricPanelQuery(string Key, string Title, string Unit, string Query, string? SeriesLabel = null);
