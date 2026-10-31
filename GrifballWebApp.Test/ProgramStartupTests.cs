using DiscordInterface.Generated;
using GrifballWebApp.Server.Excel;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Hosting;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Preview;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Server.Notifications;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
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

    private sealed class StartupFactory(Dictionary<string, string?> settings, bool removeDiscordHostedServices,
        Action<IServiceCollection>? configureServices = null)
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

                configureServices?.Invoke(services);
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

    /// <summary>Normal mode, with Discord configured but never contacted (DisableGlobally, hosted services removed).</summary>
    private Dictionary<string, string?> NormalModeSettings()
    {
        var settings = CommonSettings();
        settings["Discord:ClientId"] = "123456789012345678";
        settings["Discord:ClientSecret"] = "dummy-client-secret";
        settings["Discord:Token"] = DummyBotToken;
        settings["Discord:DraftChannel"] = "1";
        // QueueService (UpdateDisplayHandler's) refuses to be built without it.
        settings["Discord:QueueChannel"] = "2";
        settings["Discord:DisableGlobally"] = "true";
        return settings;
    }

    /// <summary>Every concrete notification handler in the server, with the handler interface it implements.</summary>
    private static List<(Type Handler, Type Service)> NotificationHandlers()
        => typeof(ServerProgram).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                .Select(i => (t, i)))
            .ToList();

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

    private const string SmokePassword = "Smoke-Test-Password-1";

    private static async Task AssertLogin(HttpClient client, string userName, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync("/Identity/Login", new { Username = userName, Password = SmokePassword });
        Assert.That(response.StatusCode, Is.EqualTo(expected), $"login as {userName}");
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
        settings[PreviewSmokeUsers.PasswordKey] = SmokePassword;

        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: false);
        using var client = factory.CreateClient();

        await Assert.MultipleAsync(async () =>
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
        var ex = await Assert.ThrowsAsync<PreviewDiscordDisabledException>(()
            => discord.SendMessageAsync(1234, new MessageProperties { Content = "hello" }));
        Assert.That(ex!.Message, Does.StartWith("Discord is disabled in preview mode"));

        await AssertOk(client, "/CommitHash");
        await AssertOk(client, "/CommitDate");
        await AssertOk(client, "/Home/CurrentAndFutureEvents");
        await AssertOk(client, "/Home/PastSeasons");

        // The smoke-test accounts were made at startup and can sign in.
        foreach (var userName in PreviewSmokeUsers.UserNames)
            await AssertLogin(client, userName, HttpStatusCode.OK);
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
        // Ignored outside preview mode.
        settings[PreviewSmokeUsers.PasswordKey] = SmokePassword;

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
            // App:Role unset: all, today's single process, which also owns the Halo Infinite sign-in.
            Assert.That(hostedTypes, Does.Contain(typeof(HaloTokenRefreshService)));
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
        await AssertLogin(client, PreviewSmokeUsers.UserNames[0], HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task NormalMode_DiscordCallback_Should_SeeTheForwardedSchemeAndHost()
    {
        // Behind the frontend: the backend is reached over http from the cluster, the visitor came over https.
        // The callback builds its redirect_uri from the request, so it must see https like the sign-in did.
        var settings = CommonSettings();
        settings["Discord:ClientId"] = "123456789012345678";
        settings["Discord:ClientSecret"] = "dummy-client-secret";
        settings["Discord:Token"] = DummyBotToken;
        settings["Discord:DraftChannel"] = "1";
        settings["Discord:DisableGlobally"] = "true";
        settings["ForwardedHeaders:ForwardedForHeaderName"] = "X-Real-IP";
        settings["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16";

        string? seen = null;
        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: true, services =>
        {
            // The test server has no client address: give it the frontend pod's, a known proxy.
            services.AddSingleton<IStartupFilter>(new FromAddress(IPAddress.Parse("10.42.3.239")));
            // The bogus state fails the callback; note what the handler saw instead of throwing.
            services.PostConfigure<AspNet.Security.OAuth.Discord.DiscordAuthenticationOptions>("Discord", options =>
                options.Events.OnRemoteFailure = context =>
                {
                    seen = $"{context.Request.Scheme}://{context.Request.Host}";
                    context.HandleResponse();
                    return Task.CompletedTask;
                });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/signin-discord?code=some-code&state=not-a-state");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "grifball.example");
        request.Headers.Add("X-Real-IP", "203.0.113.9");
        using var response = await client.SendAsync(request);

        Assert.That(seen, Is.EqualTo("https://grifball.example"));
    }

    private sealed class FromAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    [Test]
    public async Task NormalMode_Should_RegisterEveryNotificationHandler()
    {
        // Program.cs registers the handlers by scanning its assembly (AddNotifications); every other test replaces
        // IPublisher with a substitute, so this is what checks they are all found and can be built from the container.
        await using var factory = new StartupFactory(NormalModeSettings(), removeDiscordHostedServices: true);
        using var client = factory.CreateClient();

        var handlers = NotificationHandlers();
        Assert.That(handlers.Select(h => h.Handler), Does.Contain(typeof(LockChangeHandler)).And.Contain(typeof(UpdateDisplayHandler)));

        using var scope = factory.Services.CreateScope();
        Assert.Multiple(() =>
        {
            foreach (var (handler, service) in handlers)
            {
                var resolved = scope.ServiceProvider.GetServices(service).Select(h => h!.GetType());
                Assert.That(resolved, Does.Contain(handler),
                    $"{handler.Name} is not registered for {service.GenericTypeArguments[0].Name}");
            }
        });
    }

    /// <summary>Every NetCord module and autocomplete provider in the server, which NetCord builds per interaction from the container.</summary>
    private static List<Type> DiscordModules()
        => typeof(ServerProgram).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(t => IsSubclassOfGeneric(t, typeof(ComponentInteractionModule<>))
                || IsSubclassOfGeneric(t, typeof(ApplicationCommandModule<>))
                || t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutocompleteProvider<>)))
            .ToList();

    private static bool IsSubclassOfGeneric(Type type, Type genericBase)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == genericBase)
                return true;
        return false;
    }

    [Test]
    public async Task NormalMode_Should_ConstructEveryDiscordModule()
    {
        // NetCord builds each module from the interaction's DI scope, so a dependency that isn't registered only fails when
        // someone uses the command or clicks the button. The unit tests build the handlers by hand and can't catch it.
        var settings = NormalModeSettings();
        // UrlService (the signup button's) refuses to be built without it.
        settings["BaseUrl"] = "https://grif.test";
        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: true);
        using var client = factory.CreateClient();

        var modules = DiscordModules();
        Assert.That(modules.Select(m => m.FullName), Is.EquivalentTo(new[]
        {
            "GrifballWebApp.Server.DiscordCommands",
            "GrifballWebApp.Server.GamertagAutocomplete",
            "GrifballWebApp.Server.Matchmaking.ButtonInteractions",
            "GrifballWebApp.Server.Matchmaking.ModalInteractions",
            "GrifballWebApp.Server.Matchmaking.StringMenuInteractions",
            "GrifballWebApp.Server.Signups.ButtonInteractions",
            "GrifballWebApp.Server.Teams.StringMenuInteractions",
        }));

        using var scope = factory.Services.CreateScope();
        Assert.Multiple(() =>
        {
            foreach (var module in modules)
                Assert.DoesNotThrow(() => ActivatorUtilities.CreateInstance(scope.ServiceProvider, module),
                    $"{module.FullName} could not be constructed from the container");
        });
    }

    [Test]
    public async Task NormalMode_Should_AuditScopedAndFactoryContexts_WithOneInterceptorEach()
    {
        // The scoped context gets its AuditInterceptor from DI, the factory's contexts from GrifballContextFactory: neither may get both
        await using var factory = new StartupFactory(NormalModeSettings(), removeDiscordHostedServices: true);
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<GrifballWebApp.Database.GrifballContext>();
        using var fromFactory = factory.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<GrifballWebApp.Database.GrifballContext>>().CreateDbContext();

        Assert.Multiple(() =>
        {
            Assert.That(scoped.Interceptors().OfType<GrifballWebApp.Database.Interceptors.AuditInterceptor>().Count(), Is.EqualTo(1));
            Assert.That(fromFactory.Interceptors().OfType<GrifballWebApp.Database.Interceptors.AuditInterceptor>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NormalMode_Should_PublishNotifications_ToTheTeamsHub()
    {
        // The real IPublisher from Program.cs's registration: LockChanged reaches LockChangeHandler, which tells every
        // other connection to lock the captains (and LockChangeDiscordHandler, with Discord disabled, does nothing).
        var others = Substitute.For<ITeamsHubClient>();
        var clients = Substitute.For<IHubClients<ITeamsHubClient>>();
        clients.AllExcept(Arg.Any<IReadOnlyList<string>>()).Returns(others);
        var hub = Substitute.For<IHubContext<TeamsHub, ITeamsHubClient>>();
        hub.Clients.Returns(clients);

        await using var factory = new StartupFactory(NormalModeSettings(), removeDiscordHostedServices: true,
            services => services.AddSingleton(hub));
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(new LockChanged(true, 7, "conn-1"));
        await publisher.Publish(new LockChanged(false, 8, "conn-2"));

        await others.Received(1).LockCaptains(7);
        await others.Received(1).UnlockCaptains(8);
        clients.Received(1).AllExcept(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "conn-1" })));
        clients.Received(1).AllExcept(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "conn-2" })));
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
    public async Task PreviewModeWithBotToken_Should_FailStartup_When_DiscordSettingsAreMissing()
    {
        // A bot token turns Discord on in a preview, so the rest of the Discord settings are required as in normal mode.
        var settings = CommonSettings();
        settings[PreviewMode.ConfigurationKey] = "true";
        settings["Discord:Token"] = DummyBotToken;
        var args = settings
            .Select(kv => $"--{kv.Key}={kv.Value}")
            .Append("--urls=http://127.0.0.1:0")
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => ServerProgram.Run(args));
        Assert.That(ex!.Message, Is.EqualTo("ClientId is required, ClientSecret is required, DraftChannel is required"));
    }

    [Test]
    public async Task NormalMode_Should_FailStartup_When_DiscordSettingsAreMissing()
    {
        // Preview off and only a bot token (NetCord needs one to build the gateway client): DiscordOptions validation
        // must stop the host before it starts serving.
        var settings = CommonSettings();
        settings["Discord:Token"] = DummyBotToken;
        var args = settings
            .Select(kv => $"--{kv.Key}={kv.Value}")
            .Append("--urls=http://127.0.0.1:0")
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => ServerProgram.Run(args));
        Assert.That(ex!.Message, Is.EqualTo("ClientId is required, ClientSecret is required, DraftChannel is required"));
    }

    [Test]
    public async Task ApiRole_Should_ServeTheApiAndHub_WithoutTheGatewayOrBackgroundServices()
    {
        var settings = NormalModeSettings();
        settings[AppRole.ConfigurationKey] = "api";
        // Scaled out, with the backplane (nothing listens there: it connects in the background).
        settings["ConnectionStrings:Redis"] = "127.0.0.1:9";

        // Nothing removed: the API role must not start anything that talks to Discord on its own.
        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: false);
        using var client = factory.CreateClient();

        var hostedTypes = factory.HostedServices.Select(ImplementationOf).ToList();
        TestContext.Out.WriteLine("Hosted services: " + string.Join(", ", hostedTypes.Select(t => t?.FullName)));
        Assert.Multiple(() =>
        {
            Assert.That(factory.Services.GetRequiredService<AppRole>().Kind, Is.EqualTo(AppRoleKind.Api));
            Assert.That(factory.GatewayClientRegistered, Is.False, "No Discord gateway in the API role");
            Assert.That(factory.HostedServices.Where(IsNetCord), Is.Empty);
            foreach (var workerOnly in AppRole.WorkerHostedServices)
                Assert.That(hostedTypes, Does.Not.Contain(workerOnly));
            Assert.That(factory.Services.GetRequiredService<HaloTokenOwnership>().OwnsRefresh, Is.False);
            Assert.That(factory.Services.GetRequiredService<HubLifetimeManager<TeamsHub>>(),
                Is.InstanceOf<Microsoft.AspNetCore.SignalR.StackExchangeRedis.RedisHubLifetimeManager<TeamsHub>>());
        });

        // It still sends Discord messages, with the bot's token, and signs people in with Discord.
        Assert.That(factory.Services.GetRequiredService<RestClient>().Token, Is.Not.Null);
        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.That(schemes.Select(s => s.Name), Does.Contain("Discord"));
        var checks = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name);
        Assert.That(checks, Does.Not.Contain("discord").And.Contain("halo_infinite"));

        AssertControllersResolve(factory.Services);
        await AssertOk(client, "/CommitHash");
        await AssertOk(client, "/Home/PastSeasons");
        await AssertOk(client, "/health/live");
        using var hub = await client.PostAsync("/TeamsHub/negotiate?negotiateVersion=1", null);
        Assert.That(hub.StatusCode, Is.EqualTo(HttpStatusCode.OK), "The hub is mapped");
    }

    [Test]
    public async Task WorkerRole_Should_RunTheGatewayAndBackgroundServices_AndAnswerOnlyHealth()
    {
        var settings = NormalModeSettings();
        settings[AppRole.ConfigurationKey] = "Worker";

        await using var factory = new StartupFactory(settings, removeDiscordHostedServices: true);
        using var client = factory.CreateClient();

        var hostedTypes = factory.HostedServices.Select(ImplementationOf).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(factory.Services.GetRequiredService<AppRole>().Kind, Is.EqualTo(AppRoleKind.Worker));
            Assert.That(factory.GatewayClientRegistered, Is.True);
            Assert.That(factory.HostedServices.Where(IsNetCord), Is.Not.Empty);
            foreach (var workerOnly in AppRole.WorkerHostedServices)
                Assert.That(hostedTypes, Does.Contain(workerOnly));
            Assert.That(factory.Services.GetRequiredService<HaloTokenOwnership>().OwnsRefresh, Is.True);
            // No ConnectionStrings:Redis: no backplane.
            Assert.That(factory.Services.GetRequiredService<HubLifetimeManager<TeamsHub>>(),
                Is.InstanceOf<DefaultHubLifetimeManager<TeamsHub>>());
        });

        var checks = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name);
        Assert.That(checks, Does.Contain("discord"));

        await AssertOk(client, "/health/live");
        foreach (var path in new[] { "/CommitHash", "/Home/PastSeasons" })
        {
            using var response = await client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), $"GET {path}: no public API on the worker");
        }
        using var hub = await client.PostAsync("/TeamsHub/negotiate?negotiateVersion=1", null);
        Assert.That(hub.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "No hub endpoint on the worker");
    }

    [Test]
    public async Task UnknownRole_Should_FailStartup()
    {
        var settings = NormalModeSettings();
        settings[AppRole.ConfigurationKey] = "both";
        var args = settings
            .Select(kv => $"--{kv.Key}={kv.Value}")
            .Append("--urls=http://127.0.0.1:0")
            .ToArray();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ServerProgram.Run(args));
        Assert.That(ex!.Message, Is.EqualTo("App:Role must be all, api or worker, not 'both'"));
    }
}
