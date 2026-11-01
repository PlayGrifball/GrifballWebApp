using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore.Design;

namespace GrifballWebApp.Migrations.Sqlite;

/// <summary>dotnet ef and the SQLite migrations bundle, with this project as the startup project.</summary>
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<GrifballContext>
{
    public GrifballContext CreateDbContext(string[] args) => DesignTimeContext.Create(args, DatabaseProvider.Sqlite);
}
