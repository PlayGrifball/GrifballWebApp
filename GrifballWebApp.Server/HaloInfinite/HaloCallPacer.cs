using System.Threading.RateLimiting;
using GrifballWebApp.Server.Hosting;
using StackExchange.Redis;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// Spaces the Halo calls of a pull (<see cref="Services.DataPullService"/>) and of the XUID lookups (<see cref="Historical.XuidLookupService"/>) to the gateway's quota for this app's key,
/// <c>HaloGateway:PermitsPerMinute</c>: evenly, a little under it, so the gateway's limiter doesn't turn them away. A call
/// it turned away is retried after the gateway's <c>Retry-After</c> (one second), and turned away again while the pull
/// keeps asking faster than the quota: a few of those in a row and the call was given up (GrifballWebApp#237). Unset (or
/// 0, or Direct mode): no pacing.
/// <para>
/// The quota is the key's, not a pod's: scaled out (several workers, the API pods, grif-discord), the turns are shared
/// through Redis (<see cref="IHaloCallSlots"/>, <c>ConnectionStrings:Redis</c>), so every pod's calls together keep to
/// it. Without Redis (one pod), the process paces its own calls at the full rate. A pod that has Redis but can't reach
/// it paces its own at <see cref="UnsharedShare"/> of that: the other pods may still be taking every shared turn, and
/// its calls come on top of theirs (a dropped connection, a NetworkPolicy, a failover).
/// </para>
/// </summary>
public sealed class HaloCallPacer : IAsyncDisposable
{
    public const string PermitsPerMinuteKey = HaloInfiniteSettings.GatewaySection + ":PermitsPerMinute";

    /// <summary>The share of the quota used: the rest is room for the app's other calls on the same key.</summary>
    public const double QuotaShare = 0.9;

    /// <summary>
    /// The share of the rate a pod paces at while it can't reach the shared turns: with the others at the full rate
    /// (<see cref="QuotaShare"/>), 0.9 + 0.09 of the key's quota stays under it. Slow, but only until Redis is back.
    /// </summary>
    public const double UnsharedShare = 0.1;

    private readonly RateLimiter? _limiter;
    private readonly IHaloCallSlots? _shared;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _stopping = new();
    private long _sharedUnavailableUntil;

    public HaloCallPacer(IConfiguration configuration, HaloInfiniteSettings settings, IHaloCallSlots? shared = null, ILogger<HaloCallPacer>? logger = null)
        : this(settings.IsGateway ? configuration.GetValue<int?>(PermitsPerMinuteKey) ?? 0 : 0, shared, logger)
    {
    }

