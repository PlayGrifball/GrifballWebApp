using GrifballWebApp.Database;
using GrifballWebApp.Seeder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeederTests
{
    private static readonly string[] IconFiles =
    [
        "Iron2.png", "iron1.png", "Bronze3.png", "Bronze2.png", "Bronze1.png", "Silver3.png", "Silver2.png", "Silver1.png",
        "gold3.png", "Gold2.png", "Gold1.png", "Platinum3.png", "Platinum2.png", "Platinum1.png", "Diamond3.png", "Diamond2.png",
        "Diamond1.png", "Masters3.png", "Masters2.png", "Masters1.png", "Challenger.png",
    ];

    private GrifballContext _context;
    private string _iconFolder;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
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

    private IConfiguration Config(string? folder) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Folder"] = folder }).Build();

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
    public async Task FileReader_LoadsIconFromConfiguredFolder()
    {
        var bytes = await new FileReader(Config(_iconFolder)).LoadIcon("Gold1.png");

        Assert.That(bytes, Is.EqualTo(new byte[] { 10, 1, 2 }));
    }

    [Test]
    public void FileReader_MissingFolderConfig_Throws()
    {
        var ex = Assert.ThrowsAsync<Exception>(() => new FileReader(Config(null)).LoadIcon("Gold1.png"));

        Assert.That(ex!.Message, Is.EqualTo("Missing Folder in configuration cannot load icon"));
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
        Assert.Multiple(() =>
        {
            Assert.That(ranks, Has.Count.EqualTo(21));
            Assert.That(ranks.First().Name, Is.EqualTo("Iron 2"));
            Assert.That(ranks.First().Icon, Is.EqualTo(new byte[] { 0, 1, 2 }), "Icon is read from the configured folder");
            Assert.That(ranks.Last().Name, Is.EqualTo("Challenger"));
            Assert.That(ranks.Last().Icon, Is.EqualTo(new byte[] { 20, 1, 2 }));
        });
    }

    [Test]
    public async Task HostedService_OldSeed_RecreatesDatabaseWithTestSeason()
    {
        var service = CreateHostedService(Config(_iconFolder));
        var old = service.GetType().GetMethod("Old", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        await (Task)old.Invoke(service, [CancellationToken.None])!;

        await using var fresh = NewContext();
        var season = await fresh.Seasons.Include(x => x.SeasonSignups).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(season.SeasonName, Is.EqualTo("Test Season 2024"));
            Assert.That(season.SeasonSignups, Has.Count.EqualTo(30));
            Assert.That(season.SeasonSignups.Count(x => x.WillCaptain), Is.EqualTo(6));
            Assert.That(season.SeasonSignups.Where(x => x.WillCaptain).Select(x => x.TeamName), Has.All.EndWith("'s Team"));
        });
        Assert.That(await fresh.Users.CountAsync(x => x.XboxUserID == 2535417961072277), Is.EqualTo(1));
    }
}
