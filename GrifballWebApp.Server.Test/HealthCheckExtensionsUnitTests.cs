using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;

namespace GrifballWebApp.Server.Test;

/// <summary>HealthCheckExtensionsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class HealthCheckExtensionsUnitTests
{
    private static readonly string[] AllChecks = ["database", "discord", "halo_infinite", "memory"];
    private static readonly string[] PreviewWithDiscordChecks = ["database", "memory", "discord"];
    private static readonly string[] CoreChecks = ["database", "memory"];

    [Test]
    public void AddAppHealthChecks_RegistersChecksTagsAndThePublisher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppHealthChecks(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToDictionary(r => r.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registrations.Keys, Is.SupersetOf(AllChecks));
            Assert.That(registrations["database"].Tags, Does.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations["database"].FailureStatus, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(registrations["discord"].Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations["discord"].FailureStatus, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(registrations["memory"].Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations.Values.Count(r => r.Tags.Contains(HealthCheckExtensions.ReadyTag)), Is.EqualTo(2), "database and application lifecycle");
            Assert.That(provider.GetServices<IHealthCheckPublisher>(), Is.Not.Empty);
            Assert.That(provider.GetRequiredService<HealthCheckPaths>(), Is.EqualTo(HealthCheckPaths.Default));
        }
    }

    [Test]
    public void AddAppHealthChecks_PreviewWithDiscord_KeepsDiscord_LeavesOutHalo()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppHealthChecks(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), discord: true, haloInfinite: false);
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registrations, Is.SupersetOf(PreviewWithDiscordChecks));
            Assert.That(registrations, Does.Not.Contain("halo_infinite"));
            Assert.That(services.Any(d => d.ServiceType == typeof(DiscordGatewayHealthCheck)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(HaloInfiniteHealthCheck)), Is.False);
        }
    }

    [Test]
    public void AddAppHealthChecks_WithoutExternalServices_LeavesOutDiscordAndHalo()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppHealthChecks(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), discord: false, haloInfinite: false);
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name);

        using (Assert.EnterMultipleScope())
        {
            // Preview mode registers no Discord gateway client, which the Discord check (a hosted service) needs.
            Assert.That(registrations, Is.SupersetOf(CoreChecks));
            Assert.That(registrations, Does.Not.Contain("discord"));
            Assert.That(registrations, Does.Not.Contain("halo_infinite"));
            Assert.That(services.Any(d => d.ServiceType == typeof(DiscordGatewayHealthCheck)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(HaloInfiniteHealthCheck)), Is.False);
        }
    }

    [TestCase("/health", true)]
    [TestCase("/health/ready", true)]
    [TestCase("/HEALTH/live", true)]
    [TestCase("/healthz", false)]
    [TestCase("/api/health", false)]
    public void IsHealthPath_Defaults(string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(HealthCheckPaths.Default.IsHealthPath(new PathString(path)), Is.EqualTo(expected));
            Assert.That(HealthCheckExtensions.IsHealthPath(context), Is.EqualTo(expected), "no services: the defaults");
        }
    }

    [Test]
    public void HealthCheckPaths_FromConfiguration()
    {
        var empty = HealthCheckPaths.FromConfiguration(new ConfigurationBuilder().Build());
        var configured = HealthCheckPaths.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:Path"] = "healthz/",
            ["HealthChecks:LivePath"] = " /livez ",
            ["HealthChecks:ReadyPath"] = "  ",
        }).Build());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty, Is.EqualTo(HealthCheckPaths.Default));
            Assert.That(empty.Health.Value, Is.EqualTo("/health"));
            Assert.That(empty.Live.Value, Is.EqualTo("/health/live"));
            Assert.That(empty.Ready.Value, Is.EqualTo("/health/ready"));
            Assert.That(configured, Is.EqualTo(new HealthCheckPaths("/healthz", "/livez", "/health/ready")), "blank keeps the default");
            Assert.That(configured.IsHealthPath("/LIVEZ"), Is.True);
            Assert.That(configured.IsHealthPath("/health/ready"), Is.True);
            Assert.That(configured.IsHealthPath("/health"), Is.False);
        }
    }

    // Built at runtime so secret scanning does not read it as a real token: base64("123456789012345678").
    private static string FakeToken() => string.Join(".", "MTIzNDU2Nzg5MDEyMzQ1Njc4", "GAAAAA", new string('A', 38));

    [Test]
    public async Task DiscordCheck_FollowsTheGatewayConnection()
    {
        using var client = new GatewayClient(new BotToken(FakeToken()));
        var check = new DiscordGatewayHealthCheck(client);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("discord", check, HealthStatus.Degraded, null),
        };

        Assert.That((await check.CheckHealthAsync(context)).Status, Is.EqualTo(HealthStatus.Degraded));

        await check.SetConnected(true);
        Assert.That((await check.CheckHealthAsync(context)).Status, Is.EqualTo(HealthStatus.Healthy));

        await check.SetConnected(false);
        Assert.That((await check.CheckHealthAsync(context)).Status, Is.EqualTo(HealthStatus.Degraded));

        await Assert.DoesNotThrowAsync(() => check.StartAsync(CancellationToken.None));
        await Assert.DoesNotThrowAsync(() => check.StopAsync(CancellationToken.None));
    }
}
