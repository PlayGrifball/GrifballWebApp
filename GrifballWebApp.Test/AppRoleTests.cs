using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Hosting;
using GrifballWebApp.Server.Matchmaking;
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
        Assert.That(ex!.Message, Does.Contain("must be all, api or worker"));
    }

    [TestCase(AppRoleKind.All, true, true)]
    [TestCase(AppRoleKind.Api, true, false)]
    [TestCase(AppRoleKind.Worker, false, true)]
    public void WhatEachRoleRuns(AppRoleKind kind, bool servesApi, bool runsWorker)
    {
        var role = new AppRole(kind);
        Assert.Multiple(() =>
        {
            Assert.That(role.ServesApi, Is.EqualTo(servesApi));
            Assert.That(role.RunsWorker, Is.EqualTo(runsWorker));
        });
    }

    private sealed class OtherHostedService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }

    private static IServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddHostedService<QueueBackgroundService>();
        services.AddHostedService<EventsBackgroundService>();
        services.AddHostedService<HaloTokenRefreshService>();
        services.AddHostedService<OtherHostedService>();
        services.AddKeyedSingleton<IHostedService, OtherHostedService>("keyed");
        return services;
    }

    private static List<Type?> HostedTypes(IServiceCollection services)
        => services.Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.IsKeyedService ? d.KeyedImplementationType : d.ImplementationType).ToList();

    [Test]
    public void ApplyAppRole_InTheApiRole_DropsOnlyTheWorkersHostedServices()
    {
        var services = Services().ApplyAppRole(new AppRole(AppRoleKind.Api));

        Assert.That(HostedTypes(services), Is.EqualTo(new[] { typeof(OtherHostedService), typeof(OtherHostedService) }));
        Assert.That(services.Single(d => d.ServiceType == typeof(AppRole)).ImplementationInstance, Is.EqualTo(new AppRole(AppRoleKind.Api)));
    }

    [TestCase(AppRoleKind.All)]
    [TestCase(AppRoleKind.Worker)]
    public void ApplyAppRole_InTheWorkerRoles_KeepsThemAll(AppRoleKind kind)
    {
        var services = Services().ApplyAppRole(new AppRole(kind));

        Assert.That(HostedTypes(services), Has.Count.EqualTo(5));
    }
}
