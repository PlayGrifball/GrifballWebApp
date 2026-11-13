using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static GrifballWebApp.Seeder.Test.SeederTestData;

namespace GrifballWebApp.Seeder.Test;

/// <summary>
/// The seeder's hosted service on a SQLite file of the test's own (in a temp folder, with the icon files beside it):
/// it migrates the database, then seeds the ranks from the configured folder. Every real server is
/// GrifballWebApp.Integration.Test's.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HostedServiceUnitTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

    private DirectoryInfo _folder = null!;
    private ServiceProvider? _services;

    [SetUp]
    public void CreateFolder() => _folder = Directory.CreateTempSubdirectory("seeder-host-");

    [TearDown]
    public void DeleteFolder()
    {
        _services?.Dispose();
        _folder.Delete(recursive: true);
    }

    /// <summary>The test's own database file, unpooled: each connection closes the file, so TearDown can delete it without
    /// touching the pools of the tests running beside it.</summary>
    private DbContextOptions<GrifballContext> Options() => new DbContextOptionsBuilder<GrifballContext>()
        .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={Path.Join(_folder.FullName, "grif.db")};Pooling=False").Options;

    /// <summary>The service, on a provider that hands each scope a new context on the test's database.</summary>
    private HostedService Service(string? iconFolder)
    {
        var options = Options();
        _services = new ServiceCollection().AddScoped(_ => new GrifballContext(options)).BuildServiceProvider();
        return new HostedService(_services, Config(iconFolder));
    }

    [Test]
    public async Task ExecuteAsync_MigratesThenSeedsEveryRankWithItsOwnIconFile()
    {
        // Each icon file's one byte is its rank's place in the list
        for (var i = 0; i < RankSeeder.Ranks.Count; i++)
            await File.WriteAllBytesAsync(Path.Join(_folder.FullName, RankSeeder.Ranks[i].IconFile), [(byte)i]);
        using var service = Service(_folder.FullName);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(Patience);

        await using var context = new GrifballContext(Options());
        var ranks = await context.Ranks.OrderBy(r => r.MmrThreshold).Select(r => new { r.Name, r.Icon }).ToListAsync();
        var pending = await context.Database.GetPendingMigrationsAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pending, Is.Empty, "Every migration applied before the seed");
            Assert.That(ranks.Select(r => r.Name), Is.EqualTo(RankSeeder.Ranks.Select(r => r.Name)), "Every rank, lowest first");
            Assert.That(ranks.Select(r => r.Icon), Is.EqualTo(Enumerable.Range(0, RankSeeder.Ranks.Count).Select(i => new[] { (byte)i })),
                "Each rank's icon read from its own file in the configured folder");
        }
    }

    [Test]
    public async Task ExecuteAsync_WithoutAnIconFolder_FailsAfterMigrating_AndSeedsNothing()
    {
        using var service = Service(iconFolder: null);

        var failed = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!.WaitAsync(Patience);
        });

        await using var context = new GrifballContext(Options());
        var pending = await context.Database.GetPendingMigrationsAsync();
        var ranks = await context.Ranks.CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(failed!.Message, Is.EqualTo("Missing Folder in configuration cannot load icon"));
            Assert.That(pending, Is.Empty, "The database is migrated first");
            Assert.That(ranks, Is.Zero, "No rank without its icon");
        }
    }
}
