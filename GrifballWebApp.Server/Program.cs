using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Util;

namespace GrifballWebApp.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddSignalR();

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
            });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(option =>
        {
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

        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.SignIn.RequireConfirmedEmail = false;
                //options.Tokens.
            })
            .AddDefaultTokenProviders()
            .AddApiEndpoints()
            //.AddRoles<IdentityRole>()
            .AddSignInManager()
            //.AddTokenProvider(IdentityConstants.BearerScheme, BearerToke)
            //.AddUserManager<UserManager<IdentityUser<string>>>()
            .AddEntityFrameworkStores<GrifballContext>();

        //builder.Services.Configure<DataProtectionTokenProviderOptions>(o =>
        //{
        //    o.TokenLifespan = TimeSpan.FromMinutes(5);
        //});

        //builder.Services.AddIdentityApiEndpoints<IdentityUser>()
        //.AddEntityFrameworkStores<GrifballContext>();

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

        builder.Services.AddTransient<DataPullService>();
        builder.Services.AddTransient<BracketService>();
        builder.Services.AddTransient<EventOrganizerService>();
        builder.Services.AddTransient<SignupsService>();
        builder.Services.AddTransient<SeasonService>();
        builder.Services.AddTransient<TeamService>();

        builder.Services.
            AddAuthentication(options =>
            {
                //options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                //options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = IdentityConstants.BearerScheme;
                options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
                //options.DefaultAuthenticateScheme = "Test";
                //options.DefaultChallengeScheme = "Test";
                //options.DefaultScheme = "Test";
            })
            .AddBearerToken("Identity.Bearer", options =>
            {
                // Bearer defaults 1 hr
                // Refresh defaults to 14 days
                options.BearerTokenExpiration = TimeSpan.FromMinutes(1);
                //options.RefreshTokenExpiration = TimeSpan.FromMinutes(10);
            })
            .AddDiscord(options =>
            {
                options.ClientId = builder.Configuration.GetValue<string>("Discord:ClientId") ?? throw new Exception("Discord:ClientId is missing");
                options.ClientSecret = builder.Configuration.GetValue<string>("Discord:ClientSecret") ?? throw new Exception("Discord:ClientSecret is missing");
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        ////.AddCookie("Identity.Bearer")
        //.AddJwtBearer(options =>
        //{
        //    options.TokenValidationParameters = new TokenValidationParameters()
        //    {
        //        ValidateAudience = true,
        //        ValidAudience = "GrifballWebApp",
        //        ValidateIssuer = true,
        //        ValidIssuer = "GrifballWebApp",
        //        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration.GetValue<string>("JwtSecret")
        //                                                                                                ?? throw new Exception("JwtSecret is missing"))),
        //    };

        //    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents()
        //    {
        //        OnMessageReceived = context =>
        //        {
        //            var accessToken = context.Request.Query["access_token"];
        //            // TODO: may be able to use IsWebSocketRequest
        //            //var isWebSocketRequest = context.HttpContext.WebSockets.IsWebSocketRequest;
        //            // If the request is for our hub...
        //            var path = context.HttpContext.Request.Path;
        //            if (!string.IsNullOrEmpty(accessToken) &&
        //                (path.StartsWithSegments("/api/TeamsHub")))
        //            {
        //                // Read the token out of the query string
        //                context.Token = accessToken;
        //            }
        //            return Task.CompletedTask;
        //        }
        //    };

        //});

        builder.RegisterHaloInfiniteClientFactory();

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapIdentityApi<IdentityUser>();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<TeamsHub>("TeamsHub", options =>
        {
            options.CloseOnAuthenticationExpiration = true;
        });

        app.MapFallbackToFile("/index.html");

        app.Run();
    }
}
