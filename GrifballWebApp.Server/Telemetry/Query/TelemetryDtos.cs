namespace GrifballWebApp.Server.Telemetry.Query;

/// <summary>Which parts of the Telemetry page have a backend (TelemetryQuery:*), and the service.name they are scoped to.</summary>
public sealed record TelemetryStatusDto(string ServiceName, bool Logs, bool Metrics, bool Traces);

public sealed record LogEntryDto(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? TraceId,
    string? SpanId,
    string? Source,
    string? Pod,
    IReadOnlyDictionary<string, string> Attributes);

/// <summary>Newest first. <paramref name="Truncated"/>: the limit was reached, older lines were left out.</summary>
public sealed record LogsDto(IReadOnlyList<LogEntryDto> Entries, bool Truncated);

/// <summary>A point: Unix milliseconds and the value.</summary>
public sealed record MetricSeriesDto(string Name, IReadOnlyList<double[]> Points);

public sealed record MetricPanelDto(string Key, string Title, string Unit, IReadOnlyList<MetricSeriesDto> Series);

public sealed record RouteStatsDto(string Route, double Requests, double Errors, double? P95Ms);

public sealed record MetricsDto(DateTimeOffset Start, DateTimeOffset End, int StepSeconds, IReadOnlyList<MetricPanelDto> Panels, IReadOnlyList<RouteStatsDto> Routes);

public sealed record TraceSummaryDto(string TraceId, string RootName, string? RootService, DateTimeOffset Start, double DurationMs, string? Route, int? StatusCode);

public sealed record SpanEventDto(string Name, double OffsetMs, IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// A span: <paramref name="OffsetMs"/> from the trace's first span. <paramref name="Status"/> is Unset, Ok or Error.
/// <paramref name="Service"/> is its service.name: this app's, or one in front of it or that it calls (TelemetryQuery:EdgeServices,
/// DownstreamServices).
/// </summary>
public sealed record SpanDto(
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Kind,
    string Scope,
    string Service,
    string? Pod,
    double OffsetMs,
    double DurationMs,
    string Status,
    string? StatusMessage,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<SpanEventDto> Events);

/// <summary>This service's spans of a trace and those of the services in front of it and that it calls, in start order.</summary>
public sealed record TraceDto(string TraceId, DateTimeOffset Start, double DurationMs, IReadOnlyList<SpanDto> Spans);
