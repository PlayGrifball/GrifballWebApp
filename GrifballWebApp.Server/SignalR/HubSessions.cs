using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace GrifballWebApp.Server.SignalR;

/// <summary>A hub connection's user as the database has them now: their ID and roles; no ID signed out.</summary>
public sealed record HubUserState(int? UserId, IReadOnlyList<string> Roles)
{
    public static readonly HubUserState Anonymous = new(null, []);

    /// <summary>The same user with the same roles (in any order).</summary>
    public bool SameAs(HubUserState other) => UserId == other.UserId && Roles.ToHashSet().SetEquals(other.Roles);
}

/// <summary>
/// The user a hub call is for, as <see cref="HubSessionFilter"/> found them in the database for this call (scoped: one per call or
/// connection), never the connection's claims from when it connected. Hubs read roles here.
/// </summary>
public sealed class HubUser
{
    public HubUserState State { get; private set; } = HubUserState.Anonymous;

    public IReadOnlyList<string> Roles => State.Roles;

    public void Set(HubUserState state) => State = state;

    /// <summary>The caller the agents' tools run as: this user with their roles now, else no account.</summary>
    public ToolCaller AsToolCaller() => State.UserId is { } id ? new ToolCaller(id, State.Roles) : ToolCaller.Anonymous;
}

/// <summary>
/// A connection's claims checked against the database: still that user, still signed in (the security stamp the claims carry is
/// still theirs: a sign-out, a new password or a sysadmin's change of it ends it), not locked out, and their roles now.
/// </summary>
public class HubSessionValidator(SignInManager<User> signIn)
{
    /// <summary>The user now: <see cref="HubUserState.Anonymous"/> for a connection signed out, null for one whose session has ended.</summary>
    public virtual async Task<HubUserState?> CurrentAsync(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return HubUserState.Anonymous;
        var user = await signIn.ValidateSecurityStampAsync(principal);
        if (user is null || await signIn.UserManager.IsLockedOutAsync(user))
            return null;
        return new HubUserState(user.Id, [.. await signIn.UserManager.GetRolesAsync(user)]);
    }
}

/// <summary>This process's open hub connections (every hub's), each with its user as they were when it connected.</summary>
public sealed class HubConnections
{
    public sealed record Connection(HubCallerContext Context, HubUserState User);

    private readonly ConcurrentDictionary<string, Connection> _connections = new();

    public void Add(HubCallerContext context, HubUserState user) => _connections[context.ConnectionId] = new Connection(context, user);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public Connection? Find(string connectionId) => _connections.GetValueOrDefault(connectionId);

    /// <summary>The connections of a signed-in user.</summary>
    public IReadOnlyList<Connection> SignedIn() => _connections.Values.Where(c => c.User.UserId is not null).ToList();

    /// <summary>Closes the connections of <paramref name="userIds"/> here; how many there were.</summary>
    public int End(IReadOnlyCollection<int> userIds)
    {
        var ended = 0;
        foreach (var connection in _connections.Values.Where(c => c.User.UserId is { } id && userIds.Contains(id)))
        {
            connection.Context.Abort();
            ended++;
        }
        return ended;
    }
}

