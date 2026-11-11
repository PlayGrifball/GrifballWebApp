using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Gateway;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
public class HealthCheckExtensionsTests
{
    // Refused at once: nothing listens on port 9.
    private static readonly string UnreachableDatabase = TestDatabase.UnreachableConnectionString();

    // A migrated database, shared by the fixture's tests: the Discord outbox check reads its table.
    private static readonly Lazy<Task<string>> MigratedDatabase = new(async () =>
    {
        var name = $"TestDb_Health_{Guid.NewGuid():N}";
        await TestDatabase.CreateMigratedDatabase(SetUpFixture.DatabaseContainer, name);
        return TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, name);
    });

    private static string ReachableDatabase() => MigratedDatabase.Value.GetAwaiter().GetResult();

    // Far above what the test process allocates, so the memory check (Degraded over its limit) can't decide a test that
    // expects Healthy: at the default limit, the whole suite's allocations sometimes tipped it.
    private const int NoMemoryLimitMegabytes = 64 * 1024;

    private static async Task<WebApplication> StartApp(string connectionString, bool discordConnected, int? maxAllocatedMegabytes = null,
        IDictionary<string, string?>? settings = null, int haloStatusCode = 200)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (maxAllocatedMegabytes is { } max)
            builder.Configuration["HealthChecks:MaxAllocatedMegabytes"] = max.ToString();
        if (settings is not null)
            builder.Configuration.AddInMemoryCollection(settings);

        builder.Services.AddDbContext<GrifballContext>(options => options.UseGrifballDatabase(TestDatabase.Provider, connectionString));
        builder.Services.AddSingleton(new DiscordGatewayHealthCheck(discordConnected));
        builder.Services.AddSingleton<IHaloInfiniteClient>(HaloInfiniteHealthCheckTestData.Answering(haloStatusCode));
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
        await using var app = await StartApp(ReachableDatabase(), discordConnected: true, maxAllocatedMegabytes: NoMemoryLimitMegabytes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Healthy")));
        }
    }

    [Test]
    public async Task DatabaseDown_FailsReadiness_ButNotLiveness()
    {
        await using var app = await StartApp(UnreachableDatabase, discordConnected: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((503, "Unhealthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((503, "Unhealthy")));
        }
    }

    [Test]
    public async Task DiscordDownOrHighMemory_OnlyDegrades_AndNeverGatesReadiness()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: false, maxAllocatedMegabytes: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Degraded")));
        }
    }

    [Test]
    public async Task HaloDown_OnlyDegrades_AndNeverGatesReadiness()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: true, haloStatusCode: 503);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Degraded")));
        }
    }

    [Test]
    public async Task ConfiguredPaths_AreMapped_AndRecognised()
    {
        await using var app = await StartApp(ReachableDatabase(), discordConnected: true, maxAllocatedMegabytes: NoMemoryLimitMegabytes, settings: new Dictionary<string, string?>
        {
            ["HealthChecks:Path"] = "/healthz",
            ["HealthChecks:LivePath"] = "/livez",
            ["HealthChecks:ReadyPath"] = "/readyz",
        });

        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Path = "/readyz";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Get(app, "/livez"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/readyz"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/healthz"), Is.EqualTo((200, "Healthy")));
            Assert.That((await Get(app, "/health")).Status, Is.EqualTo(404));
            Assert.That(HealthCheckExtensions.IsHealthPath(context), Is.True);
        }
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
}
