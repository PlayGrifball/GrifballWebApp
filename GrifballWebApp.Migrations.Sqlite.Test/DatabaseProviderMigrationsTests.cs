using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Test;
using static GrifballWebApp.Infrastructure.Test.DatabaseProviderTestData;

namespace GrifballWebApp.Migrations.Sqlite.Test;

[TestFixture]
public class DatabaseProviderMigrationsTests : DatabaseProviderMigrationsTestsBase
{
    protected override DatabaseProvider Provider => DatabaseProvider.Sqlite;
    protected override string ProviderName => "Microsoft.EntityFrameworkCore.Sqlite";
    protected override string ConnectionString => SqliteConnectionString;
    protected override string MigrationsAssemblyName => DatabaseProviderExtensions.SqliteMigrationsAssembly;
}

[TestFixture, NonParallelizable]
public class DesignTimeContextFactoryTests : DesignTimeContextFactoryTestsBase
{
    protected override DatabaseProvider Provider => DatabaseProvider.Sqlite;
    protected override string ConnectionString => "Data Source=/data/grif.db";
    protected override string Logged => "file /data/grif.db";
    protected override GrifballContext CreateDbContext(string[] args) => new DesignTimeContextFactory().CreateDbContext(args);
}