/// <summary>
/// Every hub's calls and connections checked against the database (<see cref="HubSessionValidator"/>), not the claims the connection
/// was made with: a user signed out, locked out, deleted or whose roles changed is refused and their connection closed, so they
/// connect again as they are now (and are put in the groups their roles allow then). A call also needs the roles its
/// <c>[Authorize]</c> names, as they are now. The user found is the call's <see cref="HubUser"/>. Registered first, so a refusal
/// is no fault to the hubs' error log.
/// </summary>
public sealed class HubSessionFilter(HubConnections connections) : IHubFilter
{
    public const string SessionEnded = "Your session has ended: sign in again.";
    public const string RolesChanged = "Your roles have changed: connect again.";
    public const string NotAllowed = "You may not do that.";

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var user = await Current(context.ServiceProvider, context.Context.User) ?? throw new HubException(SessionEnded);
        context.ServiceProvider.GetRequiredService<HubUser>().Set(user);
        connections.Add(context.Context, user);
        await next(context);
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        connections.Remove(context.Context.ConnectionId);
        return next(context, exception);
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = await Current(invocationContext.ServiceProvider, invocationContext.Context.User);
        var refusal = Refusal(user, connections.Find(invocationContext.Context.ConnectionId)?.User, invocationContext.HubMethod, invocationContext.Hub.GetType());
        if (refusal is not null)
        {
            invocationContext.Context.Abort();
            throw new HubException(refusal);
        }
        invocationContext.ServiceProvider.GetRequiredService<HubUser>().Set(user!);
        return await next(invocationContext);
    }

    private static Task<HubUserState?> Current(IServiceProvider services, ClaimsPrincipal? principal)
        => services.GetRequiredService<HubSessionValidator>().CurrentAsync(principal);

    /// <summary>
    /// Why a call is refused, its connection closed: the session ended, the roles changed since it connected (its groups were
    /// the old roles'), or the method's roles aren't the user's now; null when it may go ahead.
    /// </summary>
    public static string? Refusal(HubUserState? now, HubUserState? connected, MethodInfo method, Type hub)
    {
        if (now is null)
            return SessionEnded;
        if (connected is not null && !connected.SameAs(now))
            return RolesChanged;
        return Allows(method, hub, now) ? null : NotAllowed;
    }

    /// <summary>Whether <paramref name="user"/> passes every <c>[Authorize]</c> on the method and its hub, with their roles now.</summary>
    public static bool Allows(MethodInfo method, Type hub, HubUserState user)
        => method.GetCustomAttributes<AuthorizeAttribute>(true).Concat(hub.GetCustomAttributes<AuthorizeAttribute>(true))
            .All(a => user.UserId is not null && (string.IsNullOrWhiteSpace(a.Roles)
                || a.Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(user.Roles.Contains)));
}

/// <summary>
/// Ends users' hub connections (<see cref="IUserSessions"/>, called once a change to their sign-in or roles is committed): this
/// process's at once, and, scaled out (<c>ConnectionStrings:Redis</c>), every other process's through Redis
/// (<see cref="HubUserSessionsListener"/>). Their pushes stop with them: a connection that only listens (TeamsHub's staff group)
/// is closed too, and connects again as the user is now.
/// </summary>
public sealed class HubUserSessions(HubConnections connections, ILogger<HubUserSessions> logger, AppRedis? redis = null) : IUserSessions
{
    /// <summary>The channel (under the app's prefix) the users whose sessions ended are published on.</summary>
    public static readonly RedisChannel Channel = RedisChannel.Literal("user-sessions-ended");

    public async Task EndAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        connections.End(userIds);
        if (redis is null || userIds.Count == 0)
            return;
        try
        {
            var connection = await redis.ConnectAsync();
            await connection.GetSubscriber().PublishAsync(Channel, Format(userIds));
        }
        catch (Exception e)
        {
            // The other processes' sweep (HubSessionSweep) still ends them
            logger.LogWarning(e, "Could not tell the other processes to end the hub connections of users {UserIds}", userIds);
        }
    }

    public static string Format(IEnumerable<int> userIds) => string.Join(',', userIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));

    /// <summary>The users in a message; none for one that isn't a list of IDs.</summary>
    public static List<int> Parse(string? message)
        => (message ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) ? userId : (int?)null)
            .OfType<int>()
            .ToList();
}

/// <summary>Scaled out: ends this process's connections of the users another process published (<see cref="HubUserSessions"/>).</summary>
public sealed class HubUserSessionsListener(HubConnections connections, AppRedis? redis = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        redis?.WhenConnected(connection => connection.GetSubscriber()
            .Subscribe(HubUserSessions.Channel, (_, message) => connections.End(HubUserSessions.Parse(message))));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// The backstop: every <see cref="Interval"/>, each signed-in connection here is checked against the database as a call would be, and
/// closed when its session has ended or its roles changed. Catches what no save announced (a change made in SQL, a lost Redis
/// message); the announced ones are closed at once.
/// </summary>
public sealed class HubSessionSweep(HubConnections connections, IServiceScopeFactory scopes, TimeProvider time, ILogger<HubSessionSweep> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Could not check the hub connections' sessions");
            }
        }
    }

    /// <summary>Closes the connections whose user is no longer as they connected; how many.</summary>
    public async Task<int> SweepAsync()
    {
        var ended = 0;
        foreach (var connection in connections.SignedIn())
        {
            await using var scope = scopes.CreateAsyncScope();
            var now = await scope.ServiceProvider.GetRequiredService<HubSessionValidator>().CurrentAsync(connection.Context.User);
            if (now is not null && now.SameAs(connection.User))
                continue;
            connection.Context.Abort();
            ended++;
        }
        return ended;
    }
}
