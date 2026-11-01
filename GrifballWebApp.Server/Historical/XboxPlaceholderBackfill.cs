namespace GrifballWebApp.Server.Historical;

public class XboxPlaceholderOptions
{
    /// <summary>How often the worker gives Xbox accounts no user holds their placeholder (one cheap query when there are none).</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Gives every Xbox account no user holds a placeholder (<see cref="GamertagMatching.EnsureXboxPlaceholdersAsync"/>), at
/// startup and then every <see cref="XboxPlaceholderOptions.Interval"/>: the accounts known before placeholders were made
/// for them, and those a gamertag lookup stored since. Idempotent; the worker role only.
/// </summary>
public sealed class XboxPlaceholderBackfillWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Microsoft.Extensions.Options.IOptions<XboxPlaceholderOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<XboxPlaceholderBackfillWorker> _logger;

    public XboxPlaceholderBackfillWorker(IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<XboxPlaceholderOptions> options, TimeProvider time, ILogger<XboxPlaceholderBackfillWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Xbox placeholders failed; trying again later");
            }
            await Task.Delay(_options.Value.Interval, _time, ct);
        }
    }

    /// <summary>One pass. Returns the placeholders made.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var made = await scope.ServiceProvider.GetRequiredService<GamertagMatching>().EnsureXboxPlaceholdersAsync(null, ct);
        if (made > 0)
            _logger.LogInformation("Made {Count} placeholders for Xbox accounts no user holds", made);
        return made;
    }
}
