using Surprenant.Grunt.Models;
using System.Text.Json;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// Keeps the stored Halo Infinite token fresh, in the one process that owns the sign-in (<see cref="HaloTokenOwnership"/>):
/// exchanges an admin's authorization code for a token as soon as it is stored, and refreshes the token
/// <c>HaloInfinite:TokenRefresh:RefreshBefore</c> (default 15 min) before it expires, checking every
/// <c>HaloInfinite:TokenRefresh:CheckInterval</c> (default 1 min); after a refused refresh it waits
/// <c>HaloInfinite:TokenRefresh:RetryAfter</c> (default 10 min). The other processes only read the token, so no two
/// processes ever refresh it at once.
/// At start it imports, once, the files Grunt kept in the working directory before (tokens.json,
/// AccountAuthorization.txt; <c>HaloInfinite:LegacyDirectory</c>, default the working directory), so a deployment that
/// had them needn't authorize the account again. The files are left in place (an older version still reads them).
/// </summary>
public sealed class HaloTokenRefreshService : BackgroundService
{
    public const string TokenFile = "tokens.json";
    public const string CodeFile = "AccountAuthorization.txt";

    public static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultRefreshBefore = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(10);

    public enum Outcome { NotSignedIn, Fresh, SignedIn, Refreshed, Failed, Waiting }

    private readonly IHaloTokenStore _store;
    private readonly IXboxTokenClient _client;
    private readonly TimeProvider _time;
    private readonly ILogger<HaloTokenRefreshService> _logger;
    private readonly string _legacyDirectory;
    private DateTime _retryAt = DateTime.MinValue;

    public HaloTokenRefreshService(IHaloTokenStore store, IXboxTokenClient client, TimeProvider time, IConfiguration configuration, ILogger<HaloTokenRefreshService> logger)
    {
        _store = store;
        _client = client;
        _time = time;
        _logger = logger;
        CheckInterval = ReadPositive(configuration, "HaloInfinite:TokenRefresh:CheckInterval", DefaultCheckInterval);
        RefreshBefore = ReadPositive(configuration, "HaloInfinite:TokenRefresh:RefreshBefore", DefaultRefreshBefore);
        RetryAfter = ReadPositive(configuration, "HaloInfinite:TokenRefresh:RetryAfter", DefaultRetryAfter);
        _legacyDirectory = configuration["HaloInfinite:LegacyDirectory"] is { Length: > 0 } directory ? directory : Directory.GetCurrentDirectory();
    }

    public TimeSpan CheckInterval { get; }
    public TimeSpan RefreshBefore { get; }
    public TimeSpan RetryAfter { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ImportLegacyFilesAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not import the Halo Infinite token files");
        }

        using var timer = new PeriodicTimer(CheckInterval, _time);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Halo Infinite token refresh failed");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>One check: signs in with a pending code, or refreshes a token near its expiry.</summary>
    public async Task<Outcome> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        if (now < _retryAt)
            return Outcome.Waiting;

        var stored = await _store.GetTokenAsync(cancellationToken);
        if (stored is null)
        {
            if (await _store.TakeCodeAsync(cancellationToken) is not { } code)
                return Outcome.NotSignedIn;
            var token = await _client.RequestTokenAsync(code);
            if (token is null)
            {
                _logger.LogWarning("The Halo Infinite authorization code was refused; an admin must authorize the account again");
                return Outcome.Failed;
            }
            await _store.SetTokenAsync(token, cancellationToken: cancellationToken);
            _logger.LogInformation("Signed in to Halo Infinite with the admin's authorization code");
            return Outcome.SignedIn;
        }

        // Unknown expiry (an imported token): refreshed now, which also learns it.
        if (stored.ExpiresAt is { } expiresAt && expiresAt - now > RefreshBefore)
            return Outcome.Fresh;

        if (string.IsNullOrEmpty(stored.Token.RefreshToken))
        {
            _logger.LogWarning("The stored Halo Infinite token has no refresh token; an admin must authorize the account again");
            _retryAt = now + RetryAfter;
            return Outcome.Failed;
        }

        var refreshed = await _client.RefreshTokenAsync(stored.Token.RefreshToken);
        if (refreshed is null)
        {
            _logger.LogWarning("Microsoft refused to refresh the Halo Infinite token; trying again in {RetryAfter}", RetryAfter);
            _retryAt = now + RetryAfter;
            return Outcome.Failed;
        }

        // A refresh may not hand out a new refresh token: the old one still works then.
        if (string.IsNullOrEmpty(refreshed.RefreshToken))
            refreshed.RefreshToken = stored.Token.RefreshToken;
        await _store.SetTokenAsync(refreshed, cancellationToken: cancellationToken);
        _logger.LogInformation("Refreshed the Halo Infinite token");
        return Outcome.Refreshed;
    }

    /// <summary>
    /// Once: tokens.json and AccountAuthorization.txt from the working directory into the store, unless it already has
    /// a token (or code). Marked done in the store, so a later sign-out isn't undone by importing the old token again.
    /// </summary>
    public async Task<bool> ImportLegacyFilesAsync(CancellationToken cancellationToken = default)
    {
        var tokenFile = Path.Combine(_legacyDirectory, TokenFile);
        var codeFile = Path.Combine(_legacyDirectory, CodeFile);
        if (!File.Exists(tokenFile) && !File.Exists(codeFile))
            return false;
        if (await _store.LegacyFilesImportedAsync(cancellationToken))
            return false;

        var imported = false;
        var hasToken = await _store.GetTokenAsync(cancellationToken) is not null;
        if (!hasToken && File.Exists(tokenFile)
            && JsonSerializer.Deserialize<OAuthToken>(await File.ReadAllTextAsync(tokenFile, cancellationToken)) is { AccessToken: not null } token)
        {
            await _store.SetTokenAsync(token, expiryKnown: false, cancellationToken);
            _logger.LogInformation("Imported the Halo Infinite token from {File}", tokenFile);
            hasToken = imported = true;
        }

        if (!hasToken && File.Exists(codeFile) && (await File.ReadAllTextAsync(codeFile, cancellationToken)).Trim() is { Length: > 0 } code)
        {
            await _store.SetCodeAsync(code, cancellationToken);
            _logger.LogInformation("Imported the Halo Infinite authorization code from {File}", codeFile);
            imported = true;
        }

        await _store.MarkLegacyFilesImportedAsync(cancellationToken);
        return imported;
    }

    private static TimeSpan ReadPositive(IConfiguration configuration, string key, TimeSpan fallback)
        => TimeSpan.TryParse(configuration[key], out var value) && value > TimeSpan.Zero ? value : fallback;
}