    public HaloCallPacer(int permitsPerMinute, IHaloCallSlots? shared = null, ILogger? logger = null)
    {
        if (permitsPerMinute <= 0)
            return;
        _shared = shared;
        _logger = logger;
        PerCall = TimeSpan.FromMinutes(1) / (permitsPerMinute * QuotaShare);
        // Shared: this pod's own pace is only for while Redis can't be reached
        UnsharedPerCall = shared is null ? PerCall : PerCall / UnsharedShare;
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = UnsharedPerCall,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    /// <summary>The time between two calls; zero without pacing.</summary>
    public TimeSpan PerCall { get; }

    /// <summary>The time between two calls paced by this pod alone: <see cref="PerCall"/>, or, while the shared turns can't be reached, longer (<see cref="UnsharedShare"/>).</summary>
    public TimeSpan UnsharedPerCall { get; }

    /// <summary>Whether the turns are shared with the other pods (through Redis).</summary>
    public bool Shared => _shared is not null;

    /// <summary>How long the shared turns are left alone after they couldn't be reached (this process paces its own calls meanwhile).</summary>
    public TimeSpan SharedRetryAfter { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Waits for this call's turn (at once without pacing).</summary>
    public async ValueTask WaitAsync(CancellationToken ct = default)
    {
        if (_limiter is null)
            return;
        // The app is stopping (the pacer went with it): so is the pull, rather than calling Halo unpaced.
        if (_stopping.IsCancellationRequested)
            throw new OperationCanceledException("The app is stopping", ct);
        if (_shared is not null && Environment.TickCount64 >= Interlocked.Read(ref _sharedUnavailableUntil) && await SharedTurnAsync(_shared, ct))
            return;

        // A call that starts waiting after the pacer is disposed throws; one already waiting is handed a refused turn.
        RateLimitLease lease;
        try
        {
            lease = await _limiter.AcquireAsync(1, ct);
        }
        catch (ObjectDisposedException)
        {
            throw new OperationCanceledException("The app is stopping", ct);
        }
        using (lease)
        {
            if (!lease.IsAcquired)
                throw new OperationCanceledException("The app is stopping", ct);
        }
    }

    /// <summary>Waits for the turn every pod's calls share; false when the shared turns can't be reached (paced in process then).</summary>
    private async Task<bool> SharedTurnAsync(IHaloCallSlots shared, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        TimeSpan wait;
        try
        {
            wait = await shared.ReserveAsync(PerCall, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            throw new OperationCanceledException("The app is stopping", ct);
        }
        catch (Exception e)
        {
            Interlocked.Exchange(ref _sharedUnavailableUntil, Environment.TickCount64 + (long)SharedRetryAfter.TotalMilliseconds);
            _logger?.LogWarning(e, "Couldn't reach the Halo calls' shared pace (Redis): pacing this pod's calls alone, one every {PerCall} ms, for {Seconds} s", UnsharedPerCall.TotalMilliseconds, SharedRetryAfter.TotalSeconds);
            return false;
        }
        if (wait > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(wait, linked.Token);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException("The app is stopping", ct);
            }
        }
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        return _limiter?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

/// <summary>The Halo calls' turns, shared by every pod calling with the same gateway key.</summary>
public interface IHaloCallSlots
{
    /// <summary>
    /// Takes the next turn, <paramref name="perCall"/> after the last one any pod took (now, if that one is past), and
    /// returns how long until it.
    /// </summary>
    Task<TimeSpan> ReserveAsync(TimeSpan perCall, CancellationToken ct);
}

/// <summary>
/// The turns in Redis (or Valkey): one key holding the next free turn, taken and moved on by one script, so the pods
/// queue one after another whatever their number. Time is Redis's own (<c>TIME</c>), so the pods' clocks don't matter.
/// On the process's one Redis connection (<see cref="AppRedis"/>), the SignalR backplane's.
/// </summary>
public sealed class RedisHaloCallSlots : IHaloCallSlots, IAsyncDisposable
{
    public const string KeySuffix = ":halo-call-pace";

    // In microseconds; written with %.0f, as tostring would round a number this large to 14 digits. The key lasts as long
    // as the turns queued in it, and a minute more. A write after TIME (not deterministic) needs the script's effects
    // replicated rather than the script itself: the default from Redis 5, asked for before it (a no-op since, and gone
    // from some servers, so only called if there).
    private const string Script = """
        if redis.replicate_commands then redis.replicate_commands() end
        local t = redis.call('TIME')
        local now = tonumber(t[1]) * 1000000 + tonumber(t[2])
        local per = tonumber(ARGV[1])
        local turn = tonumber(redis.call('GET', KEYS[1]) or '0')
        if turn < now then turn = now end
        local nextTurn = turn + per
        redis.call('SET', KEYS[1], string.format('%.0f', nextTurn), 'PX', math.ceil((nextTurn - now) / 1000) + 60000)
        return turn - now
        """;

    private readonly AppRedis _redis;
    private readonly bool _ownsRedis;

    public RedisHaloCallSlots(AppRedis redis, string key)
    {
        _redis = redis;
        Key = key;
    }

    /// <summary>On a connection of its own to <paramref name="connectionString"/>, disposed with it.</summary>
    public RedisHaloCallSlots(string connectionString, string key)
        : this(new AppRedis(connectionString, SignalR.SignalRBackplaneExtensions.DefaultChannelPrefix), key)
    {
        _ownsRedis = true;
    }

    public string Key { get; }

    /// <summary>The key: under the app's Redis prefix (<c>SignalR:RedisChannelPrefix</c>), so environments sharing a server keep their own.</summary>
    public static string KeyFor(IConfiguration configuration) => SignalR.SignalRBackplaneExtensions.ChannelPrefix(configuration) + KeySuffix;

    public async Task<TimeSpan> ReserveAsync(TimeSpan perCall, CancellationToken ct)
    {
        var connection = await _redis.ConnectAsync().WaitAsync(ct);
        var micros = (long)Math.Ceiling(perCall.TotalMicroseconds);
        var result = await connection.GetDatabase().ScriptEvaluateAsync(Script, new RedisKey[] { Key }, new RedisValue[] { micros }).WaitAsync(ct);
        return TimeSpan.FromMicroseconds((long)result);
    }

    public ValueTask DisposeAsync() => _ownsRedis ? _redis.DisposeAsync() : ValueTask.CompletedTask;
}

public static class HaloCallPacerExtensions
{
    /// <summary>
    /// The app's <see cref="HaloCallPacer"/>, one per process, every Halo call of the pulls and the XUID lookups through
    /// it; with <c>ConnectionStrings:Redis</c> (scaled out), its turns shared with every other pod's (<see cref="RedisHaloCallSlots"/>,
    /// on the process's one Redis connection).
    /// </summary>
    public static IServiceCollection AddHaloCallPacer(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.TryAddAppRedis(configuration))
        {
            var key = RedisHaloCallSlots.KeyFor(configuration);
            services.AddSingleton<IHaloCallSlots>(provider => new RedisHaloCallSlots(provider.GetRequiredService<AppRedis>(), key));
        }
        services.AddSingleton<HaloCallPacer>();
        return services;
    }
}
