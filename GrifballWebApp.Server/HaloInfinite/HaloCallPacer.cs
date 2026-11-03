using System.Threading.RateLimiting;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// Spaces the Halo calls of a pull (<see cref="Services.DataPullService"/>) to the gateway's quota for this app's key,
/// <c>HaloGateway:PermitsPerMinute</c>: evenly, a little under it, so the gateway's limiter doesn't turn them away. A call
/// it turned away is retried after the gateway's <c>Retry-After</c> (one second), and turned away again while the pull
/// keeps asking faster than the quota: a few of those in a row and the call was given up (GrifballWebApp#237). Unset (or
/// 0, or Direct mode): no pacing.
/// </summary>
public sealed class HaloCallPacer : IAsyncDisposable
{
    public const string PermitsPerMinuteKey = HaloInfiniteSettings.GatewaySection + ":PermitsPerMinute";

    /// <summary>The share of the quota used: the rest is room for the app's other calls on the same key.</summary>
    public const double QuotaShare = 0.9;

    private readonly RateLimiter? _limiter;

    public HaloCallPacer(IConfiguration configuration, HaloInfiniteSettings settings)
        : this(settings.IsGateway ? configuration.GetValue<int?>(PermitsPerMinuteKey) ?? 0 : 0)
    {
    }

    public HaloCallPacer(int permitsPerMinute)
    {
        if (permitsPerMinute <= 0)
            return;
        PerCall = TimeSpan.FromMinutes(1) / (permitsPerMinute * QuotaShare);
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = PerCall,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    /// <summary>The time between two calls; zero without pacing.</summary>
    public TimeSpan PerCall { get; }

    /// <summary>Waits for this call's turn (at once without pacing).</summary>
    public async ValueTask WaitAsync(CancellationToken ct = default)
    {
        if (_limiter is null)
            return;
        using var lease = await _limiter.AcquireAsync(1, ct);
    }

    public ValueTask DisposeAsync() => _limiter?.DisposeAsync() ?? ValueTask.CompletedTask;
}
