using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Test;

/// <summary>ProfileCommendationsService.GetAsync against the fake IApplicationDbContext: a player's medals over their Xbox
/// account's Waypoint matches, most earned first, each with its tier (ProfileCommendationsUnitTests covers TierOf alone).</summary>
[TestFixture]
internal sealed class ProfileCommendationsServiceUnitTests : ApplicationDbContextTests
{
    private const long AliceXuid = 10;
    private const long BobXuid = 20;
    private const long KillingSpree = 1;
    private const long DoubleKill = 2;
    private const long Pancake = 3;
    private static readonly long[] MostEarnedFirst = [KillingSpree, DoubleKill];
    private static readonly long[] OnlyTheMostEarned = [KillingSpree];

    private ProfileCommendationsService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _service = new ProfileCommendationsService(Context);
        Context.MedalTypes.AddRange(MedalType.Imported(1, "Spree"), MedalType.Imported(2, "Mode"), MedalType.Imported(3, "Multikill"));
        Context.MedalDifficulties.AddRange(MedalDifficulty.Imported(1, "Normal"), MedalDifficulty.Imported(2, "Heroic"));
        Context.Medals.AddRange(
            Medal.Imported(KillingSpree, "Killing Spree", "5 kills in a row", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0),
            Medal.Imported(DoubleKill, "Double Kill", "2 quick kills", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 2, medalTypeID: 3, personalScore: 0),
            Medal.Imported(Pancake, "Pancake", "From above", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 2, personalScore: 0));
        var alice = XboxUser.FromXbox(AliceXuid, "Alice");
        var bob = XboxUser.FromXbox(BobXuid, "Bob");
        Context.XboxUsers.AddRange(alice, bob);
        Context.Users.AddRange(User.Seed(userName: "alice", xboxUser: alice, id: 1), User.Seed(userName: "bob", xboxUser: bob, id: 2), User.Seed(userName: "noxbox", id: 3));
        await Context.SaveChangesAsync();

        // Alice: 30 + 25 sprees and 5 + 5 double kills over two games (a Pancake counted 0 times isn't one); Bob: 1000 pancakes
        AddMatch(new Guid("00000000-0000-0000-0000-000000000001"),
            (AliceXuid, new[] { new MedalCount(KillingSpree, 30), new MedalCount(DoubleKill, 5), new MedalCount(Pancake, 0) }),
            (BobXuid, new[] { new MedalCount(Pancake, 1000) }));
        AddMatch(new Guid("00000000-0000-0000-0000-000000000002"),
            (AliceXuid, new[] { new MedalCount(KillingSpree, 25), new MedalCount(DoubleKill, 5) }));
        await Context.SaveChangesAsync();
    }

    private void AddMatch(Guid id, params (long Xuid, MedalCount[] Medals)[] players)
    {
        var match = Match.Seed(id, startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc), duration: TimeSpan.FromMinutes(10));
        match.AddTeam(0, 0, Outcomes.Won);
        foreach (var (xuid, medals) in players)
            match.AddParticipant(0, xuid, new MatchParticipantStats { Kills = 10 }, medals);
        Context.Matches.Add(match);
    }

    [Test]
    public async Task GetAsync_TotalsTheMedalsMostEarnedFirst_WithTheirTiers()
    {
        var result = await _service.GetAsync(1);

        Assert.That(result.Commendations.Select(c => c.MedalID), Is.EqualTo(MostEarnedFirst));
        var spree = result.Commendations[0];
        var doubleKill = result.Commendations[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((result.UserID, result.TotalMedals, result.DistinctMedals), Is.EqualTo((1, 65, 2)));
            Assert.That((spree.MedalName, spree.Description, spree.DifficultyName, spree.TypeName), Is.EqualTo(("Killing Spree", "5 kills in a row", "Normal", "Spree")));
            Assert.That((spree.Count, spree.Matches, spree.Tier, spree.TierFloor, spree.NextTierAt), Is.EqualTo((55, 2, 2, 50, (int?)100)));
            Assert.That((doubleKill.Count, doubleKill.Matches, doubleKill.Tier, doubleKill.TierFloor, doubleKill.NextTierAt), Is.EqualTo((10, 2, 1, 10, (int?)50)));
            Assert.That((doubleKill.DifficultyName, doubleKill.TypeName), Is.EqualTo(("Heroic", "Multikill")));
        }
    }

    [Test]
    public async Task GetAsync_TheLastTierReached_HasNoNextTier()
    {
        var result = await _service.GetAsync(2);

        var pancake = result.Commendations.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((result.TotalMedals, result.DistinctMedals), Is.EqualTo((1000, 1)));
            Assert.That((pancake.MedalID, pancake.Count, pancake.Matches), Is.EqualTo((Pancake, 1000, 1)));
            Assert.That((pancake.Tier, pancake.TierFloor, pancake.NextTierAt), Is.EqualTo((6, 1000, (int?)null)));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task GetAsync_Top_IsAtLeastOne(int top)
    {
        var result = await _service.GetAsync(1, top);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Commendations.Select(c => c.MedalID), Is.EqualTo(OnlyTheMostEarned));
            Assert.That(result.DistinctMedals, Is.EqualTo(2), "the totals are over every medal, not only those shown");
        }
    }

    [TestCase(1)]
    [TestCase(1000)]
    public async Task GetAsync_TopAboveWhatThePlayerHas_ShowsThemAll(int extra)
    {
        var result = await _service.GetAsync(1, ProfileCommendationsService.DefaultTop + extra);

        Assert.That(result.Commendations, Has.Count.EqualTo(2));
    }

    [TestCase(3)]
    [TestCase(999)]
    public async Task GetAsync_NoXboxAccountOrNoSuchUser_IsEmpty(int userID)
    {
        var result = await _service.GetAsync(userID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.UserID, Is.EqualTo(userID));
            Assert.That(result.TotalMedals, Is.Zero);
            Assert.That(result.DistinctMedals, Is.Zero);
            Assert.That(result.Commendations, Is.Empty);
        }
    }
}
