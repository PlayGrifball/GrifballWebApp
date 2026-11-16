using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;
using GrifballWebApp.Application.Historical;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.Historical;

public class XuidLookupOptions
{
    /// <summary>
    /// Whether the worker looks anything up. False stops its Halo calls (e.g. to measure Halo's limits without it): the
    /// placeholders stay due, and are looked up once it is true again. Read at startup (IOptions): a change applies when the
    /// pod restarts, as it does when the environment variable (XuidLookup__Enabled) changes.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Placeholders looked up per batch; the worker runs batches until none is due.</summary>
    public int BatchSize { get; set; } = 50;
    /// <summary>
    /// At most this many lookups a minute, spread evenly. The Halo gateway keeps them within Halo's own profile limit (its
    /// budgets): a lookup over it is answered 429 with Retry-After, which stops the batch until then.
    /// </summary>
    public int LookupsPerMinute { get; set; } = 90;
    /// <summary>A placeholder found or not found is looked up again after this long (its gamertag may change hands).</summary>
    public TimeSpan LookAgainAfter { get; set; } = TimeSpan.FromDays(30);
    /// <summary>A lookup that failed (the API down, rate limited) is tried again after this long.</summary>
    public TimeSpan RetryFailedAfter { get; set; } = TimeSpan.FromHours(6);
    /// <summary>
    /// How often the lookups check for due placeholders (one cheap query). They run on the discord pod, apart from the API and the workers, so
    /// a sysadmin's "Look up XUIDs", or an import on a worker, reaches them through the database: within this long.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);
}

public class XuidLookupReport
{
    public int LookedUp { get; set; }
    public int Found { get; set; }
    public int NotFound { get; set; }
    public int Failed { get; set; }
    public int ClaimsCreated { get; set; }
    /// <summary>Placeholders whose name can't be a gamertag (<see cref="XboxUser.IsPossibleGamertag"/>): recorded as not found, never asked.</summary>
    public int NotGamertags { get; set; }
    /// <summary>
    /// Set when Halo, or the gateway's budget for it, answered 429: the batch stopped there, that placeholder left due, and
    /// this is how long to wait before asking again (its Retry-After; a minute without one).
    /// </summary>
    public TimeSpan? ThrottledFor { get; set; }
}

/// <summary>The XUID lookups of placeholders, by outcome, and how many are due.</summary>
public class XuidLookupStatusDto
{
    public int Placeholders { get; set; }
    public int Due { get; set; }
    public int Found { get; set; }
    public int NotFound { get; set; }
    public int Failed { get; set; }
    public DateTime? LastLookedUpAt { get; set; }
}

/// <summary>
/// Looks up the XUID that holds each import placeholder's gamertag now (gamertag to XUID, through the Halo client, so
/// it works in Gateway mode), and makes each one found a pending claim (<see cref="GamertagClaimKind.XuidLookup"/>):
/// never linked automatically, since the gamertag's current holder may not be the player who used it in the sheet's
/// year. Throttled and batched; each placeholder's last lookup is kept (<see cref="XuidLookup"/>), and one isn't looked
/// up again too soon.
/// </summary>
public class XuidLookupService
{
    private readonly GrifballContext _context;
    private readonly IHaloInfiniteClient _halo;
    private readonly GamertagMatching _matching;
    private readonly XuidLookupOptions _options;
    private readonly TimeProvider _time;

    public XuidLookupService(GrifballContext context, IHaloInfiniteClient halo, GamertagMatching matching, IOptions<XuidLookupOptions> options, TimeProvider time)
    {
        _context = context;
        _halo = halo;
        _matching = matching;
        _options = options.Value;
        _time = time;
    }

    /// <summary>Waits between two lookups (the throttle); replaceable in tests.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (delay, ct) => Task.Delay(delay, ct);

