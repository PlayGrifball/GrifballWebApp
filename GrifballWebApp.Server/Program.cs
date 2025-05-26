using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Excel;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.MatchPlanner;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Scheduler;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OpenApi.Models;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using Serilog;
using Surprenant.Grunt.Util;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Debug(Serilog.Events.LogEventLevel.Verbose)
                .WriteTo.Console(Serilog.Events.LogEventLevel.Verbose)
                .MinimumLevel.Verbose()
                .CreateBootstrapLogger();

        Log.Logger.ForContext<Program>().Information("Starting up");
        
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext<Program>().Fatal(ex, "Fatal error");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services));

        // Add services to the container.
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddSignalR();

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
            });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(option =>
        {
            option.CustomSchemaIds(x => x.FullName);

            option.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme()
            {
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "Please enter a valid token",
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            option.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
            {
                {
                    new OpenApiSecurityScheme()
                    {
                        Reference = new OpenApiReference()
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[]{}
                }
            });
        });

        builder.Services.AddDbContextFactory<GrifballContext>((services, options)
            => options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("GrifballWebApp")
            ?? throw new Exception("GrifballContext failed to configure")));

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

        builder.Services.ConfigureApplicationCookie(o =>
        {
            o.Events = new CookieAuthenticationEvents()
            {
                OnRedirectToLogin = (ctx) =>
                {
                    if (ctx.Response.StatusCode == 200)
                    {
                        ctx.Response.StatusCode = 401;
                    }

                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = (ctx) =>
                {
                    if (ctx.Response.StatusCode == 200)
                    {
                        ctx.Response.StatusCode = 403;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddOptions<DiscordOptions>()
            .Bind(builder.Configuration.GetSection("Discord"))
            .Validate(options =>
            {
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(options.ClientId))
                    errors.Add("ClientId is required");
                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                    errors.Add("ClientSecret is required");
                if (options.DisableGlobally)
                {

                }
                else
                {
                    if (string.IsNullOrWhiteSpace(options.Token))
                        errors.Add("Token is required");
                    if (options.DraftChannel is 0)
                        errors.Add("DraftChannel is required");
                }
                
                if (errors.Count > 0)
                {
                    throw new ArgumentException(string.Join(", ", errors));
                }
                return true;
            })
            .ValidateOnStart();

        builder.Services.AddTransient<IQueueRepository, QueueRepository>();
        builder.Services.AddTransient<QueueService>();
        builder.Services.AddHostedService<QueueBackgroundService>();
        builder.Services.AddDiscordGateway()
            .AddApplicationCommands()
            .AddComponentInteractions<NetCord.ButtonInteraction, NetCord.Services.ComponentInteractions.ButtonInteractionContext>()
            .AddComponentInteractions<NetCord.StringMenuInteraction, NetCord.Services.ComponentInteractions.StringMenuInteractionContext>(); ;
        builder.Services.AddSingleton<IDiscordClient, DiscordClient>();
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });

        builder.Services.AddTransient<IDataPullService, DataPullService>();
        builder.Services.AddTransient<BracketService>();
        builder.Services.AddTransient<EventOrganizerService>();
        builder.Services.AddTransient<SignupsService>();
        builder.Services.AddTransient<SeasonService>();
        builder.Services.AddTransient<TeamService>();
        builder.Services.AddTransient<UserManagementService>();
        builder.Services.AddTransient<ExcelService>();
        builder.Services.AddTransient<MatchPlannerService>();
        builder.Services.AddTransient<SeasonMatchService>();
        builder.Services.AddTransient<TeamStandingsService>();
        builder.Services.AddTransient<ProfileService>();
        builder.Services.AddTransient<GradesService>();
        builder.Services.AddTransient<ScheduleService>();
        builder.Services.AddTransient<AvailabilityService>();

        builder.Services.AddDataProtection()
            .PersistKeysToDbContext<GrifballContext>();

        builder.Services.
            AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.BearerScheme;
                options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
            })
            .AddBearerToken("Identity.Bearer", options =>
            {
                // Bearer defaults 1 hr
                // Refresh defaults to 14 days
                options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
                //options.RefreshTokenExpiration = TimeSpan.FromMinutes(10);

                options.Events = new BearerTokenEvents()
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        // TODO: may be able to use IsWebSocketRequest
                        //var isWebSocketRequest = context.HttpContext.WebSockets.IsWebSocketRequest;
                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/api/TeamsHub")))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };


            })
            .AddDiscord(options =>
            {
                options.ClientId = builder.Configuration.GetValue<string>("Discord:ClientId") ?? throw new Exception("Discord:ClientId is missing");
                options.ClientSecret = builder.Configuration.GetValue<string>("Discord:ClientSecret") ?? throw new Exception("Discord:ClientSecret is missing");
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });

        builder.RegisterHaloInfiniteClientFactory();

        var app = builder.Build();

        {
            using var scope = app.Services.CreateScope();
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
                context.Database.Migrate();
                logger.LogInformation("Migrations applied");
            }
        }

        app.UseForwardedHeaders(new ForwardedHeadersOptions()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
        });

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        //app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<TeamsHub>("TeamsHub", options =>
        {
            // Need to reinvestigate this with bearer tokens
            options.CloseOnAuthenticationExpiration = true;
        });

        app.MapGet("CommitHash", () => GitInfo.CommitShortHash);
        app.MapGet("CommitDate", () => GitInfo.CommitDate);

        app.AddModules(typeof(Program).Assembly);
        app.UseGatewayEventHandlers();

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