using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Test;
using static GrifballWebApp.Infrastructure.Test.DatabaseProviderTestData;

namespace GrifballWebApp.Migrations.SqlServer.Test;

[TestFixture]
public class DatabaseProviderMigrationsTests : DatabaseProviderMigrationsTestsBase
{
    protected override DatabaseProvider Provider => DatabaseProvider.SqlServer;
    protected override string ProviderName => "Microsoft.EntityFrameworkCore.SqlServer";
    protected override string ConnectionString => SqlServerConnectionString;
    protected override string MigrationsAssemblyName => DatabaseProviderExtensions.SqlServerMigrationsAssembly;
}

[TestFixture, NonParallelizable]
public class DesignTimeContextFactoryTests : DesignTimeContextFactoryTestsBase
{
    protected override DatabaseProvider Provider => DatabaseProvider.SqlServer;
    protected override string ConnectionString => "Server=example-sql;Database=Grif;User Id=sa;Password=hunter2-sql;";
    protected override string Logged => "server example-sql, database Grif";
    protected override GrifballContext CreateDbContext(string[] args) => new DesignTimeContextFactory().CreateDbContext(args);
}
