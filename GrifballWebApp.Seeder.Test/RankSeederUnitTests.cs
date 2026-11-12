using GrifballWebApp.Infrastructure;
using NSubstitute;

namespace GrifballWebApp.Seeder.Test;

/// <summary>RankSeeder's unit tests: no database. <see cref="FakeDbSet{TEntity}"/> stands in for
/// GrifballContext.Ranks (Add/AddRange and AnyAsync's Items-backed fallback, no real provider). Its integration
/// tests (a real database) stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RankSeederUnitTests
{
    private static GrifballContext NewContext(FakeDbSet<Domain.Shared.Rank> ranks)
    {
        var context = Substitute.For<GrifballContext>();
        context.Ranks = ranks;
        context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
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
    public async Task BuildRanksAsync_BuildsEveryRankWithItsOwnLoadedIcon()
    {
        var seeder = new RankSeeder(NewContext(new()), Loader());

        var ranks = await seeder.BuildRanksAsync();

        Assert.That(ranks, Has.Count.EqualTo(21));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ranks[0].Name, Is.EqualTo("Iron 2"));
            Assert.That(ranks[0].MmrThreshold, Is.EqualTo(99));
            Assert.That(ranks[0].Icon, Is.EqualTo(System.Text.Encoding.UTF8.GetBytes("Iron2.png")), "Each rank's icon comes from the loader, keyed by its own file name");
            Assert.That(ranks[^1].Name, Is.EqualTo("Challenger"));
            Assert.That(ranks[^1].MmrThreshold, Is.EqualTo(3500));
            Assert.That(ranks.Select(r => r.MmrThreshold), Is.Ordered, "Each rank's threshold is higher than the last");
            Assert.That(ranks.Select(r => r.Name).Distinct(), Has.Count.EqualTo(21), "Every rank has its own name");
        }
    }

    [Test]
    public async Task SeedRanks_WhenEmpty_AddsEveryRankAndSaves()
    {
        var rankSet = new FakeDbSet<Domain.Shared.Rank>();
        var context = NewContext(rankSet);
        var seeder = new RankSeeder(context, Loader());

        await seeder.SeedRanks();

        Assert.That(rankSet.Items, Has.Count.EqualTo(21));
        await context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SeedRanks_WhenAlreadySeeded_DoesNothing()
    {
        var rankSet = new FakeDbSet<Domain.Shared.Rank>();
        rankSet.Items.Add(Domain.Shared.Rank.Create("Iron 2", 99, "#4D5458", "iron", []));
        var context = NewContext(rankSet);
        var loader = Loader();
        var seeder = new RankSeeder(context, loader);

        await seeder.SeedRanks();

        Assert.That(rankSet.Items, Has.Count.EqualTo(1), "Nothing new is added once ranks already exist");
        await loader.DidNotReceive().LoadIcon(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
