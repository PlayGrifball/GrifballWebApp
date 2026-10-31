using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Excel;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.MatchPlanner;
using GrifballWebApp.Server.Preview;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Scheduler;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.SignalR;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Server.Telemetry;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OpenApi;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using Serilog;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Util;
using System.Net;
using Polly;
using Polly.Extensions.Http;

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
            Log.CloseAndFlush();
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

        var telemetry = TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment, GitInfo.CommitShortHash);
        builder.AddSerilogLogging(telemetry);
        builder.Services.AddMetricsAndTracing(telemetry, builder.Configuration);
        builder.Services.AddAppHealthChecks(builder.Configuration, discord: !discordOff, haloInfinite: !previewMode);
        (await builder.Services.ConfigureAppForwardedHeadersAsync(builder.Configuration)).LogTo(Log.Logger);

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddSignalR(options =>
        {
            options.AddFilter<ExceptionLogHubFilter>();
            options.AddFilter<UserContextHubFilter>();
        });

        // Cookie sessions must send the antiforgery token with every change (CookieAntiforgeryFilter).
        builder.Services.AddAntiforgery(BrowserAuth.ConfigureAntiforgery);
        builder.Services.AddControllers(options => options.Filters.Add<CookieAntiforgeryFilter>())
            .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(option =>
        {
            option.CustomSchemaIds(x => x.FullName);

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

        // Add HTTP context accessor for audit interceptor
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddScoped<Database.Interceptors.AuditInterceptor>();

        builder.Services.AddDbContext<GrifballContext>((services, options) =>
        {
            options.UseGrifballDatabase(services.GetRequiredService<IConfiguration>())
                .AddInterceptors(services.GetRequiredService<Database.Interceptors.AuditInterceptor>(),
                    services.GetRequiredService<OutboxSignalInterceptor>());
        });

        // For some reason I need to have my own implementation of IDbContextFactory<GrifballContext> otherwise it conflicts with the scoped context
        builder.Services.AddDbContextFactory<GrifballContext, GrifballContextFactory>();

        //builder.Services.AddDbContextFactory<GrifballContext>((services, options)
        //    => options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("GrifballWebApp")
        //    ?? throw new Exception("GrifballContext failed to configure")));

        builder.Services.AddAuthorization();

        builder.Services.AddIdentity<User, Role>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddDefaultTokenProviders()
        .AddSignInManager<SignInManager<User>>()
        .AddEntityFrameworkStores<GrifballContext>();

        // The browser's session: an HttpOnly, Secure, SameSite=Lax cookie (BrowserAuth).
        builder.Services.ConfigureApplicationCookie(BrowserAuth.ConfigureCookie);
        // Check sessions against the security stamp often (default 30 min), so Logout ends copies of the cookie.
        builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = BrowserAuth.SecurityStampValidationInterval);

        if (discordOff)
        {
            builder.Services.AddPreviewDiscord(builder.Configuration);
        }
        else
        {
            builder.Services.AddOptions<DiscordOptions>()
                .Bind(builder.Configuration.GetSection("Discord"))
                .Validate(options =>
                {
                    var errors = new List<string>();
                    if (string.IsNullOrWhiteSpace(options.ClientId))
                        errors.Add("ClientId is required");
                    if (string.IsNullOrWhiteSpace(options.ClientSecret))
                        errors.Add("ClientSecret is required");

                    if (string.IsNullOrWhiteSpace(options.Token))
                        errors.Add("Token is required");
                    if (options.DraftChannel is 0)
                        errors.Add("DraftChannel is required");

                    if (errors.Count > 0)
                        throw new ArgumentException(string.Join(", ", errors));

                    return true;
                })
                .ValidateOnStart();
        }

        builder.Services.AddTransient<DiscordOnDeckMessages>();
        builder.Services.AddTransient<UrlService>();
        builder.Services.AddTransient<IQueueRepository, QueueRepository>();
        builder.Services.AddTransient<QueueService>();
        // Discord side effects queued with their database changes, sent by OutboxDispatcher (Discord off: skipped).
        builder.Services.AddDiscordOutbox(builder.Configuration);
        builder.Services.AddMatchmakingOutboxHandlers();
        if (!discordOff)
            builder.Services.AddHostedService<QueueBackgroundService>();
        builder.Services.AddTransient<EventsService>();
        if (!discordOff)
            builder.Services.AddHostedService<EventsBackgroundService>();

        if (!discordOff)
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

        builder.Services.AddSingleton<IDiscordRestClient, DiscordRestClient>();

        builder.Services.AddNotifications(typeof(Program).Assembly);

        builder.Services.AddTransient<IDataPullService, DataPullService>();
        // The historical import: queued by HistoricalImportController, run in the background by HistoricalImportWorker
        builder.Services.AddOptions<GrifballWebApp.Server.Historical.HistoricalImportOptions>().Bind(builder.Configuration.GetSection("HistoricalImport"));
        builder.Services.AddOptions<GrifballWebApp.Server.Historical.HistoricalImportWorkerOptions>().Bind(builder.Configuration.GetSection("HistoricalImport:Worker"));
        builder.Services.AddTransient<GrifballWebApp.Server.Historical.IHistoricalImportService, GrifballWebApp.Server.Historical.HistoricalImportService>();
        builder.Services.AddTransient<GrifballWebApp.Server.Historical.HistoricalImportJobs>();
        builder.Services.AddTransient<GrifballWebApp.Server.Historical.GamertagClaimService>();
        builder.Services.AddSingleton<GrifballWebApp.Server.Historical.HistoricalImportSignal>();
        builder.Services.AddHostedService<GrifballWebApp.Server.Historical.HistoricalImportWorker>();
        builder.Services.AddTransient<IBracketService, BracketService>();
        builder.Services.AddTransient<EventOrganizerService>();
        builder.Services.AddTransient<SignupsService>();
        builder.Services.AddTransient<SeasonService>();
        builder.Services.AddTransient<TeamService>();
        builder.Services.AddTransient<IUserManagementService, UserManagementService>();
        builder.Services.AddTransient<ExcelService>();
        builder.Services.AddTransient<MatchPlannerService>();
        builder.Services.AddTransient<SeasonMatchService>();
        builder.Services.AddTransient<TeamStandingsService>();
        builder.Services.AddTransient<ISetGamertagService, SetGamertagService>();
        builder.Services.AddTransient<GradesService>();
        builder.Services.AddTransient<ScheduleService>();
        builder.Services.AddTransient<AvailabilityService>();
        builder.Services.AddTransient<DiscordSetGamertag>();
        builder.Services.AddTransient<MatchmakingButtonHandler>();
        builder.Services.AddTransient<MatchmakingStringMenuHandler>();
        builder.Services.AddTransient<SignupButtonHandler>();
        builder.Services.AddTransient<DraftStringMenuHandler>();
        builder.Services.AddTransient<DiscordCommandsHandler>();
        builder.Services.AddTransient<IGetsertXboxUserService, GetsertXboxUserService>();
        builder.Services.AddTransient<IUserMergeService, UserMergeService>();
        builder.Services.AddTransient<IExternalLoginService, ExternalLoginService>();
        builder.Services.AddScoped<IDiscordAccountLinkService, DiscordAccountLinkService>();
        builder.Services.AddTransient<GrifballWebApp.Server.Reschedule.RescheduleService>();

        builder.Services.AddDataProtection().PersistKeysToDbContext<GrifballContext>();

        // A request is authenticated by its bearer token if it has one (scripts, the smoke suite), otherwise by
        // the browser's cookie. Challenges and forbids go the same way: 401/403, never a redirect.
        // The default sign-in scheme stays Identity's external cookie (Discord's round trip).
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

        builder.RegisterHaloInfiniteClientFactory();

        var retryDelayCalculator = new RetryDelayCalculator();
        builder.Services.AddSingleton<IRetryDelayCalculator>(retryDelayCalculator);

        builder.Services.AddHttpClient<IHaloInfiniteClient, HaloInfiniteClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            })
            .AddPolicyHandler((services, request) =>
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                const int maxRetries = 5;

                return HttpPolicyExtensions.HandleTransientHttpError()
                    .OrResult(msg => (int)msg.StatusCode == 429)
                    .WaitAndRetryAsync(
                        maxRetries,
                        (int retryAttempt, DelegateResult<HttpResponseMessage> outcome, Context ctx) =>
                        {
                            return retryDelayCalculator.CalculateDelay(retryAttempt, outcome?.Result);
                        },
                        (outcome, timespan, retryNumber, ctx) =>
                        {
                            var status = outcome?.Result?.StatusCode.ToString() ?? (outcome?.Exception?.GetType().Name ?? "Unknown");
                            logger.LogWarning("Received HTTP {Status} for {Method} {Uri}. Retry {Retry}/{MaxRetries}. Next delay {Delay}.", status, request.Method, request.RequestUri, retryNumber, maxRetries, timespan);
                            return Task.CompletedTask;
                        });
            });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            using var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

            var exists = context.Database.GetService<IRelationalDatabaseCreator>().Exists();
            var missingMigrations = context.Database.GetPendingMigrations();

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

            // Preview mode only (it checks): the smoke suite's accounts.
            await PreviewSmokeUsers.EnsureAsync(app.Configuration, scope.ServiceProvider.GetRequiredService<UserManager<User>>(), logger);
        }

        // Forwarded headers first: everything after sees the real client address and scheme.
        app.UseForwardedHeaders();
        app.UseRequestTelemetry();

        app.UseExceptionHandler();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        //app.UseHttpsRedirection();

        // Explicitly, after the forwarded headers: left out, WebApplication adds it at the start of the
        // pipeline, where the Discord callback (/signin-discord) would see the frontend's http and send
        // Discord a redirect_uri other than the https one the sign-in started with (invalid_grant).
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAppHealthChecks();

        app.MapControllers();

        // Hubs are not MVC, so CookieAntiforgeryFilter does not see them: refuse cookie sessions from other origins.
        app.Use(BrowserAuth.RefuseForeignHubRequests);

        app.MapHub<TeamsHub>("TeamsHub", options => { options.CloseOnAuthenticationExpiration = true; });

        app.MapGet("CommitHash", () => GitInfo.CommitShortHash);
        app.MapGet("CommitDate", () => GitInfo.CommitDate);

        if (!discordOff)
        {
            app.AddModules(typeof(Program).Assembly);
        }

        app.Run();
    }
}

public class DiscordOptions
{
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string Token { get; set; } = null!;
    public ulong DraftChannel { get; set; }
    public bool DisableGlobally { get; set; } = false;
    public ulong QueueChannel { get; set; }
    public ulong EventsChannel { get; set; }
    public ulong ReschedulesChannel { get; set; }
    public ulong LogChannel { get; set; }
    public int MatchPlayers { get; set; } = 8; // Default to 8 players per match
    public int KFactor { get; set; } = 32;
    public int WinThreshold { get; set; } = 3;
    public int BonusPerWin { get; set; } = 10;
    public int MaxBonus { get; set; } = 20;
    public int LossThreshold { get; set; } = 3;
    public int PenaltyPerLoss { get; set; } = 10;
    public int MaxPenalty { get; set; } = 20;
}