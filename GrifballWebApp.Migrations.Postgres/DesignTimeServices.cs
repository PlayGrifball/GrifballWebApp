using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Migrations.Postgres;

/// <summary>
/// dotnet ef finds this in the startup project (this one): migrations add scaffolds with
/// <see cref="HistoryMigrationsModelDiffer"/> around the context's own differ, and writes row history's
/// functions as SQL to read (<see cref="RawSqlMigrationOperationGenerator"/>). Only design time needs
/// them; the app and the bundle apply migrations already scaffolded.
/// </summary>
public class DesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services)
    {
        services.AddSingleton<IMigrationsModelDiffer>(provider => new HistoryMigrationsModelDiffer(
            provider.GetRequiredService<ICurrentDbContext>().Context.GetService<IMigrationsModelDiffer>()));
        services.AddSingleton<ICSharpMigrationOperationGenerator, RawSqlMigrationOperationGenerator>();
    }
}
