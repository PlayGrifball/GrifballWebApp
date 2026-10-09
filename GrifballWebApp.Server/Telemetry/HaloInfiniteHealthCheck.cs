using Microsoft.Extensions.Diagnostics.HealthChecks;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Telemetry;

/// <summary>
/// Healthy while the Halo Infinite client can authenticate and call the API. Registered as
/// Degraded-on-failure, outside the readiness checks: a Halo outage must not take the website out of
/// service.
/// </summary>
/// <remarks>
/// <para>
/// The probe is <see cref="IHaloInfiniteClient.StatsGetMatchStats(string)"/> through the factory, for one
/// known match (<c>HealthChecks:HaloInfinite:MatchId</c>, default the match the admin "Check status" button
/// fetches): the stats API the pulls call, with the Spartan token. The lobby <c>qosservers</c> endpoint was
/// tried first and answers 403 to this sign-in, so it cannot tell a working client from a broken one.
/// The factory reuses its cached token, refreshes
/// it when it is near expiry (the full Xbox Live and Spartan sign-in), and retries once with a new token
/// on a 401 or 403, so a pass means the same path the stats pulls use works end to end.
/// </para>
/// <para>
/// Every check runs every 30 s (the telemetry publisher), and <c>/health</c> is public, so the result is
/// cached: Halo is called at most once per <c>HealthChecks:HaloInfinite:Interval</c> (default 5 min),
/// however often the check runs. A probe gives up after <c>HealthChecks:HaloInfinite:Timeout</c>
/// (default 10 s); the client's retry policy can otherwise hold a call for minutes. Failures, timeouts
/// included, are cached like successes. Nothing is thrown out of the check.
/// </para>
/// </remarks>
public sealed class HaloInfiniteHealthCheck : IHealthCheck
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    /// <summary>The match the admin "Check status" button fetches; known to exist.</summary>
    public const string DefaultMatchId = "1fde4c2a-7935-4fb0-9706-e226f4d13683";

    private readonly IHaloInfiniteClientFactory _factory;
    private readonly TimeProvider _time;
    private readonly ILogger<HaloInfiniteHealthCheck> _logger;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private Probe? _last;

    public HaloInfiniteHealthCheck(IHaloInfiniteClientFactory factory, TimeSpan interval, TimeSpan timeout,
        TimeProvider time, ILogger<HaloInfiniteHealthCheck> logger, string matchId = DefaultMatchId)
    {
        _factory = factory;
        MatchId = matchId;
        Interval = interval;
        Timeout = timeout;
        _time = time;
        _logger = logger;
    }

    public TimeSpan Interval { get; }

    public TimeSpan Timeout { get; }

    /// <summary>The match whose stats the probe fetches.</summary>
    public string MatchId { get; }

    /// <summary>
    /// From <c>HealthChecks:HaloInfinite:Interval</c> and <c>HealthChecks:HaloInfinite:Timeout</c>
    /// (TimeSpan strings, e.g. <c>00:05:00</c>; unset, unparseable or not positive: the default), and
    /// <c>HealthChecks:HaloInfinite:MatchId</c> (blank: <see cref="DefaultMatchId"/>).
    /// </summary>
    public static HaloInfiniteHealthCheck FromConfiguration(IServiceProvider services, IConfiguration configuration) => new(
        services.GetRequiredService<IHaloInfiniteClientFactory>(),
        ReadPositive(configuration, "HealthChecks:HaloInfinite:Interval", DefaultInterval),
        ReadPositive(configuration, "HealthChecks:HaloInfinite:Timeout", DefaultTimeout),
        services.GetService<TimeProvider>() ?? TimeProvider.System,
        services.GetRequiredService<ILogger<HaloInfiniteHealthCheck>>(),
        configuration["HealthChecks:HaloInfinite:MatchId"] is { } matchId && !string.IsNullOrWhiteSpace(matchId)
            ? matchId.Trim()
            : DefaultMatchId);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (Fresh() is { } cached)
                return cached.ToResult(context.Registration.FailureStatus);

            await _probeLock.WaitAsync(cancellationToken);
            try
            {
                // Another caller may have probed while this one waited.
                if (Fresh() is { } probed)
                    return probed.ToResult(context.Registration.FailureStatus);

                _last = await ProbeAsync(cancellationToken);
                return _last.ToResult(context.Registration.FailureStatus);
            }
            finally
            {
                _probeLock.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up (e.g. the registration's timeout); nothing is cached.
            return new HealthCheckResult(context.Registration.FailureStatus, "Halo Infinite check was cancelled");
        }
    }

    private Probe? Fresh()
    {
        var last = _last;
        return last is not null && _time.GetUtcNow() - last.At < Interval ? last : null;
    }

    private async Task<Probe> ProbeAsync(CancellationToken cancellationToken)
    {
        var at = _time.GetUtcNow();
        try
        {
            var response = await _factory.StatsGetMatchStats(MatchId).WaitAsync(Timeout, _time, cancellationToken);
            var code = response?.Error?.Code ?? 0;
            return new Probe(at, code is >= 200 and < 300, $"Halo Infinite API answered {code}", null);
        }
        catch (TimeoutException)
        {
            return new Probe(at, false, $"Halo Infinite API did not answer within {Timeout.TotalSeconds:0.#} s", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Usually the sign-in: missing client settings, or a refresh token that no longer works.
            _logger.LogWarning(ex, "Halo Infinite health probe failed");
            return new Probe(at, false, "Halo Infinite client failed: " + ex.GetType().Name, ex);
        }
    }

    private static TimeSpan ReadPositive(IConfiguration configuration, string key, TimeSpan fallback) =>
        TimeSpan.TryParse(configuration[key], System.Globalization.CultureInfo.InvariantCulture, out var value) && value > TimeSpan.Zero
            ? value
            : fallback;

    private sealed record Probe(DateTimeOffset At, bool Passed, string Description, Exception? Exception)
    {
        public HealthCheckResult ToResult(HealthStatus failureStatus) => Passed
            ? HealthCheckResult.Healthy(Description)
            : new HealthCheckResult(failureStatus, Description, Exception);
    }
}
