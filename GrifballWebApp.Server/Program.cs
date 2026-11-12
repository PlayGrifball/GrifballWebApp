using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.DatabaseBackup;
using GrifballWebApp.Server.Hosting;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Preview;
using GrifballWebApp.Server.SignalR;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Telemetry;
using GrifballWebApp.Server.Telemetry.Query;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Rest;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using Serilog;
using System.Security.Cryptography.X509Certificates;

namespace GrifballWebApp.Server;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = LoggingExtensions.CreateBootstrapLogger();

        Log.Logger.ForContext<Program>().Information("Starting up");

        try
        {
            await Run(args);
            return 0;
        }
        // HostAbortedException: EF tooling stopping the host on purpose after reading its services.
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Logger.ForContext<Program>().Fatal(ex, "Fatal error");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static async Task Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var previewMode = PreviewMode.IsEnabled(builder.Configuration);
        // A preview turns Discord off unless its preview slot gives it a bot token; then Discord runs as normal.
        var discordOff = PreviewMode.DisablesDiscord(builder.Configuration);
        if (discordOff)
            Log.Logger.ForContext<Program>().Warning(PreviewMode.StartupMessage);
        else if (previewMode)
            Log.Logger.ForContext<Program>().Warning(PreviewMode.DiscordStartupMessage);

        // App:Role: all (the default) runs everything; scaled out, api processes serve the site, workers run the jobs
        // and the background services, and one discord process runs the Discord gateway and what must run once.
        var role = AppRole.FromConfiguration(builder.Configuration);
        if (role.Kind is not AppRoleKind.All)
            Log.Logger.ForContext<Program>().Information("Role: {Role}", role.Kind);

        var telemetry = TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment, GitInfo.CommitShortHash);
        builder.AddSerilogLogging(telemetry);
        // Gateway (the default, previews included) or Direct; logged so the active mode is in the startup logs.
        var haloSettings = HaloInfiniteSettings.FromConfiguration(builder.Configuration);
        Log.Logger.ForContext<Program>().Information("{HaloInfinite}", haloSettings);
        // Several pods (App:Role api or worker) need the gateway: in Direct mode the tokens are files in one pod's working directory.
        role.EnsureSupports(haloSettings);

        await AddWebHostAsync(builder, telemetry, role, discordOff,
            // A preview in Direct mode has no Microsoft app or tokens to probe with; in Gateway mode it probes the gateway.
            haloHealthCheck: !(previewMode && haloSettings.Mode == HaloInfiniteMode.Direct));
        builder.Services.AddGrifballDatabase();
        AddIdentity(builder.Services);
        AddDiscord(builder, role, discordOff);
        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration, discordOff);
        AddDiscordHandlers(builder.Services);

        // Encrypted with DataProtection:Certificate when it's set (DataProtectionAtRest), so backups can't sign anyone in.
        var keyCertificate = DataProtectionAtRest.Certificate(builder.Configuration);
        builder.Services.AddDataProtection().PersistKeysToDbContext<GrifballContext>().ProtectKeysWith(keyCertificate);
        AddAuthentication(builder, discordOff);

        var retryDelayCalculator = new RetryDelayCalculator();
        builder.Services.AddSingleton<IRetryDelayCalculator>(retryDelayCalculator);

        // IHaloInfiniteClient: the Halo gateway (default) or the app's own sign-in (HaloInfinite:Mode Direct).
        builder.AddHaloInfinite(haloSettings, retryDelayCalculator);

        // Last: drops the hosted services this role doesn't run (the workers' and the discord process's).
        builder.Services.ApplyAppRole(role);

        var app = builder.Build();

        await PrepareDatabaseAsync(app, keyCertificate);
        MapEndpoints(app, role, discordOff);

        await app.RunAsync();
    }

    /// <summary>The web host's own services: telemetry, health, forwarded headers, errors, SignalR, MVC and Swagger.</summary>
    private static async Task AddWebHostAsync(WebApplicationBuilder builder, TelemetryOptions telemetry, AppRole role, bool discordOff, bool haloHealthCheck)
    {
        builder.Services.AddMetricsAndTracing(telemetry, builder.Configuration);
        // The Telemetry page: the same telemetry read back from Loki, Prometheus and Tempo (TelemetryQuery:*)
        builder.Services.AddTelemetryQuery(builder.Configuration, telemetry.ServiceName);
        builder.Services.AddAppHealthChecks(builder.Configuration, discord: !discordOff && role.RunsDiscord, haloInfinite: haloHealthCheck);
        (await builder.Services.ConfigureAppForwardedHeadersAsync(builder.Configuration)).LogTo(Log.Logger);

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddSignalR(options =>
        {
            options.AddFilter<ExceptionLogHubFilter>();
            options.AddFilter<UserContextHubFilter>();
        })
        // Scaled out (ConnectionStrings:Redis): every process's hub messages reach every API pod's browsers.
        .AddAppBackplane(builder.Configuration);

        // Cookie sessions must send the antiforgery token with every change (CookieAntiforgeryFilter).
        builder.Services.AddAntiforgery(BrowserAuth.ConfigureAntiforgery);
        builder.Services.AddMemoryCache();
        // Scaled out (ConnectionStrings:Redis): what every API pod must show alike (the leaderboards) is cached in Redis
        builder.Services.AddAppDistributedCache(builder.Configuration);
        builder.Services.AddControllers(options => options.Filters.Add<CookieAntiforgeryFilter>())
            .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();

        // "OpenApi:Servers" (appsettings.json): every other environment's URL, offered in the document alongside the
        // one it was requested from (OpenApiServers.Build, MapEndpoints).
        builder.Services.Configure<OpenApiServersOptions>(builder.Configuration.GetSection("OpenApi"));

        builder.Services.AddSwaggerGen(option =>
        {
            option.CustomSchemaIds(x => x.FullName);

            // Controller actions' <summary>/<param>/<returns>/<remarks> (GenerateDocumentationFile, the .csproj),
            // into the OpenAPI document. The file sits next to this assembly in every environment, including the
            // Docker publish (GenerateDocumentationFile copies it there by default).
            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            option.IncludeXmlComments(xmlFile);

            option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
            {
                In = ParameterLocation.Header,
                Description = "Please enter a valid token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            option.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });
        });
    }

    /// <summary>ASP.NET Core Identity on the app's users, and the browser's session cookie.</summary>
    private static void AddIdentity(IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddIdentity<User, Role>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddDefaultTokenProviders()
        .AddSignInManager<SignInManager<User>>()
        .AddEntityFrameworkStores<GrifballContext>();

        // The browser's session: an HttpOnly, Secure, SameSite=Lax cookie (BrowserAuth).
        services.ConfigureApplicationCookie(BrowserAuth.ConfigureCookie);
        // Check sessions against the security stamp often (default 30 min), so Logout ends copies of the cookie.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = BrowserAuth.SecurityStampValidationInterval);
    }

    /// <summary>
    /// Discord: its settings and outbox (Infrastructure), the queue's watchers (QueueHub), and the gateway with its
    /// commands, components and message handler on the discord process, or only its REST client elsewhere.
    /// </summary>
    private static void AddDiscord(WebApplicationBuilder builder, AppRole role, bool discordOff)
    {
        builder.Services.AddDiscordInfrastructure(builder.Configuration, discordOff);
        builder.Services.AddSingleton<IQueueNotifier, QueueNotifier>();

        if (!discordOff && role.RunsDiscord)
        {
            builder.Services.AddDiscordGateway(options =>
            {
                options.Intents = NetCord.Gateway.GatewayIntents.MessageContent |
                                  NetCord.Gateway.GatewayIntents.GuildMessages |
                                  NetCord.Gateway.GatewayIntents.DirectMessages;
            })
            .AddGatewayHandlers(typeof(Program).Assembly)
            // DiscordInteractionPipeline sets the audit user and handles exceptions for every command and component
            .AddApplicationCommands(options => DiscordInteractionPipeline.Use(options))
            .AddComponentInteractions<NetCord.ButtonInteraction, NetCord.Services.ComponentInteractions.ButtonInteractionContext>(options => DiscordInteractionPipeline.Use(options))
            .AddComponentInteractions<NetCord.StringMenuInteraction, NetCord.Services.ComponentInteractions.StringMenuInteractionContext>(options => DiscordInteractionPipeline.Use(options))
            .AddComponentInteractions<NetCord.ModalInteraction, NetCord.Services.ComponentInteractions.ModalInteractionContext>(options => DiscordInteractionPipeline.Use(options));
        }
        else if (!discordOff)
        {
            // The API and worker roles send Discord messages but have no gateway (the discord role's): just the REST client.
            builder.Services.AddDiscordRest();
        }

        builder.Services.AddSingleton<IDiscordRestClient, DiscordRestClient>();

        // The notifications' handlers are the web host's (SignalR, the queue's Discord message)
        builder.Services.AddNotifications(typeof(Program).Assembly);
    }

    /// <summary>The Discord interactions' handlers, and the web host's own identity services.</summary>
    private static void AddDiscordHandlers(IServiceCollection services)
    {
        services.AddTransient<DiscordSetGamertag>();
        services.AddTransient<MatchmakingButtonHandler>();
        services.AddTransient<MatchmakingStringMenuHandler>();
        services.AddTransient<SignupButtonHandler>();
        services.AddTransient<DraftStringMenuHandler>();
        services.AddTransient<DiscordCommandsHandler>();
        services.AddTransient<IExternalLoginService, ExternalLoginService>();
    }

    /// <summary>
    /// A request is authenticated by its bearer token if it has one (scripts, the smoke suite), otherwise by the browser's
    /// cookie. Challenges and forbids go the same way: 401/403, never a redirect. The default sign-in scheme stays
    /// Identity's external cookie (Discord's round trip).
    /// </summary>
    private static void AddAuthentication(WebApplicationBuilder builder, bool discordOff)
    {
        var authentication = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = BrowserAuth.Scheme;
            options.DefaultAuthenticateScheme = BrowserAuth.Scheme;
            options.DefaultChallengeScheme = BrowserAuth.Scheme;
            options.DefaultForbidScheme = BrowserAuth.Scheme;
        })
        .AddPolicyScheme(BrowserAuth.Scheme, "Cookie or bearer token", options =>
        {
            options.ForwardDefaultSelector = BrowserAuth.SelectScheme;
        })
        .AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
        });

        if (!discordOff)
        {
            authentication.AddDiscord(options =>
            {
                options.ClientId = builder.Configuration.GetValue<string>("Discord:ClientId") ?? throw new Exception("Discord:ClientId is missing");
                options.ClientSecret = builder.Configuration.GetValue<string>("Discord:ClientSecret") ?? throw new Exception("Discord:ClientSecret is missing");
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }
    }

    /// <summary>
    /// The database brought up to date (when ApplyMigrations allows, and created when CreateDatabase does), the data
    /// protection keys kept in the clear dropped once there is a certificate, and the preview's smoke accounts.
    /// </summary>
    private static async Task PrepareDatabaseAsync(WebApplication app, X509Certificate2? keyCertificate)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        using var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        var exists = await context.Database.GetService<IRelationalDatabaseCreator>().ExistsAsync();
        var missingMigrations = await context.Database.GetPendingMigrationsAsync();

        if (exists is false || missingMigrations.Any())
        {
            if (exists is false)
                logger.LogWarning("Database does not exist");
            else
                logger.LogWarning("Missing migrations: {MissingMigrations}", missingMigrations);

            if (app.Services.GetRequiredService<IConfiguration>().GetValue("ApplyMigrations", false) is false)
            {
                throw new Exception("Database is missing migrations or does not exist, ApplyMigrations is false so no attempt will be made to apply migrations");
            }

            if (exists is false && app.Services.GetRequiredService<IConfiguration>().GetValue("CreateDatabase", false) is false)
            {
                throw new Exception("Database does not exist and CreateDatabase is false. Will not attempt to apply migrations");
            }

            logger.LogInformation("Applying migrations");
            await context.Database.MigrateAsync();
            logger.LogInformation("Migrations applied");
        }

        await DataProtectionAtRest.DiscardUnencryptedKeysAsync(context, keyCertificate, logger);

        // Preview mode only (it checks): the smoke suite's accounts.
        await PreviewSmokeUsers.EnsureAsync(app.Configuration, scope.ServiceProvider.GetRequiredService<UserManager<User>>(), logger);
    }

    /// <summary>The request pipeline and its endpoints: the API and hubs on the API role, health everywhere.</summary>
    private static void MapEndpoints(WebApplication app, AppRole role, bool discordOff)
    {
        // Forwarded headers first: everything after sees the real client address and scheme.
        app.UseForwardedHeaders();
        app.UseRequestTelemetry();

        app.UseExceptionHandler();
        // An error answered without a body (an empty 401/403/404/405, the auth middleware's too) gets a ProblemDetails one
        app.UseStatusCodePages();

        // The API's description in every environment, wherever the API is served: callers reach it at /api/swagger.
        // The page asks for its document relative to itself, so it works under the prefix, and "Try it out" calls the
        // API through /api (or the backend itself, with no proxy in front, in development).
        if (role.ServesApi)
        {
            app.UseSwagger(options => options.PreSerializeFilters.Add((document, httpRequest) =>
            {
                var requestUrl = $"{httpRequest.Scheme}://{httpRequest.Host}{ApiLocations.PublicPrefix}";
                var configured = httpRequest.HttpContext.RequestServices.GetRequiredService<IOptions<OpenApiServersOptions>>().Value.Servers;
                document.Servers = OpenApiServers.Build(requestUrl, configured, app.Environment.IsDevelopment());
            }));
            app.UseSwaggerUI(options => options.SwaggerEndpoint("v1/swagger.json", "GrifballWebApp"));
        }

        //app.UseHttpsRedirection();

        // Explicitly, after the forwarded headers: left out, WebApplication adds it at the start of the
        // pipeline, where the Discord callback (/signin-discord) would see the frontend's http and send
        // Discord a redirect_uri other than the https one the sign-in started with (invalid_grant).
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAppHealthChecks();

        // The workers and the discord process answer their health endpoints only.
        if (role.ServesApi)
        {
            // A created or accepted answer's Location as its caller reaches it, under /api
            app.Use(ApiLocations.UsePublicPrefix);
            app.MapControllers();

            // Hubs are not MVC, so CookieAntiforgeryFilter does not see them: refuse cookie sessions from other origins.
            app.Use(BrowserAuth.RefuseForeignHubRequests);

            app.MapHub<TeamsHub>("TeamsHub", options => { options.CloseOnAuthenticationExpiration = true; });
            // Anonymous and read-only: the matchmaking queue's size, for the home page
            app.MapHub<QueueHub>("QueueHub");

            app.MapGet("CommitHash", () => GitInfo.CommitShortHash);
            app.MapGet("CommitDate", () => GitInfo.CommitDate);
        }

        // A worker hands an API pod the backup a job made here, for a token only the app's pods can make (BackupDownloads)
        if (role.Kind == AppRoleKind.Worker)
            app.MapMethods(BackupDownloads.WorkerPath + "/{fileId}", ["GET", "HEAD"], BackupDownloadEndpoint.Serve);

        if (!discordOff && role.RunsDiscord)
        {
            app.AddModules(typeof(Program).Assembly);
        }
    }
}