    /// <summary>The placeholders whose lookup is due: import placeholders with no Xbox account, never looked up, or
    /// looked up longer ago than <see cref="XuidLookupOptions.LookAgainAfter"/> (a failure: RetryFailedAfter).</summary>
    private IQueryable<User> Due()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var again = now - _options.LookAgainAfter;
        var retry = now - _options.RetryFailedAfter;
        return PlaceholderUsers().Where(u => !_context.XuidLookups.Any(l => l.UserID == u.Id
            && (l.Status == XuidLookupStatus.Failed ? l.LookedUpAt > retry : l.LookedUpAt > again)));
    }

    private IQueryable<User> PlaceholderUsers() => _context.Users.Where(User.IsImportPlaceholderExpression);

    /// <summary>Error of a placeholder whose name can't be a gamertag: recorded as not found without asking Halo.</summary>
    public const string NotAGamertagError = "Not a possible gamertag: not looked up";

    /// <summary>Looks up one batch of due placeholders. Returns what it did; LookedUp and NotGamertags 0 when none was due.
    /// A name that can't be a gamertag (<see cref="XboxUser.IsPossibleGamertag"/>) is never asked about: no Halo call, no wait.</summary>
    public async Task<XuidLookupReport> RunBatchAsync(CancellationToken ct = default)
    {
        var report = new XuidLookupReport();
        var due = await Due().OrderBy(u => u.Id).Take(Math.Max(1, _options.BatchSize))
            .Select(u => new { u.Id, u.UserName }).ToListAsync(ct);
        var gap = TimeSpan.FromMinutes(1) / Math.Max(1, _options.LookupsPerMinute);
        foreach (var placeholder in due)
        {
            var gamertag = User.PlaceholderGamertagOf(placeholder.UserName)!;
            var possible = XboxUser.IsPossibleGamertag(gamertag);
            if (possible)
            {
                if (report.LookedUp > 0)
                    await Delay(gap, ct);
                report.LookedUp++;
            }
            var lookup = await LookupRowAsync(placeholder.Id, ct);
            if (!possible)
            {
                lookup.Status = XuidLookupStatus.NotFound;
                lookup.Error = NotAGamertagError;
                report.NotGamertags++;
            }
            else if (await LookUpAsync(lookup, placeholder.Id, gamertag, report, ct))
            {
                // Throttled: nothing learned, so nothing recorded (it stays due), and no more asking until ThrottledFor has passed
                report.LookedUp--;
                _context.ChangeTracker.Clear();
                break;
            }
            await _context.SaveChangesAsync(ct);
        }
        return report;
    }

    /// <summary>The placeholder's lookup row (made if it has none), reset for a lookup now.</summary>
    private async Task<XuidLookup> LookupRowAsync(int placeholderId, CancellationToken ct)
    {
        var lookup = await _context.XuidLookups.FirstOrDefaultAsync(l => l.UserID == placeholderId, ct);
        if (lookup is null)
        {
            lookup = new XuidLookup { UserID = placeholderId };
            _context.XuidLookups.Add(lookup);
        }
        lookup.LookedUpAt = _time.GetUtcNow().UtcDateTime;
        lookup.XboxUserID = null;
        lookup.Error = null;
        return lookup;
    }

    /// <summary>
    /// Asks Xbox for the gamertag's XUID, writing what it said onto <paramref name="lookup"/> and <paramref name="report"/>. A 429
    /// (Halo throttling, or the gateway's budget for it turning the call away) says nothing about the gamertag: nothing is
    /// written, <see cref="XuidLookupReport.ThrottledFor"/> is set and true returned, so the batch stops. Any other error code
    /// with no result is a failed lookup, tried again after RetryFailedAfter (<see cref="IsFailure"/>: 400 bad request, 5xx,
    /// 401/403, and 0, which the gateway sends when Halo gave no answer), except Halo's 404 (the gamertag isn't there). That
    /// 404, or a success naming no user, is not found, for LookAgainAfter.
    /// </summary>
    private async Task<bool> LookUpAsync(XuidLookup lookup, int placeholderId, string gamertag, XuidLookupReport report, CancellationToken ct)
    {
        try
        {
            var response = await _halo.UserByGamertag(gamertag);
            if (response?.Result is null && response?.Error is { Code: 429 } throttled)
            {
                report.ThrottledFor = throttled.RetryAfter is { } wait && wait > TimeSpan.Zero ? wait : TimeSpan.FromMinutes(1);
                return true;
            }
            await RecordAsync(lookup, placeholderId, gamertag, response, report, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lookup.Status = XuidLookupStatus.Failed;
            lookup.Error = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
            report.Failed++;
        }
        return false;
    }

    /// <summary>Writes what Halo answered about the gamertag (anything but a 429) onto <paramref name="lookup"/> and <paramref name="report"/>.</summary>
    private async Task RecordAsync(XuidLookup lookup, int placeholderId, string gamertag,
        Surprenant.Grunt.Models.HaloApiResultContainer<Surprenant.Grunt.Models.User, Surprenant.Grunt.Models.HaloApiErrorContainer>? response,
        XuidLookupReport report, CancellationToken ct)
    {
        if (response?.Result is null && response?.Error is { } error && IsFailure(error.Code))
        {
            lookup.Status = XuidLookupStatus.Failed;
            lookup.Error = error.Code == 0 ? "Halo gave no answer" : $"Halo answered {error.Code}";
            report.Failed++;
        }
        else if (response?.Result is null || string.IsNullOrEmpty(response.Result.gamertag))
        {
            lookup.Status = XuidLookupStatus.NotFound;
            report.NotFound++;
        }
        else if (!long.TryParse(response.Result.xuid, out var xuid))
        {
            lookup.Status = XuidLookupStatus.Failed;
            lookup.Error = "The XUID Xbox gave isn't a number";
            report.Failed++;
        }
        else
        {
            lookup.Status = XuidLookupStatus.Found;
            lookup.XboxUserID = xuid;
            report.Found++;
            if (await ClaimAsync(placeholderId, gamertag, xuid, response.Result.gamertag, lookup.LookedUpAt, ct))
                report.ClaimsCreated++;
        }
    }

    /// <summary>A Halo code that isn't an answer about the gamertag: anything but success and 404 (it isn't there).</summary>
    private static bool IsFailure(int code) => code is not 404 and not (>= 200 and < 300);

    /// <summary>A pending claim pairing the placeholder with the XUID, unless the pair was claimed before (pending,
    /// approved or rejected: a rejected pair isn't suggested again). The Xbox account is stored if the app doesn't know it.</summary>
    private async Task<bool> ClaimAsync(int placeholderId, string gamertag, long xuid, string currentGamertag, DateTime lookedUpAt, CancellationToken ct)
    {
        if (!await _context.XboxUsers.AnyAsync(x => x.XboxUserID == xuid, ct))
            _context.XboxUsers.Add(XboxUser.FromXbox(xuid, currentGamertag));
        return await _matching.ClaimAsync(GamertagClaimKind.XuidLookup, xuid, gamertag, placeholderId, null, null, evenIfRejected: false, ct, (currentGamertag, lookedUpAt)) is not null;
    }

    /// <summary>A sysadmin's "Look up XUIDs": the placeholders not found or failed are due again now (the found ones
    /// keep their claims). The worker picks them up on its next poll.</summary>
    public async Task<int> LookUpAgainAsync(CancellationToken ct = default)
        => await _context.XuidLookups.Where(l => l.Status != XuidLookupStatus.Found).ExecuteDeleteAsync(ct);

    public async Task<XuidLookupStatusDto> StatusAsync(CancellationToken ct = default)
    {
        var lookups = await _context.XuidLookups.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return new XuidLookupStatusDto
        {
            Placeholders = await PlaceholderUsers().CountAsync(ct),
            Due = await Due().CountAsync(ct),
            Found = lookups.FirstOrDefault(l => l.Key == XuidLookupStatus.Found)?.Count ?? 0,
            NotFound = lookups.FirstOrDefault(l => l.Key == XuidLookupStatus.NotFound)?.Count ?? 0,
            Failed = lookups.FirstOrDefault(l => l.Key == XuidLookupStatus.Failed)?.Count ?? 0,
            LastLookedUpAt = await _context.XuidLookups.MaxAsync(l => (DateTime?)l.LookedUpAt, ct),
        };
    }
}

