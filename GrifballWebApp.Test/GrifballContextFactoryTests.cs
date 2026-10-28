using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class GrifballContextFactoryTests
{
    private static IServiceProvider Provider(string? connectionString, string? provider = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:GrifballWebApp"] = connectionString, ["Database:Provider"] = provider }).Build();
        return new ServiceCollection().AddSingleton<IConfiguration>(config).BuildServiceProvider();
    }

    [Test]
    public void CreateDbContext_UsesConfiguredConnectionString()
    {
        const string cs = "Server=example;Database=Grif;Trusted_Connection=True;";

        using var context = new GrifballContextFactory(Provider(cs)).CreateDbContext();

        var actual = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Multiple(() =>
        {
            Assert.That(actual.DataSource, Is.EqualTo("example"));
            Assert.That(actual.InitialCatalog, Is.EqualTo("Grif"));
        });
    }

    [Test]
    public void CreateDbContext_Postgres_UsesNpgsql()
    {
        const string cs = "Host=example;Database=Grif;Username=grif;Password=x;";

        using var context = new GrifballContextFactory(Provider(cs, "Postgres")).CreateDbContext();

        var actual = new Npgsql.NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Multiple(() =>
        {
            Assert.That(context.Database.IsNpgsql(), Is.True);
            Assert.That(actual.Host, Is.EqualTo("example"));
            Assert.That(actual.Database, Is.EqualTo("Grif"));
        });
    }

    [Test]
    public void CreateDbContext_MissingConnectionString_Throws()
    {
        var ex = Assert.Throws<Exception>(() => new GrifballContextFactory(Provider(null)).CreateDbContext());

        Assert.That(ex!.Message, Is.EqualTo("GrifballContext failed to configure"));
    }

    // GrifballContextFactory's contexts are not audited (they're for reads). FactoryFor, the test stand-in for it
    // that handler tests use, guards against that: these tests pin the guard itself.

    private static Season NewSeason() => new()
    {
        SeasonName = "Factory Season",
        PublicAt = DateTime.UtcNow,
        SignupsOpen = DateTime.UtcNow,
        SignupsClose = DateTime.UtcNow.AddDays(7),
        DraftStart = DateTime.UtcNow.AddDays(8),
        SeasonStart = DateTime.UtcNow.AddDays(14),
        SeasonEnd = DateTime.UtcNow.AddDays(60),
    };

    [Test]
    public async Task FactoryContext_AddingAuditableEntity_FailsTheGuard()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            using var factoryContext = await GrifballContextTestExtensions.FactoryFor(context).CreateDbContextAsync();
            factoryContext.Seasons.Add(NewSeason());

            await Assert.ThrowsAsync<UnauditedWriteException>(() => factoryContext.SaveChangesAsync());
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task FactoryContext_ModifyingAuditableEntity_FailsTheGuard()
    {
        var context = await SetUpFixture.NewGrifballContext(new AuditInterceptor(new CurrentUserService(new HttpContextAccessor())));
        try
        {
            context.Seasons.Add(NewSeason());
            await context.SaveChangesAsync();

            using var factoryContext = GrifballContextTestExtensions.FactoryFor(context).CreateDbContext();
            var season = await factoryContext.Seasons.SingleAsync();
            season.SeasonName = "Renamed";

            Assert.Throws<UnauditedWriteException>(() => factoryContext.SaveChanges());
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task Guard_AllowsAuditedWrites()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            using var audited = context.NewContextLike(new AuditInterceptor(new CurrentUserService(new HttpContextAccessor())), new UnauditedWriteGuard());
            var season = NewSeason();
            audited.Seasons.Add(season);
            await audited.SaveChangesAsync();
            season.SeasonName = "Renamed";
            await audited.SaveChangesAsync();
            audited.Seasons.Remove(season);

            Assert.DoesNotThrow(() => audited.SaveChanges());
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
