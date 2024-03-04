using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Surprenant.Grunt.Util;

namespace GrifballWebApp.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddDbContextFactory<GrifballContext>((services, options)
            => options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("GrifballWebApp")
            ?? throw new Exception("GrifballContext failed to configure")));

        builder.Services.AddScoped<DataPullService>();
        builder.Services.AddScoped<AccountService>();
        builder.Services.AddScoped<CryptographyService>();

        builder.Services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration.GetValue<string>("JwtSecret")
                                                                                                            ?? throw new Exception("JwtSecret is missing"))),
                };
            });

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

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.MapFallbackToFile("/index.html");

        app.Run();
    }
}
