using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore.Design;

namespace GrifballWebApp.Migrations.SqlServer;

/// <summary>dotnet ef and the SQL Server migrations bundle, with this project as the startup project.</summary>
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<GrifballContext>
{
    public GrifballContext CreateDbContext(string[] args) => DesignTimeContext.Create(args, DatabaseProvider.SqlServer);
}
