using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Database;

/// <summary>
/// The database server the app runs on: setting Database:Provider, SQL Server unless set. Each has
/// its own migrations project (GrifballWebApp.Migrations.SqlServer, GrifballWebApp.Migrations.Postgres),
/// both generated from the same model.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    Postgres,
}

public static class DatabaseProviderExtensions
{
    public const string SqlServerMigrationsAssembly = "GrifballWebApp.Migrations.SqlServer";
    public const string PostgresMigrationsAssembly = "GrifballWebApp.Migrations.Postgres";

    /// <summary>Database:Provider (SqlServer or Postgres, any case); SqlServer when not set.</summary>
    public static DatabaseProvider GetDatabaseProvider(this IConfiguration configuration)
    {
        var value = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(value))
            return DatabaseProvider.SqlServer;
        if (Enum.TryParse<DatabaseProvider>(value, ignoreCase: true, out var provider) && Enum.IsDefined(provider))
            return provider;
        throw new InvalidOperationException($"Database:Provider '{value}' is not one of: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}");
    }

    /// <summary>
    /// The provider, with its own migrations assembly, on <paramref name="connectionString"/>. Postgres
    /// keeps row history here (<see cref="RowHistory"/>), so every context does: the app's, its factory's,
    /// the seeder's, the tests'. Its interceptor runs before those added after it (AuditInterceptor); the
    /// order changes nothing, as it records original values, which they don't touch.
    /// </summary>
    public static DbContextOptionsBuilder UseGrifballDatabase(this DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => options.UseSqlServer(connectionString, o => o.MigrationsAssembly(SqlServerMigrationsAssembly)),
            DatabaseProvider.Postgres => options.UseNpgsql(connectionString, o => o.MigrationsAssembly(PostgresMigrationsAssembly))
                .ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator, PostgresMigrationsSqlGenerator>()
                .AddInterceptors(Interceptors.RowHistoryInterceptor.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }

    /// <summary>Database:Provider on ConnectionStrings:GrifballWebApp.</summary>
    public static DbContextOptionsBuilder UseGrifballDatabase(this DbContextOptionsBuilder options, IConfiguration configuration)
    {
        return options.UseGrifballDatabase(configuration.GetDatabaseProvider(),
            configuration.GetConnectionString("GrifballWebApp") ?? throw new Exception("GrifballContext failed to configure"));
    }

    public static DbContextOptionsBuilder<TContext> UseGrifballDatabase<TContext>(this DbContextOptionsBuilder<TContext> options, DatabaseProvider provider, string connectionString)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)options).UseGrifballDatabase(provider, connectionString);
        return options;
    }

    public static DbContextOptionsBuilder<TContext> UseGrifballDatabase<TContext>(this DbContextOptionsBuilder<TContext> options, IConfiguration configuration)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)options).UseGrifballDatabase(configuration);
        return options;
    }
}
