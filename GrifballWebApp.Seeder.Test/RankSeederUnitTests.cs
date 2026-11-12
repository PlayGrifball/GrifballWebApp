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

    // Every rank in order: its name, threshold, color, description and icon file (two files are lowercase, as the seeder
    // has always asked for them)
    private static readonly (string Name, int Threshold, string Color, string Description, string Icon)[] ExpectedRanks =
    [
        ("Iron 2", 99, "#4D5458", "iron", "Iron2.png"),
        ("Iron 1", 199, "#4D5458", "iron", "iron1.png"),
        ("Bronze 3", 299, "#CD7F32", "bronze", "Bronze3.png"),
        ("Bronze 2", 424, "#CD7F32", "bronze", "Bronze2.png"),
        ("Bronze 1", 549, "#CD7F32", "bronze", "Bronze1.png"),
        ("Silver 3", 699, "#C0C0C0", "silver", "Silver3.png"),
        ("Silver 2", 849, "#C0C0C0", "silver", "Silver2.png"),
        ("Silver 1", 999, "#C0C0C0", "silver", "Silver1.png"),
        ("Gold 3", 1149, "#FFD700", "gold", "gold3.png"),
        ("Gold 2", 1299, "#FFD700", "gold", "Gold2.png"),
        ("Gold 1", 1449, "#FFD700", "gold", "Gold1.png"),
        ("Platinum 3", 1649, "#007f81", "platinum", "Platinum3.png"),
        ("Platinum 2", 1849, "#007f81", "platinum", "Platinum2.png"),
        ("Platinum 1", 2049, "#007f81", "platinum", "Platinum1.png"),
        ("Diamond 3", 2274, "#445fa5", "diamond", "Diamond3.png"),
        ("Diamond 2", 2499, "#445fa5", "diamond", "Diamond2.png"),
        ("Diamond 1", 2749, "#445fa5", "diamond", "Diamond1.png"),
        ("Masters 3", 2999, "#3BA55C", "masters", "Masters3.png"),
        ("Masters 2", 3249, "#3BA55C", "masters", "Masters2.png"),
        ("Masters 1", 3499, "#3BA55C", "masters", "Masters1.png"),
        ("Challenger", 3500, "#3BA55C", "challenger", "Challenger.png"),
    ];

    [Test]
    public async Task BuildRanksAsync_BuildsEveryRankWithItsOwnLoadedIcon()
    {
        var seeder = new RankSeeder(NewContext(new()), Loader());

        var ranks = await seeder.BuildRanksAsync();

        Assert.That(
            ranks.Select(r => (r.Name, r.MmrThreshold, r.Color, r.Description, System.Text.Encoding.UTF8.GetString(r.Icon))),
            Is.EqualTo(ExpectedRanks),
            "Each rank's icon is the one the loader gave for its own file name");
    }

    [Test]
    public async Task SeedRanks_WhenEmpty_AddsEveryRankAndSaves()
    {
        var rankSet = new FakeDbSet<Domain.Shared.Rank>();
        var context = NewContext(rankSet);
        var seeder = new RankSeeder(context, Loader());

        using var cancellation = new CancellationTokenSource();

        await seeder.SeedRanks(cancellation.Token);

        Assert.That(rankSet.Items.Select(r => r.Name), Is.EqualTo(ExpectedRanks.Select(r => r.Name)));
        await context.Received(1).SaveChangesAsync(cancellation.Token);
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
