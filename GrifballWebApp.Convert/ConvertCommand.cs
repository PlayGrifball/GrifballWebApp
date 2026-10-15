using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Convert;

/// <summary>
/// Copies a GrifballWebApp database into a new one, on SQL Server or PostgreSQL (DatabaseConverter).
/// Settings, from the command line (--Source:Provider=Postgres) or the environment (Source__Provider):
///   Source:Provider, Source:ConnectionString: the database to copy, which is only read;
///   Target:Provider, Target:ConnectionString: the new one, missing or without tables. Unset, they are
///   the app's own, Database:Provider and ConnectionStrings:GrifballWebApp, as the migration bundles
///   take them.
/// Exit code 0 once the copy is committed and checked, 1 if it refused or failed (the target is then
/// dropped if the conversion created it), 2 for missing or wrong settings.
/// </summary>
public static class ConvertCommand
{
    /// <summary>The settings from the environment, then the command line; logs to the console.</summary>
    public static async Task<int> MainAsync(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; })
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

        return await RunAsync(configuration, loggerFactory, cancellation.Token);
    }

    public static async Task<int> RunAsync(IConfiguration configuration, ILoggerFactory loggerFactory, CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ConvertCommand));
        DatabaseEndpoint source, target;
        try
        {
            source = Endpoint(configuration, "Source", fallbackProvider: null, fallbackConnectionString: null);
            target = Endpoint(configuration, "Target", configuration["Database:Provider"], configuration.GetConnectionString("GrifballWebApp"));
        }
        catch (InvalidOperationException e)
        {
            logger.LogError("{Message}", e.Message);
            return 2;
        }

        logger.LogInformation("Converting a {Source} database into a new {Target} one", source.Provider, target.Provider);
        try
        {
            await new DatabaseConverter(loggerFactory.CreateLogger<DatabaseConverter>()).ConvertAsync(source, target, cancellationToken);
            return 0;
        }
        catch (ConversionException e)
        {
            logger.LogError("{Message}", e.Message);
            return 1;
        }
        catch (Exception e)
        {
            logger.LogError(e, "The conversion failed");
            return 1;
        }
    }

    private static DatabaseEndpoint Endpoint(IConfiguration configuration, string section, string? fallbackProvider, string? fallbackConnectionString)
    {
        var provider = configuration[$"{section}:Provider"] ?? fallbackProvider;
        if (string.IsNullOrWhiteSpace(provider))
            throw new InvalidOperationException($"{section}:Provider is required: SqlServer or Postgres.");
        var parsed = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = provider })
            .Build()
            .GetDatabaseProvider();
        var connectionString = configuration[$"{section}:ConnectionString"] ?? fallbackConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"{section}:ConnectionString is required.");
        return new DatabaseEndpoint(parsed, connectionString);
    }
}
