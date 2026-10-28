using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Test;

/// <summary>
/// Contexts from IDbContextFactory&lt;GrifballContext&gt; (used by Discord handlers such as
/// MessageCreateHandler and the Teams *Handlers) must get the AuditInterceptor, same as the
/// request-scoped GrifballContext, so CreatedAt/ModifiedAt (and CreatedByID/ModifiedByID, when a
/// current user is known) still get set. See #26/#28.
/// </summary>
[TestFixture]
public class GrifballContextFactoryAuditTests
{
    private string _dbName = null!;
    private GrifballContextFactory _factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbName = $"TestDb_{Guid.NewGuid():N}";
        await TestDatabase.CreateMigratedDatabase(SetUpFixture.DatabaseContainer, _dbName);
        var connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, _dbName);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GrifballWebApp"] = connectionString,
                ["Database:Provider"] = TestDatabase.Provider.ToString(),
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddHttpContextAccessor()
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .AddScoped<AuditInterceptor>();

        _factory = new GrifballContextFactory(services.BuildServiceProvider());
    }

    [TearDown]
    public async Task TearDown()
    {
        await TestDatabase.DropDatabase(TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, _dbName));
    }

    [Test]
    public async Task CreateDbContext_SavingThroughIt_SetsCreatedAtAndModifiedAt()
    {
        using var context = _factory.CreateDbContext();

        var season = new Season
        {
            SeasonName = "Factory Season",
            PublicAt = DateTime.UtcNow,
            SignupsOpen = DateTime.UtcNow,
            SignupsClose = DateTime.UtcNow.AddDays(7),
            DraftStart = DateTime.UtcNow.AddDays(8),
            SeasonStart = DateTime.UtcNow.AddDays(14),
            SeasonEnd = DateTime.UtcNow.AddDays(60),
        };

        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(season.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(season.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(season.CreatedAt, Is.EqualTo(season.ModifiedAt).Within(TimeSpan.FromSeconds(1)));
        });
    }

    [Test]
    public async Task CreateDbContextAsync_UpdatingThroughIt_UpdatesModifiedAtOnly()
    {
        using var context = await _factory.CreateDbContextAsync();

        var season = new Season
        {
            SeasonName = "Factory Season",
            PublicAt = DateTime.UtcNow,
            SignupsOpen = DateTime.UtcNow,
            SignupsClose = DateTime.UtcNow.AddDays(7),
            DraftStart = DateTime.UtcNow.AddDays(8),
            SeasonStart = DateTime.UtcNow.AddDays(14),
            SeasonEnd = DateTime.UtcNow.AddDays(60),
        };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var originalCreatedAt = season.CreatedAt;

        await Task.Delay(100);
        season.SeasonName = "Updated Factory Season";
        await context.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(season.CreatedAt, Is.EqualTo(originalCreatedAt), "CreatedAt should not change on update");
            Assert.That(season.ModifiedAt, Is.GreaterThan(originalCreatedAt), "ModifiedAt should be updated");
        });
    }
}