/// <summary>Wakes the XUID lookup worker (after an import, or a sysadmin asking).</summary>
public sealed class XuidLookupSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        if (_semaphore.CurrentCount == 0)
            try { _semaphore.Release(); } catch (SemaphoreFullException) { }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
}

/// <summary>Runs the XUID lookups in the background, in the discord role only (one per deployment, <see cref="Hosting.AppRole.DiscordHostedServices"/>): batches until none is due, when woken
/// or every PollInterval.</summary>
public sealed partial class XuidLookupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly XuidLookupSignal _signal;
    private readonly IOptions<XuidLookupOptions> _options;
    private readonly ILogger<XuidLookupWorker> _logger;

    public XuidLookupWorker(IServiceScopeFactory scopes, XuidLookupSignal signal, IOptions<XuidLookupOptions> options, ILogger<XuidLookupWorker> logger)
    {
        _scopes = scopes;
        _signal = signal;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunUntilDoneAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "XUID lookups failed; trying again later");
            }
            await WaitForNextRunAsync(stoppingToken);
        }
    }

    /// <summary>Waits between two runs (replaceable in tests).</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (delay, ct) => Task.Delay(delay, ct);

    /// <summary>
    /// Waits before the next run: after a throttled one, its <see cref="PausedFor"/> (at least PollInterval), which a signal
    /// doesn't cut short (Halo's or the gateway budget's Retry-After); otherwise up to PollInterval, or until signalled.
    /// </summary>
    public async Task WaitForNextRunAsync(CancellationToken ct)
    {
        var poll = _options.Value.PollInterval;
        if (PausedFor is { } paused)
            await Delay(paused > poll ? paused : poll, ct);
        else
            await _signal.WaitAsync(poll, ct);
    }

    /// <summary>When the last run was throttled (a 429): how long it asked to wait before the next. Null otherwise.</summary>
    public TimeSpan? PausedFor { get; private set; }

    /// <summary>Batches until none is due, or a 429 stops them (<see cref="PausedFor"/>). Returns the lookups made; none while
    /// <see cref="XuidLookupOptions.Enabled"/> is false.</summary>
    public async Task<int> RunUntilDoneAsync(CancellationToken ct)
    {
        var total = 0;
        PausedFor = null;
        while (true)
        {
            if (!_options.Value.Enabled)
                return total;
            await using var scope = _scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<XuidLookupService>().RunBatchAsync(ct);
            total += report.LookedUp;
            if (report.LookedUp > 0)
                LogLookups(_logger, report.Found, report.NotFound, report.Failed, report.ClaimsCreated);
            if (report.NotGamertags > 0)
                LogNotGamertags(_logger, report.NotGamertags);
            if (report.ThrottledFor is { } throttled)
            {
                PausedFor = throttled;
                LogThrottled(_logger, throttled);
                return total;
            }
            // A batch of names that aren't gamertags is a full batch too: more may be due after it
            if (report.LookedUp + report.NotGamertags < Math.Max(1, _options.Value.BatchSize) || ct.IsCancellationRequested)
                return total;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "XUID lookups: {Found} found, {NotFound} not found, {Failed} failed, {Claims} claims")]
    private static partial void LogLookups(ILogger logger, int found, int notFound, int failed, int claims);

    [LoggerMessage(Level = LogLevel.Information, Message = "XUID lookups: {Count} names that can't be gamertags skipped")]
    private static partial void LogNotGamertags(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "XUID lookups: Halo is throttling gamertag lookups; next batch in {Wait}")]
    private static partial void LogThrottled(ILogger logger, TimeSpan wait);
}
