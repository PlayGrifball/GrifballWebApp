using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
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
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddHttpContextAccessor()
            .AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>()
            .BuildServiceProvider();
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

    [Test]
    public void CreateDbContext_AttachesOneAuditInterceptor()
    {
        using var context = new GrifballContextFactory(Provider("Server=example;Database=Grif;Trusted_Connection=True;")).CreateDbContext();

        Assert.That(context.Interceptors().OfType<AuditInterceptor>().Count(), Is.EqualTo(1));
    }

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
    public async Task CreateDbContext_SavingThroughIt_RecordsTheAmbientUser()
    {
        var context = await SetUpFixture.NewGrifballContext();
        var ambient = new AmbientCurrentUser();
        try
        {
            var user = new Database.Models.User { UserName = "ambient" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            ambient.Begin();
            ambient.UserId = user.Id;
            using var factoryContext = await GrifballContextTestExtensions.FactoryFor(context).CreateDbContextAsync();
            var season = NewSeason();
            factoryContext.Seasons.Add(season);
            await factoryContext.SaveChangesAsync();

            Assert.Multiple(() =>
            {
                Assert.That(season.CreatedAt, Is.Not.EqualTo(default(DateTime)));
                Assert.That(season.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
                Assert.That(season.CreatedByID, Is.EqualTo(user.Id));
                Assert.That(season.ModifiedByID, Is.EqualTo(user.Id));
            });
        }
        finally
        {
            ambient.End();
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task CreateDbContext_NoAmbientOrHttpUser_UpdatesModifiedAtWithNoUser()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            using var factoryContext = GrifballContextTestExtensions.FactoryFor(context).CreateDbContext();
            var season = NewSeason();
            factoryContext.Seasons.Add(season);
            factoryContext.SaveChanges();
            var createdAt = season.CreatedAt;

            await Task.Delay(50);
            season.SeasonName = "Renamed";
            factoryContext.SaveChanges();

            Assert.Multiple(() =>
            {
                Assert.That(season.CreatedAt, Is.EqualTo(createdAt));
                Assert.That(season.ModifiedAt, Is.GreaterThan(createdAt));
                Assert.That(season.CreatedByID, Is.Null);
                Assert.That(season.ModifiedByID, Is.Null);
            });
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
