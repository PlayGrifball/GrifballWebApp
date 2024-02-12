using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Database;
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<GrifballContext>
{
    public GrifballContext CreateDbContext(string[] args)
    {
        var configBuilder = new ConfigurationBuilder();

        configBuilder.SetBasePath(Directory.GetCurrentDirectory());

        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            //.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);
            .AddUserSecrets<DesignTimeContextFactory>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        if (args is not null)
            configBuilder.AddCommandLine(args);

        var config = configBuilder.Build();

        // Uncomment for debug info
        //var providers = config.Providers.ToList().Select(x => x.GetType().Name);
        //Console.WriteLine("Providers:" + string.Join(", ", providers));

        //Console.WriteLine(config.GetDebugView());

        var connectionString = config.GetConnectionString("GrifballWebApp")
            ?? throw new Exception("Failed to find GrifballWebApp connection string in configuration");

        Console.WriteLine($"Using connection string: {connectionString}");

        var optionsBuilder = new DbContextOptionsBuilder<GrifballContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new GrifballContext(optionsBuilder.Options);
    }
}
