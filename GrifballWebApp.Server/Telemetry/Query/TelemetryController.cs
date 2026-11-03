using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Telemetry.Query;

/// <summary>
/// The Telemetry page (sysadmin only): this app's own logs, metrics and traces, read back from Loki, Prometheus and
/// Tempo (<see cref="TelemetryQueryService"/>). An unconfigured backend is a 404, a failing one a 502.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class TelemetryController : ControllerBase
{
    public const int MaxSearchLength = 200;
    private const string BadTraceId = "A trace id is up to 32 hex digits";

    private readonly TelemetryQueryService _telemetry;

    public TelemetryController(TelemetryQueryService telemetry)
    {
        _telemetry = telemetry;
    }

    [HttpGet(Name = "GetTelemetryStatus")]
    public TelemetryStatusDto Status() => _telemetry.Status();

    /// <summary>The Overview: charts over the last <paramref name="minutes"/> and the busiest routes.</summary>
    [HttpGet(Name = "GetTelemetryMetrics")]
    public async Task<ActionResult<MetricsDto>> Metrics([FromQuery] int minutes = 60, CancellationToken ct = default)
    {
        try
        {
            return await _telemetry.Metrics(minutes, ct);
        }
        catch (TelemetryQueryException e)
        {
            return StatusCode(e.Status, e.Message);
        }
    }

    /// <summary>Log lines, newest first, from the <paramref name="minutes"/> before <paramref name="end"/> (default now).</summary>
    [HttpGet(Name = "GetTelemetryLogs")]
    public async Task<ActionResult<LogsDto>> Logs([FromQuery] int minutes = 60, [FromQuery] DateTimeOffset? end = null,
        [FromQuery] string[]? level = null, [FromQuery] string? search = null, [FromQuery] string? traceId = null,
        [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        if (level?.FirstOrDefault(l => !TelemetryQueries.Levels.Contains(l)) is { } unknown)
            return BadRequest($"Unknown level {unknown}: one of {string.Join(", ", TelemetryQueries.Levels)}");
        if (!string.IsNullOrWhiteSpace(traceId) && TelemetryQueries.NormalizeTraceId(traceId) is null)
            return BadRequest(BadTraceId);
        if (search?.Length > MaxSearchLength)
            return BadRequest($"The search is {MaxSearchLength} characters at most");

        try
        {
            return await _telemetry.Logs(minutes, end, level, search, traceId, limit, ct);
        }
        catch (TelemetryQueryException e)
        {
            return StatusCode(e.Status, e.Message);
        }
    }

    /// <summary>Traces from the last <paramref name="minutes"/>, newest first: failed ones only, slower than a minimum.</summary>
    [HttpGet(Name = "GetTelemetryTraces")]
    public async Task<ActionResult<List<TraceSummaryDto>>> Traces([FromQuery] int minutes = 60, [FromQuery] bool errorsOnly = false,
        [FromQuery] int? minDurationMs = null, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        try
        {
            return await _telemetry.Traces(minutes, errorsOnly, minDurationMs, limit, ct);
        }
        catch (TelemetryQueryException e)
        {
            return StatusCode(e.Status, e.Message);
        }
    }

    /// <summary>One trace's spans from this app. 404: not found, or none of its spans are this app's.</summary>
    [HttpGet(Name = "GetTelemetryTrace")]
    public async Task<ActionResult<TraceDto>> Trace([FromQuery] string id, CancellationToken ct = default)
    {
        if (TelemetryQueries.NormalizeTraceId(id) is null)
            return BadRequest(BadTraceId);

        try
        {
            if (await _telemetry.Trace(id, ct) is { } trace)
                return trace;
            return NotFound("No such trace from this app");
        }
        catch (TelemetryQueryException e)
        {
            return StatusCode(e.Status, e.Message);
        }
    }
}
