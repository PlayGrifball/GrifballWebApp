using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Outbox;

namespace GrifballWebApp.Server.Hosting;

/// <summary>What a backend process runs, from <c>App:Role</c>.</summary>
public enum AppRoleKind
{
    /// <summary>Everything, in one process: the default, and the only safe role for a single replica.</summary>
    All,
    /// <summary>The HTTP API and the SignalR hub; no Discord gateway and no background services. Any number of replicas.</summary>
    Api,
    /// <summary>The jobs and the background services that claim their work in the database; health endpoints only. Any number of replicas.</summary>
    Worker,
    /// <summary>The Discord gateway and whatever else must run in one process only; health endpoints only. Exactly one.</summary>
    Discord,
}

/// <summary>
/// The process's role (<c>App:Role</c>: <c>all</c>, the default; <c>api</c>; <c>worker</c>; <c>discord</c>). Scaled
/// out, any number of API processes serve the site, their SignalR hubs joined by a backplane
/// (<c>ConnectionStrings:Redis</c>); any number of workers run the jobs (Quartz, clustered) and the background services
/// that share their work through the database; and one discord process runs what must run once: the Discord gateway (two
/// bots logged in would each handle every command and message) and the XUID lookups (which don't claim their placeholders).
/// </summary>
public sealed record AppRole(AppRoleKind Kind)
{
    public const string ConfigurationKey = "App:Role";

    public static AppRole All { get; } = new(AppRoleKind.All);

    /// <summary>Serves the HTTP API and the SignalR hub.</summary>
    public bool ServesApi => Kind is AppRoleKind.All or AppRoleKind.Api;

    /// <summary>Runs the jobs and the background services (<see cref="WorkerHostedServices"/>).</summary>
    public bool RunsWorker => Kind is AppRoleKind.All or AppRoleKind.Worker;

    /// <summary>Runs the Discord gateway and what must run once (<see cref="DiscordHostedServices"/>).</summary>
    public bool RunsDiscord => Kind is AppRoleKind.All or AppRoleKind.Discord;

    /// <summary>
    /// The hosted services of the worker roles (<see cref="RunsWorker"/>), safe on several pods: each claims its work in
    /// the database (<see cref="AppRoleExtensions.ApplyAppRole"/>).
    /// </summary>
    public static IReadOnlyList<Type> WorkerHostedServices { get; } =
    [
        // Its claim query lets several dispatchers share the outbox. Messages queued by the API pods are committed to the
        // database and a dispatcher picks them up on its next poll (Outbox:PollInterval, 5 s); a commit in a dispatcher's
        // own process wakes it sooner. It runs on the discord pod too, so what its commands queue goes at once.
        typeof(OutboxDispatcher),
        // Jobs are claimed in the database, so several workers share them. A job uploaded through an API pod is picked up
        // on a worker's next poll (HistoricalImport:Worker:PollInterval, 30 s): the upload's wake-up signal only reaches
        // its own process. Its Halo calls, like the pulls', take turns shared by every pod (HaloCallPacer, through Redis), so
        // several workers importing and pulling at once keep to the gateway key's quota together.
        typeof(GrifballWebApp.Server.Historical.HistoricalImportWorker),
        // Quartz's scheduler fires the jobs: clustered, each firing runs on one worker; an API pod still builds it, in
        // standby, to read and change the shared store. And the seeder that makes the recurring jobs' missing triggers
        // before it starts (TriggerConflict.Keep, decided under the store's lock, so workers starting at once agree).
        typeof(Quartz.QuartzHostedService),
        typeof(GrifballWebApp.Server.Jobs.RecurringJobSeeder),
    ];

    /// <summary>
    /// The hosted services of the discord roles (<see cref="RunsDiscord"/>), which must run in one process only. The
    /// Discord gateway's own (NetCord's) are only registered in those roles.
    /// </summary>
    public static IReadOnlyList<Type> DiscordHostedServices { get; } =
    [
        // It doesn't claim its placeholders: one per deployment, or the pods would look up the same gamertags. Its Halo
        // calls wait their turns on the pace every pod shares (HaloCallPacer, through Redis), as the pulls' do. Woken by an import on a
        // worker or a sysadmin on an API pod only through the database: within XuidLookup:PollInterval (1 minute).
        typeof(GrifballWebApp.Server.Historical.XuidLookupWorker),
        // See WorkerHostedServices: the commands and interactions queue messages here.
        typeof(OutboxDispatcher),
    ];

    /// <summary>
    /// Fails startup when this role means several backend pods (anything but <c>all</c>) and Halo Infinite is in
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
        // One name: Enum.TryParse would also take a number, or a list ('api,worker' is Api | Worker, which is Discord)
        if (!value.Contains(',') && Enum.TryParse<AppRoleKind>(value.Trim(), ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(value, out _))
            return new AppRole(kind);
        throw new InvalidOperationException($"{ConfigurationKey} must be all, api, worker or discord, not '{value}'");
    }

    /// <summary>Whether this role runs <paramref name="hostedService"/>: any not in either list runs everywhere.</summary>
    public bool Runs(Type hostedService)
    {
        var worker = WorkerHostedServices.Contains(hostedService);
        var discord = DiscordHostedServices.Contains(hostedService);
        return (!worker && !discord) || (worker && RunsWorker) || (discord && RunsDiscord);
    }
}

public static class AppRoleExtensions
{
    /// <summary>
    /// Registers <paramref name="role"/> and removes the hosted services it doesn't run (<see cref="AppRole.Runs"/>:
    /// <see cref="AppRole.WorkerHostedServices"/> outside the worker roles, <see cref="AppRole.DiscordHostedServices"/>
    /// outside the discord roles). Call after every other registration, just before Build.
    /// </summary>
    public static IServiceCollection ApplyAppRole(this IServiceCollection services, AppRole role)
    {
        services.AddSingleton(role);
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) && !RunsIn(d, role)).ToList())
            services.Remove(descriptor);
        return services;
    }

    private static bool RunsIn(ServiceDescriptor descriptor, AppRole role)
        => (descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType) is not { } type
           || role.Runs(type);
}
