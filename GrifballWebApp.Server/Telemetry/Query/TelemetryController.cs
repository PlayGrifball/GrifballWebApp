using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Telemetry.Query;

/// <summary>
/// The Telemetry page (sysadmin only): this app's own logs, metrics and traces, read back from Loki, Prometheus and
/// Tempo (<see cref="TelemetryQueryService"/>). An unconfigured backend is a 404, a failing one a 502.
/// </summary>
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class TelemetryController : ControllerBase
{
    public const int MaxSearchLength = 200;
    private const string BadTraceId = "A trace id is up to 32 hex digits";

    /// <summary>What a 502 says: the backend's own answer (its host, its error body) stays in the log.</summary>
    public const string BackendFailedDetail = "The telemetry backend didn't answer as expected. The details are in the server's log.";

    private readonly TelemetryQueryService _telemetry;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(TelemetryQueryService telemetry, ILogger<TelemetryController> logger)
    {
        _telemetry = telemetry;
        _logger = logger;
    }

    /// <summary>What the caller is told: a 4xx's reason (an unconfigured backend), never a 5xx's.</summary>
    public static string DetailFor(TelemetryQueryException e) => e.Status < StatusCodes.Status500InternalServerError ? e.Message : BackendFailedDetail;

    private ObjectResult Refused(TelemetryQueryException e)
    {
        if (e.Status >= StatusCodes.Status500InternalServerError)
            _logger.LogWarning(e, "Telemetry query failed ({Status}): {Message}", e.Status, e.Message);
        return this.Refusal(e.Status, DetailFor(e));
    }

    /// <summary>Which telemetry backends (Loki, Prometheus, Tempo) are configured.</summary>
    [HttpGet("telemetry/status", Name = "GetTelemetryStatus")]
    [ProducesResponseType<TelemetryStatusDto>(StatusCodes.Status200OK)]
    public TelemetryStatusDto Status() => _telemetry.Status();

    /// <summary>The Overview: charts over the last <paramref name="minutes"/> and the busiest routes.</summary>
    [HttpGet("telemetry/metrics", Name = "GetTelemetryMetrics")]
    [ProducesResponseType<MetricsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<MetricsDto>> Metrics([FromQuery] int minutes = 60, CancellationToken ct = default)
    {
        try
        {
            return await _telemetry.Metrics(minutes, ct);
        }
        catch (TelemetryQueryException e)
        {
            return Refused(e);
        }
    }

    /// <summary>Log lines, newest first, from the <paramref name="minutes"/> before <paramref name="end"/> (default now).</summary>
    [HttpGet("telemetry/logs", Name = "GetTelemetryLogs")]
    [ProducesResponseType<LogsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<LogsDto>> Logs([FromQuery] int minutes = 60, [FromQuery] DateTimeOffset? end = null,
        [FromQuery] string[]? level = null, [FromQuery] string? search = null, [FromQuery] string? traceId = null,
        [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        if (level?.FirstOrDefault(l => !TelemetryQueries.Levels.Contains(l)) is { } unknown)
            return this.BadRequestProblem($"Unknown level {unknown}: one of {string.Join(", ", TelemetryQueries.Levels)}");
        if (!string.IsNullOrWhiteSpace(traceId) && TelemetryQueries.NormalizeTraceId(traceId) is null)
            return this.BadRequestProblem(BadTraceId);
        if (search?.Length > MaxSearchLength)
            return this.BadRequestProblem($"The search is {MaxSearchLength} characters at most");

        try
        {
            return await _telemetry.Logs(minutes, end, level, search, traceId, limit, ct);
        }
        catch (TelemetryQueryException e)
        {
            return Refused(e);
        }
    }

    /// <summary>Traces from the last <paramref name="minutes"/>, newest first: failed ones only, slower than a minimum.</summary>
    [HttpGet("telemetry/traces", Name = "GetTelemetryTraces")]
    [ProducesResponseType<List<TraceSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<List<TraceSummaryDto>>> Traces([FromQuery] int minutes = 60, [FromQuery] bool errorsOnly = false,
        [FromQuery] int? minDurationMs = null, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        try
        {
            return await _telemetry.Traces(minutes, errorsOnly, minDurationMs, limit, ct);
        }
        catch (TelemetryQueryException e)
        {
            return Refused(e);
        }
    }

    /// <summary>One trace's spans from this app. 404: not found, or none of its spans are this app's.</summary>
    [HttpGet("telemetry/traces/{traceID}", Name = "GetTelemetryTrace")]
    [ProducesResponseType<TraceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<TraceDto>> Trace([FromRoute] string traceID, CancellationToken ct = default)
    {
        if (TelemetryQueries.NormalizeTraceId(traceID) is null)
            return this.BadRequestProblem(BadTraceId);

        try
        {
            if (await _telemetry.Trace(traceID, ct) is { } trace)
                return trace;
            return this.NotFoundProblem("No such trace from this app");
        }
        catch (TelemetryQueryException e)
        {
            return Refused(e);
        }
    }
}
