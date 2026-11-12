using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace GrifballWebApp.Server.Telemetry.Browser;

/// <summary>
/// The browser's telemetry (grifballwebapp.client's app/telemetry): its spans and log lines, passed on to the collector
/// as this service's (service.component=browser), and its Web Vitals, recorded as this app's metrics. Off unless
/// <c>ClientTelemetry:Enabled</c> (a 404 then, and the browser stops sending).
/// </summary>
public sealed class ClientTelemetryOptions
{
    public const string Section = "ClientTelemetry";

    public bool Enabled { get; set; }

    /// <summary>The collector's OTLP/HTTP base, e.g. <c>http://otel-collector:4318</c>: required when enabled.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Batches each signed-out client address may send per minute, on each API pod; the rest are a 429. Signed-in users have no limit.</summary>
    [Range(1, 1000)]
    public int PermitsPerMinute { get; set; } = 60;

    /// <summary>The OTLP endpoint as a URI; null when it isn't an http(s) URL.</summary>
    public Uri? OtlpUri => TelemetryOptions.ParseEndpoint(OtlpEndpoint);
}

/// <summary>One batch from a browser: what it collected since its last. Every list and text is bounded.</summary>
public sealed record ClientTelemetryBatchDto
{
    [MaxLength(ClientTelemetryLimits.Spans)]
    public List<ClientSpanDto> Spans { get; init; } = [];

    [MaxLength(ClientTelemetryLimits.Logs)]
    public List<ClientLogDto> Logs { get; init; } = [];

    [MaxLength(ClientTelemetryLimits.Vitals)]
    public List<ClientVitalDto> Vitals { get; init; } = [];
}

/// <summary>A finished span. Times are the browser's clock, in epoch milliseconds.</summary>
public sealed record ClientSpanDto
{
    [Required, RegularExpression("^[0-9a-f]{32}$")]
    public required string TraceId { get; init; }

    [Required, RegularExpression("^[0-9a-f]{16}$")]
    public required string SpanId { get; init; }

    [RegularExpression("^[0-9a-f]{16}$")]
    public string? ParentSpanId { get; init; }

    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary><c>internal</c> (a navigation) or <c>client</c> (a call to the API).</summary>
    [RegularExpression("^(internal|client)$")]
    public string Kind { get; init; } = "internal";

    [Range(ClientTelemetryLimits.MinEpochMs, ClientTelemetryLimits.MaxEpochMs)]
    public required double Start { get; init; }

    [Range(ClientTelemetryLimits.MinEpochMs, ClientTelemetryLimits.MaxEpochMs)]
    public required double End { get; init; }

    [RegularExpression("^(unset|ok|error)$")]
    public string Status { get; init; } = "unset";

    public Dictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>A log line, in the trace it names (if any).</summary>
public sealed record ClientLogDto
{
    [Range(ClientTelemetryLimits.MinEpochMs, ClientTelemetryLimits.MaxEpochMs)]
    public required double Time { get; init; }

