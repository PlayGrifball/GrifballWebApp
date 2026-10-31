using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Matchmaking;

namespace GrifballWebApp.Server.Hosting;

/// <summary>What a backend process runs, from <c>App:Role</c>.</summary>
public enum AppRoleKind
{
    /// <summary>Everything, in one process: the default, and the only safe role for a single replica.</summary>
    All,
    /// <summary>The HTTP API and the SignalR hub; no Discord gateway and no background services. Any number of replicas.</summary>
    Api,
    /// <summary>The Discord gateway and every background service, the Halo Infinite sign-in's owner; health endpoints only. Exactly one.</summary>
    Worker,
}

/// <summary>
/// The process's role (<c>App:Role</c>: <c>all</c>, the default; <c>api</c>; <c>worker</c>). Scaled out, one worker
/// runs whatever must run once (the Discord gateway, the background services, the Halo Infinite token refresh) and
/// any number of API processes serve the site, their SignalR hubs joined by a backplane (<c>ConnectionStrings:Redis</c>).
/// </summary>
public sealed record AppRole(AppRoleKind Kind)
{
    public const string ConfigurationKey = "App:Role";

    public static AppRole All { get; } = new(AppRoleKind.All);

    /// <summary>Serves the HTTP API and the SignalR hub.</summary>
    public bool ServesApi => Kind is AppRoleKind.All or AppRoleKind.Api;

    /// <summary>Runs the Discord gateway, the background services and the Halo Infinite token refresh.</summary>
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
        typeof(HaloTokenRefreshService),
    ];

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
