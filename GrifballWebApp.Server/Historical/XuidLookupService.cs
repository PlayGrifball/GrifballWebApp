using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Historical;

public class XuidLookupOptions
{
    /// <summary>Placeholders looked up per batch; the worker runs batches until none is due.</summary>
    public int BatchSize { get; set; } = 50;
    /// <summary>
    /// At most this many lookups a minute. They go through the Halo gateway, whose per-key limit is 600 a minute in
    /// production and 120 in previews: each also waits its turn on the pace every pod's Halo calls share
    /// (<see cref="HaloInfinite.HaloCallPacer"/>), so with the pulls they keep to the key's quota; this keeps them from
    /// taking every turn while a pull waits.
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
    private readonly HaloInfinite.HaloCallPacer _pacer;

    public XuidLookupService(GrifballContext context, IHaloInfiniteClient halo, GamertagMatching matching, IOptions<XuidLookupOptions> options, TimeProvider time,
        HaloInfinite.HaloCallPacer? pacer = null)
    {
        _pacer = pacer ?? new HaloInfinite.HaloCallPacer(0);
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

    /// <summary>Looks up one batch of due placeholders. Returns what it did; LookedUp 0 when none was due.</summary>
    public async Task<XuidLookupReport> RunBatchAsync(CancellationToken ct = default)
    {
        var report = new XuidLookupReport();
        var due = await Due().OrderBy(u => u.Id).Take(Math.Max(1, _options.BatchSize))
            .Select(u => new { u.Id, u.UserName }).ToListAsync(ct);
        var gap = TimeSpan.FromMinutes(1) / Math.Max(1, _options.LookupsPerMinute);
        foreach (var placeholder in due)
        {
            if (report.LookedUp > 0)
                await Delay(gap, ct);
            // The Halo call's turn, shared with the pulls and every other pod's calls on the key
            await _pacer.WaitAsync(ct);
            report.LookedUp++;
            var gamertag = User.PlaceholderGamertagOf(placeholder.UserName)!;
            var lookup = await _context.XuidLookups.FirstOrDefaultAsync(l => l.UserID == placeholder.Id, ct);
            if (lookup is null)
            {
                lookup = new XuidLookup { UserID = placeholder.Id };
                _context.XuidLookups.Add(lookup);
            }
            lookup.LookedUpAt = _time.GetUtcNow().UtcDateTime;
            lookup.XboxUserID = null;
            lookup.Error = null;
            try
            {
                var response = await _halo.UserByGamertag(gamertag);
                if (response?.Result is null || string.IsNullOrEmpty(response.Result.gamertag))
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
                    if (await ClaimAsync(placeholder.Id, gamertag, xuid, response.Result.gamertag, lookup.LookedUpAt, ct))
                        report.ClaimsCreated++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lookup.Status = XuidLookupStatus.Failed;
                lookup.Error = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
                report.Failed++;
            }
            await _context.SaveChangesAsync(ct);
        }
        return report;
    }

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
public sealed class XuidLookupWorker : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunUntilDoneAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "XUID lookups failed; trying again later");
            }
            await _signal.WaitAsync(_options.Value.PollInterval, ct);
        }
    }

    /// <summary>Batches until none is due. Returns the lookups made.</summary>
    public async Task<int> RunUntilDoneAsync(CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<XuidLookupService>().RunBatchAsync(ct);
            total += report.LookedUp;
            if (report.LookedUp > 0)
                _logger.LogInformation("XUID lookups: {Found} found, {NotFound} not found, {Failed} failed, {Claims} claims", report.Found, report.NotFound, report.Failed, report.ClaimsCreated);
            if (report.LookedUp < Math.Max(1, _options.Value.BatchSize) || ct.IsCancellationRequested)
                return total;
        }
    }
}
