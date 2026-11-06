using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Hosting;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrifballWebApp.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AppRoleTests
{
    private static AppRole Read(string? value)
        => AppRole.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AppRole.ConfigurationKey] = value }).Build());

    [TestCase(null, AppRoleKind.All)]
    [TestCase("", AppRoleKind.All)]
    [TestCase("  ", AppRoleKind.All)]
    [TestCase("all", AppRoleKind.All)]
    [TestCase("api", AppRoleKind.Api)]
    [TestCase("API", AppRoleKind.Api)]
    [TestCase(" worker ", AppRoleKind.Worker)]
    [TestCase("Discord", AppRoleKind.Discord)]
    public void FromConfiguration_ReadsTheRole(string? value, AppRoleKind expected)
    {
        Assert.That(Read(value).Kind, Is.EqualTo(expected));
    }

    [TestCase("both")]
    [TestCase("1")]
    [TestCase("api,worker")]
    public void FromConfiguration_RefusesAnythingElse(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Read(value));
        Assert.That(ex!.Message, Does.Contain("must be all, api, worker or discord"));
    }

    [TestCase(AppRoleKind.All, true, true, true)]
    [TestCase(AppRoleKind.Api, true, false, false)]
    [TestCase(AppRoleKind.Worker, false, true, false)]
    [TestCase(AppRoleKind.Discord, false, false, true)]
    public void WhatEachRoleRuns(AppRoleKind kind, bool servesApi, bool runsWorker, bool runsDiscord)
    {
        var role = new AppRole(kind);
        Assert.Multiple(() =>
        {
            Assert.That(role.ServesApi, Is.EqualTo(servesApi));
            Assert.That(role.RunsWorker, Is.EqualTo(runsWorker));
            Assert.That(role.RunsDiscord, Is.EqualTo(runsDiscord));
        });
    }

    [TestCase(AppRoleKind.All, true, true, true)]
    [TestCase(AppRoleKind.Api, false, false, false)]
    [TestCase(AppRoleKind.Worker, true, true, false)]
    [TestCase(AppRoleKind.Discord, true, false, true)]
    public void Runs_TheWorkersServicesOnTheWorkers_TheSingletonsOnTheDiscordPod_AndTheOutboxOnBoth(AppRoleKind kind, bool outbox, bool jobs, bool xuidLookups)
    {
        var role = new AppRole(kind);
        Assert.Multiple(() =>
        {
            Assert.That(role.Runs(typeof(OutboxDispatcher)), Is.EqualTo(outbox), "The outbox (claimed in the database)");
            Assert.That(role.Runs(typeof(Quartz.QuartzHostedService)), Is.EqualTo(jobs), "The jobs");
            Assert.That(role.Runs(typeof(GrifballWebApp.Server.Historical.HistoricalImportWorker)), Is.EqualTo(jobs), "The historical import");
            Assert.That(role.Runs(typeof(GrifballWebApp.Server.Historical.XuidLookupWorker)), Is.EqualTo(xuidLookups), "The XUID lookups (one per deployment)");
            Assert.That(role.Runs(typeof(OtherHostedService)), Is.True, "Any other, everywhere");
        });
    }

    private static HaloInfiniteSettings Halo(HaloInfiniteMode mode)
        => new(mode, new Uri("http://halo-gateway.test/"), null, HaloInfiniteSettings.DefaultGatewayAdminUrl);

    [TestCase(AppRoleKind.All, HaloInfiniteMode.Gateway)]
    [TestCase(AppRoleKind.All, HaloInfiniteMode.Direct)]
    [TestCase(AppRoleKind.Api, HaloInfiniteMode.Gateway)]
    [TestCase(AppRoleKind.Worker, HaloInfiniteMode.Gateway)]
    [TestCase(AppRoleKind.Discord, HaloInfiniteMode.Gateway)]
    public void EnsureSupports_AllowsOnePodOrTheGateway(AppRoleKind kind, HaloInfiniteMode mode)
    {
        Assert.DoesNotThrow(() => new AppRole(kind).EnsureSupports(Halo(mode)));
    }

    [TestCase(AppRoleKind.Api, "api")]
    [TestCase(AppRoleKind.Worker, "worker")]
    [TestCase(AppRoleKind.Discord, "discord")]
    public void EnsureSupports_RefusesSeveralPodsInDirectMode(AppRoleKind kind, string name)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new AppRole(kind).EnsureSupports(Halo(HaloInfiniteMode.Direct)));
        Assert.That(ex!.Message, Is.EqualTo(
            $"App:Role '{name}' runs the backend as several pods, which needs HaloInfinite:Mode Gateway: in Direct mode " +
            "the Halo tokens are files in one pod's working directory. Set HaloInfinite:Mode to Gateway, or run a single pod with App:Role all."));
    }

    private sealed class OtherHostedService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }

    private static IServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<OtherHostedService>();
        services.AddKeyedSingleton<IHostedService, OtherHostedService>("keyed");
        return services;
    }

    private static List<Type?> HostedTypes(IServiceCollection services)
        => services.Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.IsKeyedService ? d.KeyedImplementationType : d.ImplementationType).ToList();

    [Test]
    public void TheHistoricalImportWorker_RunsOnlyInTheWorkerRoles()
    {
        var services = new ServiceCollection();
        services.AddHostedService<GrifballWebApp.Server.Historical.HistoricalImportWorker>();
        services.AddHostedService<OtherHostedService>();
        Assert.That(AppRole.WorkerHostedServices, Does.Contain(typeof(GrifballWebApp.Server.Historical.HistoricalImportWorker)));
        Assert.That(HostedTypes(services.ApplyAppRole(new AppRole(AppRoleKind.Api))), Is.EqualTo(new[] { typeof(OtherHostedService) }));

        var worker = new ServiceCollection();
        worker.AddHostedService<GrifballWebApp.Server.Historical.HistoricalImportWorker>();
        Assert.That(HostedTypes(worker.ApplyAppRole(new AppRole(AppRoleKind.Worker))), Does.Contain(typeof(GrifballWebApp.Server.Historical.HistoricalImportWorker)));
    }

    [Test]
    [TestCase(AppRoleKind.Api, false)]
    [TestCase(AppRoleKind.Worker, false)]
    [TestCase(AppRoleKind.Discord, true)]
    [TestCase(AppRoleKind.All, true)]
    public void TheXuidLookupWorker_RunsOnlyInTheDiscordRoles(AppRoleKind kind, bool runs)
    {
        // Throttled in process and unclaimed: one per deployment, so not on the workers, which may be several
        Assert.That(AppRole.DiscordHostedServices, Does.Contain(typeof(GrifballWebApp.Server.Historical.XuidLookupWorker)));
        Assert.That(AppRole.WorkerHostedServices, Does.Not.Contain(typeof(GrifballWebApp.Server.Historical.XuidLookupWorker)));
        var services = new ServiceCollection();
        services.AddHostedService<GrifballWebApp.Server.Historical.XuidLookupWorker>();
        Assert.That(HostedTypes(services.ApplyAppRole(new AppRole(kind))).Contains(typeof(GrifballWebApp.Server.Historical.XuidLookupWorker)), Is.EqualTo(runs));
    }

    [TestCase(AppRoleKind.Api, false)]
    [TestCase(AppRoleKind.Discord, false)]
    [TestCase(AppRoleKind.Worker, true)]
    [TestCase(AppRoleKind.All, true)]
    public void TheJobScheduler_StartsOnlyInTheWorkerRoles_ButEveryRoleCanReachIt(AppRoleKind kind, bool starts)
    {
        var services = new ServiceCollection();
        services.AddJobs(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:GrifballWebApp"] = "Data Source=jobs.db",
        }).Build(), _ => { });
        services.ApplyAppRole(new AppRole(kind));

        var hosted = HostedTypes(services);
        Assert.Multiple(() =>
        {
            Assert.That(hosted.Contains(typeof(Quartz.QuartzHostedService)), Is.EqualTo(starts), "The scheduler");
            Assert.That(hosted.Contains(typeof(RecurringJobSeeder)), Is.EqualTo(starts), "The seeder");
            Assert.That(services.Any(d => d.ServiceType == typeof(Quartz.ISchedulerFactory)), Is.True, "The API pods reach the store through it");
        });
        if (starts)
            Assert.That(hosted.IndexOf(typeof(RecurringJobSeeder)), Is.LessThan(hosted.IndexOf(typeof(Quartz.QuartzHostedService))),
                "The triggers are made before the scheduler starts");
    }

    [Test]
    public void TheOutboxDispatcher_RunsOnTheWorkersAndTheDiscordPod_NotTheApi()
    {
        Assert.That(AppRole.WorkerHostedServices, Does.Contain(typeof(OutboxDispatcher)));
        Assert.That(AppRole.DiscordHostedServices, Does.Contain(typeof(OutboxDispatcher)));
        Assert.That(HostedTypes(Services().ApplyAppRole(new AppRole(AppRoleKind.Api))), Does.Not.Contain(typeof(OutboxDispatcher)));
        Assert.That(HostedTypes(Services().ApplyAppRole(new AppRole(AppRoleKind.Worker))), Does.Contain(typeof(OutboxDispatcher)));
        Assert.That(HostedTypes(Services().ApplyAppRole(new AppRole(AppRoleKind.Discord))), Does.Contain(typeof(OutboxDispatcher)));
    }

    [Test]
    public void ApplyAppRole_KeepsAHostedServiceMadeByAFactory()
    {
        var services = new ServiceCollection();
        services.AddHostedService(_ => new OtherHostedService());
        Assert.That(services.ApplyAppRole(new AppRole(AppRoleKind.Api)).Count(d => d.ServiceType == typeof(IHostedService)), Is.EqualTo(1));
    }

    [Test]
    public void ApplyAppRole_InTheApiRole_DropsOnlyTheWorkersHostedServices()
    {
        var services = Services().ApplyAppRole(new AppRole(AppRoleKind.Api));

        Assert.That(HostedTypes(services), Is.EqualTo(new[] { typeof(OtherHostedService), typeof(OtherHostedService) }));
        Assert.That(services.Single(d => d.ServiceType == typeof(AppRole)).ImplementationInstance, Is.EqualTo(new AppRole(AppRoleKind.Api)));
    }

    [TestCase(AppRoleKind.All)]
    [TestCase(AppRoleKind.Worker)]
    [TestCase(AppRoleKind.Discord)]
    public void ApplyAppRole_InTheWorkerRoles_KeepsThemAll(AppRoleKind kind)
    {
        var services = Services().ApplyAppRole(new AppRole(kind));

        Assert.That(HostedTypes(services), Has.Count.EqualTo(3));
    }
}
