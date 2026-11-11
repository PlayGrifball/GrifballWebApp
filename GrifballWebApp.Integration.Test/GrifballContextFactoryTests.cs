using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
public class GrifballContextFactoryTests
{
    private static ServiceProvider Provider(string? connectionString, string? provider = null)
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(actual.DataSource, Is.EqualTo("example"));
            Assert.That(actual.InitialCatalog, Is.EqualTo("Grif"));
        }
    }

    [Test]
    public void CreateDbContext_Postgres_UsesNpgsql()
    {
        const string cs = "Host=example;Database=Grif;Username=grif;Password=x;";

        using var context = new GrifballContextFactory(Provider(cs, "Postgres")).CreateDbContext();

        var actual = new Npgsql.NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Database.IsNpgsql(), Is.True);
            Assert.That(actual.Host, Is.EqualTo("example"));
            Assert.That(actual.Database, Is.EqualTo("Grif"));
        }
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

    private static Season NewSeason() => Season.Seed(seasonName: "Factory Season").WithDates(publicAt: DateTime.UtcNow, signupsOpen: DateTime.UtcNow, signupsClose: DateTime.UtcNow.AddDays(7), draftStart: DateTime.UtcNow.AddDays(8), seasonStart: DateTime.UtcNow.AddDays(14), seasonEnd: DateTime.UtcNow.AddDays(60));

    [Test]
    public async Task CreateDbContext_SavingThroughIt_RecordsTheAmbientUser()
    {
        var context = await TestDatabase.NewGrifballContext();
        var ambient = new AmbientCurrentUser();
        try
        {
            var user = Domain.Players.User.Seed(userName: "ambient");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            ambient.Begin();
            ambient.UserId = user.Id;
            using var factoryContext = await GrifballContextTestExtensions.FactoryFor(context).CreateDbContextAsync();
            var season = NewSeason();
            factoryContext.Seasons.Add(season);
            await factoryContext.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(season.CreatedAt, Is.Not.Default);
                Assert.That(season.ModifiedAt, Is.Not.Default);
                Assert.That(season.CreatedByID, Is.EqualTo(user.Id));
                Assert.That(season.ModifiedByID, Is.EqualTo(user.Id));
            }
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
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            using var factoryContext = GrifballContextTestExtensions.FactoryFor(context).CreateDbContext();
            var season = NewSeason();
            factoryContext.Seasons.Add(season);
            await factoryContext.SaveChangesAsync();
            var createdAt = season.CreatedAt;

            await Task.Delay(50);
            season.Edited(seasonName: "Renamed");
            await factoryContext.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(season.CreatedAt, Is.EqualTo(createdAt));
                Assert.That(season.ModifiedAt, Is.GreaterThan(createdAt));
                Assert.That(season.CreatedByID, Is.Null);
                Assert.That(season.ModifiedByID, Is.Null);
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
