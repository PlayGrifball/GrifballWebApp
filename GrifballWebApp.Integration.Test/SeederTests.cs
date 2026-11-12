using GrifballWebApp.Infrastructure;
using GrifballWebApp.Seeder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static GrifballWebApp.Seeder.Test.SeederTestData;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeederTests
{
    // The seeder's own icon files, each written with its index as its first byte
    private static readonly string[] IconFiles = [.. RankSeeder.Ranks.Select(r => r.IconFile)];

    private GrifballContext _context;
    private string _iconFolder;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _iconFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "icons_e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_iconFolder);
        foreach (var (file, index) in IconFiles.Select((f, i) => (f, i)))
            await File.WriteAllBytesAsync(Path.Combine(_iconFolder, file), [(byte)index, 1, 2]);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
        Directory.Delete(_iconFolder, recursive: true);
    }

    private GrifballContext NewContext() => _context.NewContextLike();

    // HostedService is internal; create it via reflection.
    private BackgroundService CreateHostedService(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => NewContext());
        var type = typeof(RankSeeder).Assembly.GetType("GrifballWebApp.Seeder.HostedService", throwOnError: true)!;
        return (BackgroundService)Activator.CreateInstance(type, services.BuildServiceProvider(), configuration)!;
    }

    [Test]
    public async Task SeedRanks_IsIdempotent()
    {
        var seeder = new RankSeeder(_context, new TestReader());
        await seeder.SeedRanks();
        await seeder.SeedRanks();

        Assert.That(await _context.Ranks.CountAsync(), Is.EqualTo(21));
    }

    [Test]
    public async Task HostedService_MigratesAndSeedsRanksFromFiles()
    {
        var service = CreateHostedService(Config(_iconFolder));

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(60));

        var ranks = await _context.Ranks.OrderBy(x => x.MmrThreshold).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ranks, Has.Count.EqualTo(21));
            Assert.That(ranks.First().Name, Is.EqualTo("Iron 2"));
            Assert.That(ranks.First().Icon, Is.EqualTo(new byte[] { 0, 1, 2 }), "Icon is read from the configured folder");
            Assert.That(ranks.Last().Name, Is.EqualTo("Challenger"));
            Assert.That(ranks.Last().Icon, Is.EqualTo(new byte[] { 20, 1, 2 }));
        }
    }
}
