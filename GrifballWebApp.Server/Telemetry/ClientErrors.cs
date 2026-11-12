using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Threading.RateLimiting;

namespace GrifballWebApp.Server.Telemetry;

/// <summary>
/// The browser's error reports (grifballwebapp.client's clientErrors.ts): unhandled errors and failed API calls,
/// logged here so they reach Loki with the rest. Off unless <c>ClientErrors:Enabled</c> (a 404 then, and the
/// browser stops sending).
/// </summary>
public sealed class ClientErrorsOptions
{
    public const string Section = "ClientErrors";

    public bool Enabled { get; set; }

    /// <summary>Reports each signed-out client address may send per minute, on each API pod; the rest are a 429. Signed-in users have no limit.</summary>
    [Range(1, 1000)]
    public int PermitsPerMinute { get; set; } = 20;
}

/// <summary>One report. Every text is bounded; nothing names the user.</summary>
public sealed record ClientErrorDto
{
    /// <summary><c>error</c> (a script error) or <c>http</c> (a failed API call).</summary>
    [Required, RegularExpression("^(error|http)$")]
    public required string Kind { get; init; }

    [Required, StringLength(1000)]
    public required string Message { get; init; }

    [StringLength(4000)]
    public string? Stack { get; init; }

    /// <summary>The page's path (no query string: it can carry tokens).</summary>
    [Required, StringLength(300)]
    public required string Page { get; init; }

    /// <summary>The trace the error belongs to (the page's, or the failed call's), as <c>traceparent</c>.</summary>
    [StringLength(55)]
    public string? Traceparent { get; init; }

    /// <summary>A failed call's method, path and status (0: no answer).</summary>
    [StringLength(10)]
    public string? Method { get; init; }

    [StringLength(300)]
    public string? Url { get; init; }

    [Range(0, 599)]
    public int? Status { get; init; }
}

[ApiController]
[AllowAnonymous]
public partial class ClientErrorsController : ControllerBase
{
    public const string RateLimitPolicy = "client-errors";
    public const string ActivitySourceName = "GrifballWebApp.ClientErrors";
    private static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly ClientErrorsOptions _options;
    private readonly ILogger<ClientErrorsController> _logger;

    public ClientErrorsController(Microsoft.Extensions.Options.IOptions<ClientErrorsOptions> options, ILogger<ClientErrorsController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Logs a report. With a traceparent, it is logged in a span of that trace (so its log line carries the trace id,
    /// and Tempo shows it in the trace); without one, in this request's.
    /// </summary>
    [HttpPost("client-errors", Name = "ReportClientError")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [EnableRateLimiting(RateLimitPolicy)]
    [IgnoreAntiforgeryToken] // NOSONAR S4502: anonymous and only logged; the Fetch Metadata and Origin checks still refuse it from another site
    public IActionResult Report([FromBody] ClientErrorDto report)
    {
        if (!_options.Enabled)
            return this.NotFoundProblem("Browser error reports are off here");

        using var activity = ActivityContext.TryParse(report.Traceparent, null, out var parent)
            ? Source.StartActivity("browser " + report.Kind, ActivityKind.Internal, parent)
            : null;
        activity?.SetTag("url.path", report.Page);
        activity?.SetStatus(ActivityStatusCode.Error, report.Message);

        if (report.Kind == "http")
            LogFailedCall(_logger, report.Method, report.Url, report.Status, report.Page, report.Message);
        else
        {
            // The stack as a property, not in the message
            using (_logger.BeginScope(new Dictionary<string, object?> { ["Stack"] = report.Stack }))
                LogScriptError(_logger, report.Page, report.Message);
        }
        return NoContent();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Browser error on {Page}: {ErrorMessage}")]
    private static partial void LogScriptError(ILogger logger, string page, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Browser call {Method} {Url} failed ({Status}) on {Page}: {ErrorMessage}")]
    private static partial void LogFailedCall(ILogger logger, string? method, string? url, int? status, string page, string errorMessage);
}

public static class ClientErrorsExtensions
{
    /// <summary><see cref="ClientErrorsOptions"/> and its rate limit: signed out, a fixed window per client address.</summary>
    public static IServiceCollection AddClientErrors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClientErrorsOptions>().Bind(configuration.GetSection(ClientErrorsOptions.Section)).ValidateDataAnnotations();
        var permits = configuration.GetSection(ClientErrorsOptions.Section).Get<ClientErrorsOptions>()?.PermitsPerMinute ?? new ClientErrorsOptions().PermitsPerMinute;
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(ClientErrorsController.RateLimitPolicy, context => PartitionByClient(context, permits));
        });
        return services;
    }

    /// <summary>
    /// Signed in: no limit. Signed out: a window of <paramref name="permitsPerMinute"/> reports a minute for the
    /// request's client address. Needs authentication to have run (UseRateLimiter after UseAuthentication).
    /// </summary>
    public static RateLimitPartition<string> PartitionByClient(HttpContext context, int permitsPerMinute) =>
        context.User.Identity?.IsAuthenticated == true
            ? RateLimitPartition.GetNoLimiter("signed-in")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, permitsPerMinute), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
