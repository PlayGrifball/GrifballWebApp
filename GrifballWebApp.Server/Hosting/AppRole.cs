using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;

namespace GrifballWebApp.Server.Hosting;

/// <summary>What a backend process runs, from <c>App:Role</c>.</summary>
public enum AppRoleKind
{
    /// <summary>Everything, in one process: the default, and the only safe role for a single replica.</summary>
    All,
    /// <summary>The HTTP API and the SignalR hub; no Discord gateway and no background services. Any number of replicas.</summary>
    Api,
    /// <summary>The Discord gateway and every background service; health endpoints only. Exactly one.</summary>
    Worker,
}

/// <summary>
/// The process's role (<c>App:Role</c>: <c>all</c>, the default; <c>api</c>; <c>worker</c>). Scaled out, one worker
/// runs whatever must run once (the Discord gateway, the background services) and
/// any number of API processes serve the site, their SignalR hubs joined by a backplane (<c>ConnectionStrings:Redis</c>).
/// </summary>
public sealed record AppRole(AppRoleKind Kind)
{
    public const string ConfigurationKey = "App:Role";

    public static AppRole All { get; } = new(AppRoleKind.All);

    /// <summary>Serves the HTTP API and the SignalR hub.</summary>
    public bool ServesApi => Kind is AppRoleKind.All or AppRoleKind.Api;

    /// <summary>Runs the Discord gateway and the background services.</summary>
    public bool RunsWorker => Kind is AppRoleKind.All or AppRoleKind.Worker;

    /// <summary>
    /// The hosted services that must run in one process only, so only in <see cref="RunsWorker"/> roles
    /// (<see cref="AppRoleExtensions.ApplyAppRole"/>). The Discord gateway's (NetCord's) are never registered in the
    /// API role at all.
    /// </summary>
    public static IReadOnlyList<Type> WorkerHostedServices { get; } =
    [
        typeof(QueueBackgroundService),
        typeof(EventsBackgroundService),
        // Its claim query would let several dispatchers share the outbox, but one is enough: messages queued by
        // the API pods are committed to the database and the worker picks them up on its next poll
        // (Outbox:PollInterval, 5 s); only a commit in the worker's own process wakes it sooner.
        typeof(OutboxDispatcher),
        // Jobs are claimed in the database, so it would be safe on several pods, but an import is long and heavy: it
        // belongs with the worker. A job uploaded through an API pod is picked up on the worker's next poll
        // (HistoricalImport:Worker:PollInterval, 30 s): the upload's wake-up signal only reaches its own process.
        typeof(GrifballWebApp.Server.Historical.HistoricalImportWorker),
    ];

    /// <summary>
    /// Fails startup when this role means several backend pods (<c>api</c> or <c>worker</c>) and Halo Infinite is in
    /// <see cref="HaloInfiniteMode.Direct"/> mode: there the app's own Halo tokens are files in one pod's working
    /// directory, which the other pods can't read or refresh. Scaled out, the Halo gateway holds the tokens.
    /// </summary>
    public void EnsureSupports(HaloInfiniteSettings halo)
    {
        if (Kind is not AppRoleKind.All && halo.Mode == HaloInfiniteMode.Direct)
            throw new InvalidOperationException(
                $"{ConfigurationKey} '{Kind.ToString().ToLowerInvariant()}' runs the backend as several pods, which needs " +
                $"{HaloInfiniteSettings.ModeKey} Gateway: in Direct mode the Halo tokens are files in one pod's working " +
                $"directory. Set {HaloInfiniteSettings.ModeKey} to Gateway, or run a single pod with {ConfigurationKey} all.");
    }

    /// <summary>From <c>App:Role</c>; unset or blank is <see cref="AppRoleKind.All"/>. Anything else fails startup.</summary>
    public static AppRole FromConfiguration(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
            return All;
        if (Enum.TryParse<AppRoleKind>(value.Trim(), ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(value, out _))
            return new AppRole(kind);
        throw new InvalidOperationException($"{ConfigurationKey} must be all, api or worker, not '{value}'");
    }
}

public static class AppRoleExtensions
{
    /// <summary>
    /// Registers <paramref name="role"/> and, outside the worker roles, removes the hosted services only a worker runs
    /// (<see cref="AppRole.WorkerHostedServices"/>). Call after every other registration, just before Build.
    /// </summary>
    public static IServiceCollection ApplyAppRole(this IServiceCollection services, AppRole role)
    {
        services.AddSingleton(role);
        if (role.RunsWorker)
            return services;

        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) && IsWorkerOnly(d)).ToList())
            services.Remove(descriptor);
        return services;
    }

    private static bool IsWorkerOnly(ServiceDescriptor descriptor)
        => (descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType) is { } type
           && AppRole.WorkerHostedServices.Contains(type);
}
