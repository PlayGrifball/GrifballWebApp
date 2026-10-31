using GrifballWebApp.Server.HaloInfinite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Telemetry;

/// <summary>
/// Healthy while Halo Infinite can be called the way the app calls it (<c>HaloInfinite:Mode</c>). Registered as
/// Degraded-on-failure, outside the readiness checks: a Halo outage must not take the website out of
/// service. It is never Unhealthy.
/// </summary>
/// <remarks>
/// <para>
/// The probe is <see cref="IHaloInfiniteClient.StatsGetMatchStats(string)"/> for one known match
/// (<c>HealthChecks:HaloInfinite:MatchId</c>, default the match the admin "Check status" button fetches): the
/// stats API the pulls call. The lobby <c>qosservers</c> endpoint was tried first and answers 403 to this
/// sign-in, so it cannot tell a working client from a broken one.
/// </para>
/// <para>
/// Direct mode: the client is the factory, which reuses its cached token, refreshes it when it is near
/// expiry (the full Xbox Live and Spartan sign-in), and retries once with a new token on a 401 or 403, so a
/// pass means the same path the stats pulls use works end to end.
/// </para>
/// <para>
/// Gateway mode: the client is the gateway client, and the gateway's own <c>/health</c> is read as well
/// (<see cref="IHaloGatewayAccountStatus"/>). Healthy needs both: the test call answering 2xx (the gateway
/// reachable, the API key accepted, Halo answering) and the gateway's Halo account Healthy. The gateway caches
/// match stats, so the test call alone could pass on a cached answer while the account needs signing in again.
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

    private readonly IHaloInfiniteClient _client;
    private readonly IHaloGatewayAccountStatus? _gateway;
    private readonly TimeProvider _time;
    private readonly ILogger<HaloInfiniteHealthCheck> _logger;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private HaloProbe? _last;

    /// <param name="client">What the app calls Halo with: the factory (Direct) or the gateway client (Gateway).</param>
    /// <param name="gateway">Gateway mode only: the gateway's own account status, which must be Healthy too.</param>
    public HaloInfiniteHealthCheck(IHaloInfiniteClient client, TimeSpan interval, TimeSpan timeout,
        TimeProvider time, ILogger<HaloInfiniteHealthCheck> logger, string matchId = DefaultMatchId,
        IHaloGatewayAccountStatus? gateway = null)
    {
        _client = client;
        _gateway = gateway;
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

    /// <summary>Whether the probe goes through the gateway (and reads its account status).</summary>
    public bool ViaGateway => _gateway is not null;

    /// <summary>
    /// From <c>HealthChecks:HaloInfinite:Interval</c> and <c>HealthChecks:HaloInfinite:Timeout</c>
    /// (TimeSpan strings, e.g. <c>00:05:00</c>; unset, unparseable or not positive: the default), and
    /// <c>HealthChecks:HaloInfinite:MatchId</c> (blank: <see cref="DefaultMatchId"/>). The client is the registered
    /// <see cref="IHaloInfiniteClient"/>, with <see cref="IHaloGatewayAccountStatus"/> when registered (Gateway mode).
    /// </summary>
    public static HaloInfiniteHealthCheck FromConfiguration(IServiceProvider services, IConfiguration configuration) => new(
        services.GetRequiredService<IHaloInfiniteClient>(),
        ReadPositive(configuration, "HealthChecks:HaloInfinite:Interval", DefaultInterval),
        ReadPositive(configuration, "HealthChecks:HaloInfinite:Timeout", DefaultTimeout),
        services.GetService<TimeProvider>() ?? TimeProvider.System,
        services.GetRequiredService<ILogger<HaloInfiniteHealthCheck>>(),
        configuration["HealthChecks:HaloInfinite:MatchId"] is { } matchId && !string.IsNullOrWhiteSpace(matchId)
            ? matchId.Trim()
            : DefaultMatchId,
        services.GetService<IHaloGatewayAccountStatus>());

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

    /// <summary>
    /// Probes now, whatever is cached (the Infinite Client page), and caches the result for the health check.
    /// Throws only <see cref="OperationCanceledException"/>, when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async Task<HaloProbe> ProbeNowAsync(CancellationToken cancellationToken = default)
    {
        await _probeLock.WaitAsync(cancellationToken);
        try
        {
            _last = await ProbeAsync(cancellationToken);
            return _last;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    private HaloProbe? Fresh()
    {
        var last = _last;
        return last is not null && _time.GetUtcNow() - last.At < Interval ? last : null;
    }

    private async Task<HaloProbe> ProbeAsync(CancellationToken cancellationToken)
    {
        var at = _time.GetUtcNow();
        // Both at once: the probe takes at most Timeout, not twice it.
        var gatewayTask = _gateway is null ? null : ProbeGatewayAsync(cancellationToken);
        var api = await ProbeApiAsync(cancellationToken);
        if (gatewayTask is null)
            return new HaloProbe(at, api.Passed, api.Description, api.Exception, api.Code, null);

        var account = await gatewayTask;
        var accountText = account switch
        {
            { Reachable: false } => $"Halo gateway unreachable ({account.Error})",
            { Status: { } status } => $"Halo gateway account {status}",
            _ => $"Halo gateway reachable ({account.Error})",
        };
        return new HaloProbe(at, api.Passed && account.Healthy, $"{accountText}; test call: {api.Description}", api.Exception, api.Code, account);
    }

    private async Task<(bool Passed, string Description, Exception? Exception, int? Code)> ProbeApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.StatsGetMatchStats(MatchId).WaitAsync(Timeout, _time, cancellationToken);
            var code = response?.Error?.Code ?? 0;
            return (code is >= 200 and < 300, $"Halo Infinite API answered {code}{Hint(code)}", null, code);
        }
        catch (TimeoutException)
        {
            return (false, $"Halo Infinite API did not answer within {Timeout.TotalSeconds:0.#} s", null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Direct: usually the sign-in (missing client settings, or a refresh token that no longer works).
            // Gateway: the gateway unreachable after the client's own retries.
            _logger.LogWarning(ex, "Halo Infinite health probe failed");
            return (false, "Halo Infinite client failed: " + ex.GetType().Name, ex, null);
        }
    }

    /// <summary>The gateway's own answers, which carry no Halo status: a bad API key, or the key's quota.</summary>
    private string Hint(int code) => _gateway is null ? "" : code switch
    {
        401 => " (the gateway rejected the API key, HaloGateway:ApiKey)",
        429 => " (the gateway's quota for this key)",
        _ => "",
    };

    private async Task<HaloGatewayAccount> ProbeGatewayAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _gateway!.GetAsync(cancellationToken).WaitAsync(Timeout, _time, cancellationToken);
        }
        catch (TimeoutException)
        {
            return HaloGatewayAccount.Unreachable($"no answer within {Timeout.TotalSeconds:0.#} s");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HaloGatewayAccount.Unreachable(ex.GetType().Name);
        }
    }

    private static TimeSpan ReadPositive(IConfiguration configuration, string key, TimeSpan fallback) =>
        TimeSpan.TryParse(configuration[key], System.Globalization.CultureInfo.InvariantCulture, out var value) && value > TimeSpan.Zero
            ? value
            : fallback;
}

/// <summary>
/// One probe of Halo Infinite. <see cref="ApiCode"/>: what the test call answered (null: no answer).
/// <see cref="Gateway"/>: Gateway mode only, the gateway's account status.
/// </summary>
public sealed record HaloProbe(DateTimeOffset At, bool Passed, string Description, Exception? Exception, int? ApiCode, HaloGatewayAccount? Gateway)
{
    public HealthCheckResult ToResult(HealthStatus failureStatus) => Passed
        ? HealthCheckResult.Healthy(Description)
        : new HealthCheckResult(failureStatus, Description, Exception);
}
