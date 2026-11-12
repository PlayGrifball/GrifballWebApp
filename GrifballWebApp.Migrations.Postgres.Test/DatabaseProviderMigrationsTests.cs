using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Test;
using static GrifballWebApp.Infrastructure.Test.DatabaseProviderTestData;

namespace GrifballWebApp.Migrations.Postgres.Test;

[TestFixture]
public class DatabaseProviderMigrationsTests : DatabaseProviderMigrationsTestsBase
{
    protected override DatabaseProvider Provider => DatabaseProvider.Postgres;
    protected override string ProviderName => "Npgsql.EntityFrameworkCore.PostgreSQL";
    protected override string ConnectionString => PostgresConnectionString;
    protected override string MigrationsAssemblyName => DatabaseProviderExtensions.PostgresMigrationsAssembly;
}

[TestFixture, NonParallelizable]
public class DesignTimeContextFactoryTests : DesignTimeContextFactoryTestsBase
{
    protected override DatabaseProvider Provider => DatabaseProvider.Postgres;
    protected override string ConnectionString => "Host=example-pg;Database=Grif;Username=postgres;Password=hunter2-pg;";
    protected override string Logged => "server example-pg, database Grif";
    protected override GrifballContext CreateDbContext(string[] args) => new DesignTimeContextFactory().CreateDbContext(args);
}
