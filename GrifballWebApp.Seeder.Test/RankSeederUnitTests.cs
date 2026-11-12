using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Seeder.Test;

/// <summary>RankSeeder's unit tests, on an in-memory SQLite GrifballContext (no server, no file); the icon loader is
/// substituted. Its integration tests (every real server) stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RankSeederUnitTests
{
    /// <summary>A context on its own in-memory SQLite database, which lives as long as the context's open connection.</summary>
    private static async Task<GrifballContext> NewContextAsync()
    {
        var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, "Data Source=:memory:").Options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    /// <summary>An icon loader that answers with the requested file name's own bytes, so a test can tell which icon a
    /// rank got.</summary>
    private static IIconLoader Loader()
    {
        var loader = Substitute.For<IIconLoader>();
        loader.LoadIcon(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(System.Text.Encoding.UTF8.GetBytes((string)call[0]!)));
        return loader;
    }

    [Test]
    public async Task BuildRanksAsync_BuildsEveryRankInOrderWithItsOwnIcon()
    {
        await using var context = await NewContextAsync();

        var ranks = await new RankSeeder(context, Loader()).BuildRanksAsync();

        var icons = ranks.Select(r => System.Text.Encoding.UTF8.GetString(r.Icon)).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ranks.Select(r => r.Name), Is.EqualTo(RankSeeder.Ranks.Select(r => r.Name)), "every rank, lowest first");
            Assert.That(ranks.Select(r => r.MmrThreshold), Is.Ordered.Ascending, "lowest first");
            Assert.That(ranks.Select(r => r.MmrThreshold), Is.Unique, "each threshold above the last");
            Assert.That(ranks.Select(r => r.Name), Is.Unique);
            Assert.That(icons, Is.Unique, "no two ranks share an icon");
            // A copy-paste slip ("Gold 2" loading Gold3.png) breaks this: each icon file is its rank's name
            Assert.That(ranks.Zip(icons).Where(p => !string.Equals(p.Second, p.First.Name.Replace(" ", "") + ".png", StringComparison.OrdinalIgnoreCase)),
                Is.Empty, "each rank loads the icon named after it");
        }
    }

    [Test]
    public async Task SeedRanks_WhenEmpty_SavesEveryRank()
    {
        await using var context = await NewContextAsync();
        using var cancellation = new CancellationTokenSource();

        await new RankSeeder(context, Loader()).SeedRanks(cancellation.Token);

        context.ChangeTracker.Clear();
        var saved = await context.Ranks.OrderBy(r => r.MmrThreshold).Select(r => r.Name).ToListAsync();
        Assert.That(saved, Is.EqualTo(RankSeeder.Ranks.Select(r => r.Name)));
    }

    [Test]
    public async Task SeedRanks_WhenAlreadySeeded_DoesNothing()
    {
        await using var context = await NewContextAsync();
        context.Ranks.Add(Domain.Shared.Rank.Create("Iron 2", 99, "#4D5458", "iron", []));
        await context.SaveChangesAsync();
        var loader = Loader();

        await new RankSeeder(context, loader).SeedRanks();

        Assert.That(await context.Ranks.CountAsync(), Is.EqualTo(1), "nothing new is added once ranks exist");
        await loader.DidNotReceive().LoadIcon(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
