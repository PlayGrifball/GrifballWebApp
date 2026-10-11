using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Database;

/// <summary>
/// The context dotnet ef and the migrations bundles use, for one provider: each migrations project's
/// IDesignTimeDbContextFactory calls this with its own. The connection string comes from
/// ConnectionStrings:GrifballWebApp (appsettings.json, this project's user secrets, the environment,
/// the command line).
/// </summary>
public static class DesignTimeContext
{
    public static GrifballContext Create(string[] args, DatabaseProvider provider)
    {
        var configBuilder = new ConfigurationBuilder();

        configBuilder.SetBasePath(Directory.GetCurrentDirectory());

        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(DesignTimeContext).Assembly, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        if (args is not null)
            configBuilder.AddCommandLine(args);

        var config = configBuilder.Build();

        var connectionString = config.GetConnectionString("GrifballWebApp")
            ?? throw new Exception("Failed to find GrifballWebApp connection string in configuration");

        // Log where it points, never the whole string: the password would land in CI and pod logs.
        Console.WriteLine($"Using {provider} connection string for {Describe(provider, connectionString)}");

        var optionsBuilder = new DbContextOptionsBuilder<GrifballContext>();
        optionsBuilder.UseGrifballDatabase(provider, connectionString);

        return new GrifballContext(optionsBuilder.Options);
    }

    private static string Describe(DatabaseProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.Postgres:
                var postgres = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                return $"server {postgres.Host}, database {postgres.Database}";
            default:
                var sqlServer = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                return $"server {sqlServer.DataSource}, database {sqlServer.InitialCatalog}";
        }
    }
}
