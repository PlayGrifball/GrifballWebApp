using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace GrifballWebApp.Server.Live;

/// <summary>
/// The page a tab says it shows, as everyone may see it (the footer lists it for all): its route alone (no query, no
/// fragment, at most <see cref="MaxLength"/> characters of a route's own), or <see cref="PresenceSnapshot.StaffArea"/> for a
/// staff page, and for the page of a season (or of its match) not announced yet: the public never learns of it (#435).
/// </summary>
public partial class PresencePages(SeasonVisibility visibility, IApplicationDbContext context)
{
    public const int MaxLength = 128;

    /// <summary>The staff's pages (the client's routes), shown as the staff area.</summary>
    private static readonly string[] StaffPrefixes =
        ["/usermanagement", "/seasonmanager", "/seasonedit", "/commissioner-dashboard", "/databasebackup", "/excel", "/infiniteclient"];

    /// <summary>A season's staff pages.</summary>
    private static readonly string[] StaffSeasonPages = ["playergrades", "signups"];

    [GeneratedRegex(@"^/[A-Za-z0-9/_.-]*$")]
    private static partial Regex RouteChars();

    [GeneratedRegex(@"^/(?:season|seasonavailability)/(\d{1,9})(?:/([a-z]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonPage();

    [GeneratedRegex(@"^/(?:seasonmatch|reschedule-request)/(\d{1,9})", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonMatchPage();

    public virtual async Task<string> Shown(string? path, CancellationToken ct = default)
    {
        var route = Route(path);
        if (StaffPrefixes.Any(p => route.Equals(p, StringComparison.OrdinalIgnoreCase) || route.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
            return PresenceSnapshot.StaffArea;

        if (SeasonPage().Match(route) is { Success: true } season)
        {
            if (StaffSeasonPages.Contains(season.Groups[2].Value, StringComparer.OrdinalIgnoreCase))
                return PresenceSnapshot.StaffArea;
            return await Public(Id(season), ct) ? route : PresenceSnapshot.StaffArea;
        }
        if (SeasonMatchPage().Match(route) is { Success: true } match)
        {
            var seasonMatchID = Id(match);
            var seasonID = await context.SeasonMatches.AsNoTracking()
                .Where(m => m.SeasonMatchID == seasonMatchID)
                .Select(m => (int?)m.SeasonID)
                .FirstOrDefaultAsync(ct);
            return seasonID is int id && await Public(id, ct) ? route : PresenceSnapshot.StaffArea;
        }
        return route;
    }

    /// <summary>The route of <paramref name="path"/>: from its "/", without a query, a fragment or a trailing "/"; "/" for none.</summary>
    public static string Route(string? path)
    {
        var route = (path ?? "").Split('?', '#')[0].TrimEnd('/');
        if (route.Length == 0 || route.Length > MaxLength || !RouteChars().IsMatch(route))
            return "/";
        return route;
    }

    private static int Id(Match match) => int.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);

    private Task<bool> Public(int seasonID, CancellationToken ct) => visibility.CanSeeSeason(seasonID, SeasonViewer.Public, ct);
}

/// <summary>
/// Who is on the site (<see cref="PresenceSnapshot"/>): this process's tabs (<see cref="LiveHub"/>'s connections and the page
/// each shows), with every other API process's through <see cref="IPresenceStore"/>. A change waits <see cref="Settle"/>
/// (tabs come and go in bursts: a page load, a rollout), then the snapshot goes to every tab if it changed; every
/// <see cref="Heartbeat"/> this process's tabs are stored again, which keeps them alive there and drops those of a process
/// that stopped.
/// </summary>
public sealed class PresenceTracker(IPresenceStore store, IHubContext<LiveHub, ILiveHubClient> hub, IServiceScopeFactory scopes,
    TimeProvider time, ILogger<PresenceTracker> logger) : BackgroundService
{
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<string, PresenceTab> _tabs = new(StringComparer.Ordinal);
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private string _sent = "";

    /// <summary>Who is on the site, as last sent.</summary>
    public PresenceSnapshot Current { get; private set; } = PresenceSnapshot.Empty;

    /// <summary>A tab shows <paramref name="path"/> now (already as everyone may see it: <see cref="PresencePages"/>).</summary>
    public async Task Viewing(string connectionID, int? userID, string path, CancellationToken ct = default)
    {
        var name = userID is int id
            ? _tabs.TryGetValue(connectionID, out var tab) && tab.UserID == id ? tab.Name : await NameOf(id, ct)
            : null;
        _tabs[connectionID] = new PresenceTab(connectionID, userID, name, path);
        _signal.Writer.TryWrite(true);
    }

    /// <summary>A tab closed.</summary>
    public void Left(string connectionID)
    {
        if (_tabs.TryRemove(connectionID, out _))
            _signal.Writer.TryWrite(true);
    }

    /// <summary>The name the site shows for a user (User.Named: never their login name).</summary>
    private async Task<string?> NameOf(int userID, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Users.AsNoTracking()
            .Where(u => u.Id == userID)
            .Select(User.Named)
            .Select(n => n.Name)
            .FirstOrDefaultAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var beat = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                beat.CancelAfter(Heartbeat);
                try
                {
                    await _signal.Reader.WaitToReadAsync(beat.Token);
                    _signal.Reader.TryRead(out _);
                    await Task.Delay(Settle, time, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // The heartbeat: stored again, and any process gone is dropped
                }
            }
            await SendAsync(stoppingToken);
        }
    }

    /// <summary>Stores this process's tabs, and sends the snapshot of everyone's if it changed.</summary>
    public async Task SendAsync(CancellationToken ct = default)
    {
        try
        {
            var snapshot = PresenceSnapshot.Of(await store.Exchange([.. _tabs.Values], ct));
            var json = JsonSerializer.Serialize(snapshot);
            Current = snapshot;
            if (json == _sent)
                return;
            _sent = json;
            await hub.Clients.All.Presence(snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not send who is on the site");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        // Gone at once for the others, not a heartbeat or two from now
        await store.Leave(cancellationToken);
    }
}

/// <summary>Every API process's tabs: each stores its own and reads everyone's.</summary>
public interface IPresenceStore
{
    /// <summary>Stores this process's <paramref name="tabs"/> in place of what it stored before; returns every process's.</summary>
    Task<IReadOnlyList<PresenceTab>> Exchange(IReadOnlyList<PresenceTab> tabs, CancellationToken ct = default);

    /// <summary>This process stops: its tabs go.</summary>
    Task Leave(CancellationToken ct = default);
}

/// <summary>One process (no Redis): its own tabs are everyone's.</summary>
public sealed class MemoryPresenceStore : IPresenceStore
{
    public Task<IReadOnlyList<PresenceTab>> Exchange(IReadOnlyList<PresenceTab> tabs, CancellationToken ct = default) => Task.FromResult(tabs);

    public Task Leave(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Scaled out (<c>ConnectionStrings:Redis</c>): a Redis hash under the app's prefix, a field a process holding its tabs and
/// when it stored them. A process that stored none for <see cref="Stale"/> stopped without leaving (killed): its field is
/// dropped. Redis down, this process's tabs alone are shown.
/// </summary>
public sealed class RedisPresenceStore(AppRedis redis, IConfiguration configuration, TimeProvider time, ILogger<RedisPresenceStore> logger) : IPresenceStore
{
    public static readonly TimeSpan Stale = PresenceTracker.Heartbeat * 3;

    private readonly RedisKey _key = $"{AppRedisExtensions.ChannelPrefix(configuration)}presence";
    private readonly RedisValue _field = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private sealed record Stored(DateTimeOffset At, List<PresenceTab> Tabs);

    public async Task<IReadOnlyList<PresenceTab>> Exchange(IReadOnlyList<PresenceTab> tabs, CancellationToken ct = default)
    {
        try
        {
            var db = (await redis.ConnectAsync()).GetDatabase();
            var now = time.GetUtcNow();
            await db.HashSetAsync(_key, _field, JsonSerializer.Serialize(new Stored(now, [.. tabs])));
            await db.KeyExpireAsync(_key, Stale);
            var all = new List<PresenceTab>();
            foreach (var entry in await db.HashGetAllAsync(_key))
            {
                var stored = Read(entry.Value);
                if (stored is null || now - stored.At > Stale)
                {
                    await db.HashDeleteAsync(_key, entry.Name);
                    continue;
                }
                all.AddRange(stored.Tabs);
            }
            return all;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or JsonException)
        {
            logger.LogWarning(ex, "Could not read who is on the other processes: showing this one's");
            return tabs;
        }
    }

    public async Task Leave(CancellationToken ct = default)
    {
        try
        {
            await (await redis.ConnectAsync()).GetDatabase().HashDeleteAsync(_key, _field);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not take this process's tabs out of who is on the site");
        }
    }

    private static Stored? Read(RedisValue value)
    {
        try
        {
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Stored>(value.ToString());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
