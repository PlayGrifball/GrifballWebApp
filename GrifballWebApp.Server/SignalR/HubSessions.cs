using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
public class HubSessionValidator(SignInManager<User> signIn, GrifballContext context, IOptions<IdentityOptions> identity, TimeProvider time)
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

    /// <summary>
    /// What <see cref="CurrentAsync(ClaimsPrincipal?)"/> finds for each of <paramref name="principals"/>, in order, read in two
    /// queries for all their users together (the sweep's): the stamp, the lockout and the roles, as Identity checks them.
    /// </summary>
    public virtual async Task<IReadOnlyList<HubUserState?>> CurrentAllAsync(IReadOnlyList<ClaimsPrincipal> principals)
    {
        var ids = principals.Select(UserIdOf).OfType<int>().Distinct().ToList();
        var users = await context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.SecurityStamp, u.LockoutEnabled, u.LockoutEnd })
            .ToDictionaryAsync(u => u.Id);
        var roles = (await context.UserRoles.AsNoTracking()
                .Where(ur => ids.Contains(ur.UserId))
                .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .ToListAsync())
            .ToLookup(r => r.UserId, r => r.Name!);
        var now = time.GetUtcNow();
        return principals.Select(principal =>
        {
            if (principal.Identity?.IsAuthenticated != true)
                return HubUserState.Anonymous;
            if (UserIdOf(principal) is not { } id || !users.TryGetValue(id, out var user)
                || user.SecurityStamp != principal.FindFirstValue(identity.Value.ClaimsIdentity.SecurityStampClaimType)
                || (user.LockoutEnabled && user.LockoutEnd > now))
                return null;
            return new HubUserState(id, [.. roles[id]]);
        }).ToList();
    }

    private int? UserIdOf(ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirstValue(identity.Value.ClaimsIdentity.UserIdClaimType), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
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
        try
        {
            await next(context);
        }
        catch
        {
            // The hub refused it (or failed): no connection, and OnDisconnectedAsync may never come
            connections.Remove(context.Context.ConnectionId);
            throw;
        }
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        connections.Remove(context.Context.ConnectionId);
        return next(context, exception);
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = await Current(invocationContext.ServiceProvider, invocationContext.Context.User);
        var refusal = await RefusalAsync(user, connections.Find(invocationContext.Context.ConnectionId)?.User, invocationContext);
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
    /// the old roles'), or the method's authorization (<see cref="AllowsAsync"/>) refuses the user as they are now; null when it may go
    /// ahead.
    /// </summary>
    public static async Task<string?> RefusalAsync(HubUserState? now, HubUserState? connected, HubInvocationContext invocationContext)
    {
        if (now is null)
            return SessionEnded;
        if (connected is not null && !connected.SameAs(now))
            return RolesChanged;
        return await AllowsAsync(invocationContext.ServiceProvider, invocationContext.HubMethod, invocationContext.Hub.GetType(), invocationContext.Context.User, now)
            ? null
            : NotAllowed;
    }

    /// <summary>
    /// Whether the method's and its hub's authorization (<c>[Authorize]</c>: roles, policies, a signed-in user; <c>[AllowAnonymous]</c>
    /// on the method lets anyone) allows <paramref name="user"/> as the database has them now, through ASP.NET Core's own authorization:
    /// the connection's claims with the roles they hold now.
    /// </summary>
    public static async Task<bool> AllowsAsync(IServiceProvider services, MethodInfo method, Type hub, ClaimsPrincipal? claims, HubUserState user)
    {
        if (method.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any())
            return true;
        var data = hub.GetCustomAttributes(true).OfType<IAuthorizeData>().Concat(method.GetCustomAttributes(true).OfType<IAuthorizeData>()).ToList();
        if (data.Count == 0)
            return true;
        // Some authorize data: a policy, never none
        var policy = (await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), data))!;
        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(AsNow(claims, user), policy);
        return result.Succeeded;
    }

    /// <summary>The connection's user as they are now: their claims, but their roles the database's; signed out, no one.</summary>
    public static ClaimsPrincipal AsNow(ClaimsPrincipal? claims, HubUserState user)
    {
        if (user.UserId is null || claims?.Identity is not ClaimsIdentity identity)
            return new ClaimsPrincipal(new ClaimsIdentity());
        var kept = claims.Claims.Where(c => c.Type != identity.RoleClaimType);
        return new ClaimsPrincipal(new ClaimsIdentity(kept.Concat(user.Roles.Select(r => new Claim(identity.RoleClaimType, r))),
            identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType));
    }
}

/// <summary>
/// Ends users' hub connections (<see cref="IUserSessions"/>, called once a change to their sign-in or roles is committed): this
/// process's at once, and, scaled out (<c>ConnectionStrings:Redis</c>), every other process's through Redis
/// (<see cref="HubUserSessionsListener"/>). Their pushes stop with them: a connection that only listens (LiveHub's watchers of a hidden season)
/// is closed too, and connects again as the user is now.
/// </summary>
public sealed class HubUserSessions(HubConnections connections, ILogger<HubUserSessions> logger, AppRedis? redis = null) : IUserSessions
{
    /// <summary>The channel (under the app's prefix) the users whose sessions ended are published on.</summary>
    public static readonly RedisChannel Channel = RedisChannel.Literal("user-sessions-ended");

    public void End(IReadOnlyCollection<int> userIds)
    {
        connections.End(userIds);
        if (redis is not null && userIds.Count > 0)
            Published = Task.Run(() => PublishAsync([.. userIds]));
    }

    /// <summary>The last publication started (tests wait on it); it never faults.</summary>
    public Task Published { get; private set; } = Task.CompletedTask;

    /// <summary>The users published for the other processes: in the background, a failure logged (their sweep still ends them).</summary>
    private async Task PublishAsync(IReadOnlyCollection<int> userIds)
    {
        try
        {
            var connection = await redis!.ConnectAsync();
            await connection.GetSubscriber().PublishAsync(Channel, Format(userIds));
        }
        catch (Exception e)
        {
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
        var signedIn = connections.SignedIn();
        if (signedIn.Count == 0)
            return 0;
        await using var scope = scopes.CreateAsyncScope();
        // Every connection's user at once: two queries, however many connections
        var now = await scope.ServiceProvider.GetRequiredService<HubSessionValidator>().CurrentAllAsync(signedIn.Select(c => c.Context.User!).ToList());
        var ended = 0;
        foreach (var (connection, user) in signedIn.Zip(now))
        {
            if (user is not null && user.SameAs(connection.User))
                continue;
            connection.Context.Abort();
            ended++;
        }
        return ended;
    }
}
