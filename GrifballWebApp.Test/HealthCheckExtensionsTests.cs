using GrifballWebApp.Database;
using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;

namespace GrifballWebApp.Test;

[TestFixture]
public class HealthCheckExtensionsTests
{
    // Refused at once: no SQL Server listens on port 9.
    private const string UnreachableDatabase = "Server=127.0.0.1,9;Database=nope;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True";

    private static string ReachableDatabase()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(SetUpFixture.MsSqlContainer.GetConnectionString())
        {
            InitialCatalog = "master",
        };
        return builder.ConnectionString;
    }

    private static async Task<WebApplication> StartApp(string connectionString, bool discordConnected, int? maxAllocatedMegabytes = null,
        IDictionary<string, string?>? settings = null, int haloStatusCode = 200)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (maxAllocatedMegabytes is { } max)
            builder.Configuration["HealthChecks:MaxAllocatedMegabytes"] = max.ToString();
        if (settings is not null)
            builder.Configuration.AddInMemoryCollection(settings);

        builder.Services.AddDbContext<GrifballContext>(options => options.UseSqlServer(connectionString));
        builder.Services.AddSingleton(new DiscordGatewayHealthCheck(discordConnected));
        builder.Services.AddSingleton(HaloInfiniteHealthCheckTests.Answering(haloStatusCode));
        builder.Services.AddAppHealthChecks(builder.Configuration);

        var app = builder.Build();
        app.MapAppHealthChecks();
        await app.StartAsync();
        return app;
    }

    private static async Task<(int Status, string Body)> Get(WebApplication app, string path)
    {
        var context = await app.GetTestServer().SendAsync(c => c.Request.Path = path);
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    [Test]
    public async Task AllHealthy()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: true);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Healthy")));
        });
    }

    [Test]
    public async Task DatabaseDown_FailsReadiness_ButNotLiveness()
    {
        await using var app = await StartApp(UnreachableDatabase, discordConnected: true);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((503, "Unhealthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((503, "Unhealthy")));
        });
    }

    [Test]
    public async Task DiscordDownOrHighMemory_OnlyDegrades_AndNeverGatesReadiness()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: false, maxAllocatedMegabytes: 1);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Degraded")));
        });
    }

    [Test]
    public async Task HaloDown_OnlyDegrades_AndNeverGatesReadiness()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: true, haloStatusCode: 503);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Degraded")));
        });
    }

    [Test]
    public void AddAppHealthChecks_RegistersChecksTagsAndThePublisher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppHealthChecks(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToDictionary(r => r.Name);

        Assert.Multiple(() =>
        {
            Assert.That(registrations.Keys, Is.SupersetOf(new[] { "database", "discord", "halo_infinite", "memory" }));
            Assert.That(registrations["database"].Tags, Does.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations["database"].FailureStatus, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(registrations["discord"].Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations["discord"].FailureStatus, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(registrations["memory"].Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations.Values.Count(r => r.Tags.Contains(HealthCheckExtensions.ReadyTag)), Is.EqualTo(2), "database and application lifecycle");
            Assert.That(provider.GetServices<IHealthCheckPublisher>(), Is.Not.Empty);
            Assert.That(provider.GetRequiredService<HealthCheckPaths>(), Is.EqualTo(HealthCheckPaths.Default));
        });
    }

    [Test]
    public void AddAppHealthChecks_WithoutExternalServices_LeavesOutDiscordAndHalo()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppHealthChecks(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), externalServices: false);
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name);

        Assert.Multiple(() =>
        {
            // Preview mode registers no Discord gateway client, which the Discord check (a hosted service) needs.
            Assert.That(registrations, Is.SupersetOf(new[] { "database", "memory" }));
            Assert.That(registrations, Does.Not.Contain("discord"));
            Assert.That(registrations, Does.Not.Contain("halo_infinite"));
            Assert.That(services.Any(d => d.ServiceType == typeof(DiscordGatewayHealthCheck)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(HaloInfiniteHealthCheck)), Is.False);
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(HealthCheckPaths.Default.IsHealthPath(new PathString(path)), Is.EqualTo(expected));
            Assert.That(HealthCheckExtensions.IsHealthPath(context), Is.EqualTo(expected), "no services: the defaults");
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.EqualTo(HealthCheckPaths.Default));
            Assert.That(empty.Health.Value, Is.EqualTo("/health"));
            Assert.That(empty.Live.Value, Is.EqualTo("/health/live"));
            Assert.That(empty.Ready.Value, Is.EqualTo("/health/ready"));
            Assert.That(configured, Is.EqualTo(new HealthCheckPaths("/healthz", "/livez", "/health/ready")), "blank keeps the default");
            Assert.That(configured.IsHealthPath("/LIVEZ"), Is.True);
            Assert.That(configured.IsHealthPath("/health/ready"), Is.True);
            Assert.That(configured.IsHealthPath("/health"), Is.False);
        });
    }

    [Test]
    public async Task ConfiguredPaths_AreMapped_AndRecognised()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: true, settings: new Dictionary<string, string?>
        {
            ["HealthChecks:Path"] = "/healthz",
            ["HealthChecks:LivePath"] = "/livez",
            ["HealthChecks:ReadyPath"] = "/readyz",
        });

        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Path = "/readyz";

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/livez"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/readyz"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/healthz"), Is.EqualTo((200, "Healthy")));
            Assert.That((await Get(app, "/health")).Status, Is.EqualTo(404));
            Assert.That(HealthCheckExtensions.IsHealthPath(context), Is.True);
        });
    }

    [Test]
    public async Task MapAppHealthChecks_WithoutAddAppHealthChecks_ReadsTheConfiguration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["HealthChecks:LivePath"] = "/livez";
        builder.Services.AddHealthChecks();
        await using var app = builder.Build();
        app.MapAppHealthChecks();
        await app.StartAsync();

        Assert.That(await Get(app, "/livez"), Is.EqualTo((200, "Healthy")));
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

        Assert.DoesNotThrowAsync(() => check.StartAsync(CancellationToken.None));
        Assert.DoesNotThrowAsync(() => check.StopAsync(CancellationToken.None));
    }
}
