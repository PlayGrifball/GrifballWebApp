using DiscordInterface.Generated;
using GrifballWebApp.Server.Excel;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Rest;
using System.Net;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Test;

/// <summary>
/// Runs the real <see cref="ServerProgram"/> startup (service registration, migrations, middleware, endpoint mapping)
/// against the shared test database server (SQL Server or PostgreSQL, <see cref="TestDatabase.Provider"/>), once in preview mode and once in normal mode.
/// Non-parallelizable because Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ProgramStartupTests
{
    // Shaped like a bot token (base64 snowflake . timestamp . hmac) so NetCord can parse it; never sent anywhere.
    private const string DummyBotToken = "MTIzNDU2Nzg5MDEyMzQ1Njc4.GAAAAA.dummy-token-that-is-never-sent-anywhere";

    private string _connectionString = null!;

    [SetUp]
    public void SetUp()
    {
        // Program.cs creates it (CreateDatabase).
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"StartupDb_{Guid.NewGuid():N}");
    }

    [TearDown]
    public async Task TearDown()
    {
        // The startup that fails stops before creating it.
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    private sealed class StartupFactory(Dictionary<string, string?> settings, bool removeDiscordHostedServices)
        : WebApplicationFactory<ServerProgram>
    {
        /// <summary>Every IHostedService registration Program.cs made, captured before any are removed.</summary>
        public List<ServiceDescriptor> HostedServices { get; } = [];

        /// <summary>Whether Program.cs registered NetCord's <see cref="GatewayClient"/>.</summary>
        public bool GatewayClientRegistered { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // UseSetting values reach WebApplication.CreateBuilder as command-line args, so Program.cs sees them
            // before it reads Preview:Enabled.
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);

            builder.ConfigureTestServices(services =>
            {
                HostedServices.AddRange(services.Where(d => d.ServiceType == typeof(IHostedService)));
                GatewayClientRegistered = services.Any(d => d.ServiceType == typeof(GatewayClient));

                if (removeDiscordHostedServices)
                {
                    // Keep the registrations Program.cs made but never start anything that talks to Discord.
                    foreach (var descriptor in services.Where(IsDiscordOrBackgroundService).ToList())
                        services.Remove(descriptor);
                }
            });
        }
    }

    private static Type? ImplementationOf(ServiceDescriptor descriptor)
        => descriptor.ImplementationType
           ?? descriptor.ImplementationInstance?.GetType()
           ?? descriptor.ImplementationFactory?.Method.DeclaringType;

    private static bool IsNetCord(ServiceDescriptor descriptor)
        => ImplementationOf(descriptor)?.Assembly.GetName().Name?.StartsWith("NetCord") is true;

    private static bool IsDiscordOrBackgroundService(ServiceDescriptor descriptor)
        => descriptor.ServiceType == typeof(IHostedService)
           && (IsNetCord(descriptor)
               || ImplementationOf(descriptor) == typeof(QueueBackgroundService)
               || ImplementationOf(descriptor) == typeof(EventsBackgroundService));

    private Dictionary<string, string?> CommonSettings() => new()
    {
        ["Database:Provider"] = TestDatabase.Provider.ToString(),
        ["ConnectionStrings:GrifballWebApp"] = _connectionString,
        ["ApplyMigrations"] = "true",
        ["CreateDatabase"] = "true",
    };

    private static void AssertControllersResolve(IServiceProvider services)
    {
        var feature = new ControllerFeature();
        services.GetRequiredService<ApplicationPartManager>().PopulateFeature(feature);
        Assert.That(feature.Controllers, Is.Not.Empty);

        using var scope = services.CreateScope();
        Assert.Multiple(() =>
        {
            foreach (var controller in feature.Controllers.Select(c => c.AsType()))
            {
                if (controller == typeof(ExcelController))
                {
                    // ExcelService loads a Google service-account key file in its constructor; no test (or preview)
                    // environment has one, so this controller fails per request rather than at startup.
                    Assert.That(() => ActivatorUtilities.CreateInstance(scope.ServiceProvider, controller),
                        Throws.Exception.With.Message.EqualTo("Missing GoogleSheets:Key"));
                    continue;
                }

                Assert.DoesNotThrow(() => ActivatorUtilities.CreateInstance(scope.ServiceProvider, controller),
                    $"{controller.FullName} could not be constructed from the container");
            }
        });
    }

    private static async Task AssertOk(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"GET {path}");
    }

    [Test]
    public async Task PreviewMode_Should_StartWithoutDiscordHaloOrGoogleConfig()
    {
        var settings = CommonSettings();
        settings[PreviewMode.ConfigurationKey] = "true";

        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: false);
        using var client = factory.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(factory.GatewayClientRegistered, Is.False, "Discord gateway must not be registered in preview mode");
            Assert.That(factory.HostedServices.Where(IsNetCord), Is.Empty, "NetCord hosted services must not be registered in preview mode");
            Assert.That(factory.HostedServices.Select(ImplementationOf),
                Has.None.EqualTo(typeof(QueueBackgroundService)).And.None.EqualTo(typeof(EventsBackgroundService)));
            Assert.That(factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Assembly.GetName().Name),
                Has.None.StartsWith("NetCord"));
        });

        var options = factory.Services.GetRequiredService<IOptions<GrifballWebApp.Server.DiscordOptions>>().Value;
        Assert.That(options.DisableGlobally, Is.True);

        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.That(schemes.Select(s => s.Name), Has.None.EqualTo("Discord"));

        AssertControllersResolve(factory.Services);

        var discord = factory.Services.GetRequiredService<IDiscordRestClient>();
        var ex = Assert.ThrowsAsync<PreviewDiscordDisabledException>(()
            => discord.SendMessageAsync(1234, new MessageProperties { Content = "hello" }));
        Assert.That(ex!.Message, Does.StartWith("Discord is disabled in preview mode"));

        await AssertOk(client, "/CommitHash");
        await AssertOk(client, "/CommitDate");
        await AssertOk(client, "/Home/CurrentAndFutureEvents");
        await AssertOk(client, "/Home/PastSeasons");
    }

    [Test]
    public async Task NormalMode_Should_RegisterDiscordGatewayOAuthAndBackgroundServices()
    {
        var settings = CommonSettings();
        settings["Discord:ClientId"] = "123456789012345678";
        settings["Discord:ClientSecret"] = "dummy-client-secret";
        settings["Discord:Token"] = DummyBotToken;
        settings["Discord:DraftChannel"] = "1";
        settings["Discord:DisableGlobally"] = "true";

        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: true);
        using var client = factory.CreateClient();

        var hostedTypes = factory.HostedServices.Select(ImplementationOf).ToList();
        TestContext.Out.WriteLine("Hosted services: " + string.Join(", ", hostedTypes.Select(t => t?.FullName)));

        Assert.Multiple(() =>
        {
            Assert.That(factory.GatewayClientRegistered, Is.True, "Discord gateway must be registered in normal mode");
            Assert.That(factory.HostedServices.Where(IsNetCord), Is.Not.Empty, "NetCord hosted services must be registered in normal mode");
            Assert.That(hostedTypes, Does.Contain(typeof(QueueBackgroundService)));
            Assert.That(hostedTypes, Does.Contain(typeof(EventsBackgroundService)));
            // They were removed before start, so nothing connected to Discord.
            Assert.That(factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Assembly.GetName().Name),
                Has.None.StartsWith("NetCord"));
        });

        var options = factory.Services.GetRequiredService<IOptions<GrifballWebApp.Server.DiscordOptions>>().Value;
        Assert.That(options.Token, Is.EqualTo(DummyBotToken));

        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.That(schemes.Select(s => s.Name), Does.Contain("Discord"));

        // Normal mode gets its RestClient from the gateway registration, not the preview stand-in.
        var rest = factory.Services.GetRequiredService<RestClient>();
        Assert.That(rest.Token, Is.Not.Null);

        AssertControllersResolve(factory.Services);

        await AssertOk(client, "/CommitHash");
        await AssertOk(client, "/Home/CurrentAndFutureEvents");
    }

    [Test]
    public async Task PreviewModeWithBotToken_Should_RunDiscordAsInNormalMode()
    {
        // A preview holding a preview slot: that slot's bot and server, still no Halo Infinite or Google.
        var settings = CommonSettings();
        settings[PreviewMode.ConfigurationKey] = "true";
        settings["Discord:ClientId"] = "123456789012345678";
        settings["Discord:ClientSecret"] = "dummy-client-secret";
        settings["Discord:Token"] = DummyBotToken;
        settings["Discord:DraftChannel"] = "1";
        settings["Discord:DisableGlobally"] = "false";

        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: true);
        using var client = factory.CreateClient();

        var hostedTypes = factory.HostedServices.Select(ImplementationOf).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(factory.GatewayClientRegistered, Is.True, "Discord gateway must be registered in a preview with a bot token");
            Assert.That(factory.HostedServices.Where(IsNetCord), Is.Not.Empty);
            Assert.That(hostedTypes, Does.Contain(typeof(QueueBackgroundService)));
            Assert.That(hostedTypes, Does.Contain(typeof(EventsBackgroundService)));
        });

        // Not the preview stand-in: options are validated as in normal mode and DisableGlobally isn't forced.
        var options = factory.Services.GetRequiredService<IOptions<GrifballWebApp.Server.DiscordOptions>>().Value;
        Assert.That(options.DisableGlobally, Is.False);
        Assert.That(factory.Services.GetRequiredService<RestClient>().Token, Is.Not.Null);

        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.That(schemes.Select(s => s.Name), Does.Contain("Discord"));

        var checks = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name);
        Assert.That(checks, Does.Contain("discord").And.Not.Contain("halo_infinite"));

        AssertControllersResolve(factory.Services);

        await AssertOk(client, "/CommitHash");
        await AssertOk(client, "/Home/CurrentAndFutureEvents");
    }

    [Test]
    public void PreviewModeWithBotToken_Should_FailStartup_When_DiscordSettingsAreMissing()
    {
        // A bot token turns Discord on in a preview, so the rest of the Discord settings are required as in normal mode.
        var settings = CommonSettings();
        settings[PreviewMode.ConfigurationKey] = "true";
        settings["Discord:Token"] = DummyBotToken;
        var args = settings
            .Select(kv => $"--{kv.Key}={kv.Value}")
            .Append("--urls=http://127.0.0.1:0")
            .ToArray();

        var ex = Assert.ThrowsAsync<ArgumentException>(() => ServerProgram.Run(args));
        Assert.That(ex!.Message, Is.EqualTo("ClientId is required, ClientSecret is required, DraftChannel is required"));
    }

    [Test]
    public void NormalMode_Should_FailStartup_When_DiscordSettingsAreMissing()
    {
        // Preview off and only a bot token (NetCord needs one to build the gateway client): DiscordOptions validation
        // must stop the host before it starts serving.
        var settings = CommonSettings();
        settings["Discord:Token"] = DummyBotToken;
        var args = settings
            .Select(kv => $"--{kv.Key}={kv.Value}")
            .Append("--urls=http://127.0.0.1:0")
            .ToArray();

        var ex = Assert.ThrowsAsync<ArgumentException>(() => ServerProgram.Run(args));
        Assert.That(ex!.Message, Is.EqualTo("ClientId is required, ClientSecret is required, DraftChannel is required"));
    }
}