    [Required, RegularExpression("^(debug|info|warn|error)$")]
    public required string Level { get; init; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public required string Message { get; init; }

    [RegularExpression("^[0-9a-f]{32}$")]
    public string? TraceId { get; init; }

    [RegularExpression("^[0-9a-f]{16}$")]
    public string? SpanId { get; init; }

    public Dictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>One Web Vital: web-vitals' value, milliseconds (CLS unitless).</summary>
public sealed record ClientVitalDto
{
    [Required, RegularExpression("^(LCP|INP|CLS|FCP|TTFB)$")]
    public required string Name { get; init; }

    [Range(0, 600_000)]
    public required double Value { get; init; }
}

public static class ClientTelemetryLimits
{
    public const int Spans = 100;
    public const int Logs = 100;
    public const int Vitals = 20;
    public const int BodyBytes = 256 * 1024;
    /// <summary>2001 and 2286 in epoch milliseconds: anything else is no browser's clock.</summary>
    public const double MinEpochMs = 1e12;
    public const double MaxEpochMs = 1e13;
}

[ApiController]
[AllowAnonymous]
public class ClientTelemetryController : ControllerBase
{
    public const string RateLimitPolicy = "client-telemetry";
    public const string Route = "client-telemetry";

    private readonly ClientTelemetryOptions _options;
    private readonly ClientTelemetryQueue _queue;
    private readonly BrowserMetrics _metrics;

    public ClientTelemetryController(IOptions<ClientTelemetryOptions> options, ClientTelemetryQueue queue, BrowserMetrics metrics)
    {
        _options = options.Value;
        _queue = queue;
        _metrics = metrics;
    }

    /// <summary>
    /// Takes a batch of the browser's telemetry: its Web Vitals become this app's metrics, its spans and log lines are
    /// queued for the collector (ClientTelemetryPump), so the browser never waits on it. A 404 when browser telemetry is off.
    /// </summary>
    [HttpPost(Route, Name = "ReportClientTelemetry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [EnableRateLimiting(RateLimitPolicy)]
    [RequestSizeLimit(ClientTelemetryLimits.BodyBytes)]
#pragma warning disable S4502 // anonymous and only recorded; the Fetch Metadata and Origin checks still refuse it from another site
    [IgnoreAntiforgeryToken]
#pragma warning restore S4502
    public IActionResult Report([FromBody] ClientTelemetryBatchDto batch, [FromHeader(Name = "User-Agent")] string? userAgent)
    {
        if (!_options.Enabled)
            return this.NotFoundProblem("Browser telemetry is off here");

        foreach (var vital in batch.Vitals)
            _metrics.Record(vital);
        _queue.Enqueue(batch, userAgent ?? "");
        return NoContent();
    }
}

public static class ClientTelemetryExtensions
{
    /// <summary>
    /// <see cref="ClientTelemetryOptions"/> (checked at startup: enabled takes an OTLP endpoint), the forwarder, the Web
    /// Vitals' meter, and the rate limit: signed out, a fixed window per client address.
    /// </summary>
    public static IServiceCollection AddClientTelemetry(this IServiceCollection services, IConfiguration configuration, TelemetryOptions telemetry)
    {
        services.AddOptions<ClientTelemetryOptions>().Bind(configuration.GetSection(ClientTelemetryOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => !o.Enabled || o.OtlpUri is not null, "ClientTelemetry:OtlpEndpoint must be an http(s) URL when ClientTelemetry:Enabled")
            .ValidateOnStart();
        services.AddSingleton(new ClientTelemetryResource(telemetry));
        services.AddSingleton<BrowserMetrics>();
        services.AddSingleton<ForwardFailures>();
        services.AddSingleton<ClientTelemetryQueue>();
        services.AddHttpClient<ClientTelemetryForwarder>(client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHostedService<ClientTelemetryPump>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // The validated options, read per request (a singleton: no cost)
            limiter.AddPolicy(ClientTelemetryController.RateLimitPolicy, context => PartitionByClient(context,
                context.RequestServices.GetRequiredService<IOptions<ClientTelemetryOptions>>().Value.PermitsPerMinute));
        });
        return services;
    }

    /// <summary>
    /// Signed in: no limit. Signed out: a window of <paramref name="permitsPerMinute"/> batches a minute for the
    /// request's client address. Needs authentication to have run (UseRateLimiter after UseAuthentication).
    /// </summary>
    public static RateLimitPartition<string> PartitionByClient(HttpContext context, int permitsPerMinute) =>
        context.User.Identity?.IsAuthenticated == true
            ? RateLimitPartition.GetNoLimiter("signed-in")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, permitsPerMinute), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });

    /// <summary>The browser's batches: every few seconds a visitor, so left out of the traces and the request log.</summary>
    public static bool IsClientTelemetryPath(PathString path) =>
        path.StartsWithSegments("/" + ClientTelemetryController.Route, StringComparison.OrdinalIgnoreCase);
}
