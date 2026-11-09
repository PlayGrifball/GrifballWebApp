using GrifballWebApp.Database;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Domain.Players;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileCommendationsTests
{
    private GrifballContext _context;
    private ProfileController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new ProfileController(_context, Substitute.For<ISetGamertagService>(),
            new ProfileStatsService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context)),
            new ProfileCommendationsService(_context));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<User> AddUser(long? xboxUserID)
    {
        var user = User.Seed(userName: $"user{Guid.NewGuid():N}", displayName: "Player");
        if (xboxUserID is not null && await _context.XboxUsers.FindAsync(xboxUserID.Value) is null)
            _context.XboxUsers.Add(XboxUser.FromXbox(xboxUserID.Value, $"GT{xboxUserID}"));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        if (xboxUserID is long xuid)
            user.TieToXbox(xuid);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task GetCommendations_UnknownUser_IsEmpty()
    {
        // Not a 404: the profile of an unknown user shows an empty panel, not a load error
        var result = await _controller.GetCommendations(999, ct: CancellationToken.None);

        Assert.That(result.UserID, Is.EqualTo(999));
        Assert.That(result.TotalMedals, Is.Zero);
        Assert.That(result.Commendations, Is.Empty);
    }

    [Test]
    public async Task GetCommendations_UserWithoutXboxAccount_IsEmpty()
    {
        var user = await AddUser(null);

        var result = await _controller.GetCommendations(user.Id, ct: CancellationToken.None);

        Assert.That(result.UserID, Is.EqualTo(user.Id));
        Assert.That(result.TotalMedals, Is.Zero);
        Assert.That(result.DistinctMedals, Is.Zero);
        Assert.That(result.Commendations, Is.Empty);
    }

    [Test]
    public async Task GetCommendations_TotalsTheUsersMedals_MostEarnedFirst()
    {
        await GradesTestData.SeedMedals(_context);
        const long me = 100, other = 200;
        // Killing Spree 3 + 9 = 12 over two games, Double Kill 60 in one, Overkill 1; the other player's medals are not counted
        await GradesTestData.AddLinkedMatch(_context, null, 1, TimeSpan.FromMinutes(10),
            new GradesTestData.P(me, 1, 10, 2, 0, new() { [1] = 3, [3] = 60 }),
            new GradesTestData.P(other, 0, 2, 10, 0, new() { [1] = 500 }));
        await GradesTestData.AddLinkedMatch(_context, null, 2, TimeSpan.FromMinutes(10),
            new GradesTestData.P(me, 1, 10, 2, 0, new() { [1] = 9, [5] = 1 }));
        var user = await AddUser(me);

        var result = await _controller.GetCommendations(user.Id, ct: CancellationToken.None);

        Assert.That(result.TotalMedals, Is.EqualTo(73));
        Assert.That(result.DistinctMedals, Is.EqualTo(3));
        Assert.That(result.Commendations.Select(c => c.MedalName), Is.EqualTo(new[] { "Double Kill", "Killing Spree", "Overkill" }));

        var doubleKill = result.Commendations[0];
        Assert.That(doubleKill.Count, Is.EqualTo(60));
        Assert.That(doubleKill.Matches, Is.EqualTo(1));
        Assert.That(doubleKill.TypeName, Is.EqualTo("Multikill"));
        Assert.That(doubleKill.DifficultyName, Is.EqualTo("Normal"));
        Assert.That(doubleKill.Description, Is.EqualTo("Double Kill"));
        Assert.That(doubleKill.Tier, Is.EqualTo(2));
        Assert.That(doubleKill.TierFloor, Is.EqualTo(50));
        Assert.That(doubleKill.NextTierAt, Is.EqualTo(100));

        var spree = result.Commendations[1];
        Assert.That(spree.Count, Is.EqualTo(12));
        Assert.That(spree.Matches, Is.EqualTo(2));
        Assert.That(spree.Tier, Is.EqualTo(1));

        var overkill = result.Commendations[2];
        Assert.That(overkill.Tier, Is.Zero);
        Assert.That(overkill.TierFloor, Is.Zero);
        Assert.That(overkill.NextTierAt, Is.EqualTo(10));
        Assert.That(overkill.DifficultyName, Is.EqualTo("Mythic"));
    }

    [Test]
    public async Task GetCommendations_Top_LimitsTheList_ButNotTheTotals()
    {
        await GradesTestData.SeedMedals(_context);
        const long me = 300;
        await GradesTestData.AddLinkedMatch(_context, null, 1, TimeSpan.FromMinutes(10),
            new GradesTestData.P(me, 1, 10, 2, 0, new() { [1] = 5, [3] = 4, [4] = 3, [6] = 0 }));
        var user = await AddUser(me);

        var result = await _controller.GetCommendations(user.Id, top: 2, ct: CancellationToken.None);

        Assert.That(result.TotalMedals, Is.EqualTo(12));
        // A medal earned zero times is not one
        Assert.That(result.DistinctMedals, Is.EqualTo(3));
        Assert.That(result.Commendations.Select(c => c.MedalName), Is.EqualTo(new[] { "Killing Spree", "Double Kill" }));
    }

    [Test]
    public async Task GetCommendations_TopBelowOne_StillReturnsOne()
    {
        await GradesTestData.SeedMedals(_context);
        const long me = 400;
        await GradesTestData.AddLinkedMatch(_context, null, 1, TimeSpan.FromMinutes(10),
            new GradesTestData.P(me, 1, 10, 2, 0, new() { [1] = 5, [3] = 4 }));
        var user = await AddUser(me);

        var result = await _controller.GetCommendations(user.Id, top: 0, ct: CancellationToken.None);

        Assert.That(result.Commendations, Has.Count.EqualTo(1));
    }

    [TestCase(0, 0)]
    [TestCase(9, 0)]
    [TestCase(10, 1)]
    [TestCase(999, 5)]
    [TestCase(1000, 6)]
    [TestCase(50000, 6)]
    public void TierOf_CountsTheThresholdsReached(int count, int tier)
    {
        Assert.That(ProfileCommendationsService.TierOf(count), Is.EqualTo(tier));
    }

    [Test]
    public async Task GetCommendations_LastTier_HasNoNext()
    {
        await GradesTestData.SeedMedals(_context);
        const long me = 500;
        await GradesTestData.AddLinkedMatch(_context, null, 1, TimeSpan.FromMinutes(10),
            new GradesTestData.P(me, 1, 10, 2, 0, new() { [1] = 1200 }));
        var user = await AddUser(me);

        var spree = (await _controller.GetCommendations(user.Id, ct: CancellationToken.None)).Commendations.Single();

        Assert.That(spree.Tier, Is.EqualTo(ProfileCommendationsService.TierThresholds.Count));
        Assert.That(spree.TierFloor, Is.EqualTo(1000));
        Assert.That(spree.NextTierAt, Is.Null);
    }
}
