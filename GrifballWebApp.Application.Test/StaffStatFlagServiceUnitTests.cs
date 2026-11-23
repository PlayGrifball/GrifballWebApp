using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static GrifballWebApp.Application.Test.SeasonMatchUnitTestData;

namespace GrifballWebApp.Application.Test;

/// <summary>StaffStatFlagService (#548): the staff flags page's list and count, the match page's markers, and
/// dismissing a flag, on the fake IApplicationDbContext.</summary>
[TestFixture]
internal sealed class StaffStatFlagServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime FoundAt = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MatchID = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private FakeTimeProvider _time = null!;
    private StaffStatFlagService _staff = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 21, 0, 0, 0, TimeSpan.Zero));
        _staff = new StaffStatFlagService(Context, _time);
    }

    private RecordedGame SeedGame(string sourceKey, int? seasonID = null)
    {
        var game = RecordedGame.Seed(sourceKey, gameVersionID: Halo3, seasonID: seasonID);
        Context.RecordedGames.Add(game);
        return game;
    }

    [Test]
    public async Task OpenFlagsAsync_ReturnsOnlyOpenOnes_NewestFirst()
    {
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        var older = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "older", FoundAt);
        var newer = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "newer", FoundAt.AddHours(1));
        var dismissed = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "dismissed", FoundAt);
        dismissed.Dismiss(3, FoundAt);
        Context.StatFlags.AddRange(older, newer, dismissed);
        await Context.SaveChangesAsync();

        var flags = await _staff.OpenFlagsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags, Has.Count.EqualTo(2));
            Assert.That(flags[0].Reason, Is.EqualTo("newer"));
            Assert.That(flags[1].Reason, Is.EqualTo("older"));
        }
    }

    [Test]
    public async Task OpenFlagCountAsync_CountsOnlyOpenFlags()
    {
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        var open = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "open", FoundAt);
        var dismissed = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "dismissed", FoundAt);
        dismissed.Dismiss(null, FoundAt);
        Context.StatFlags.AddRange(open, dismissed);
        await Context.SaveChangesAsync();

        Assert.That(await _staff.OpenFlagCountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task OpenFlagsForSeasonMatchAsync_FindsFlagsOfEitherKindOfGame_WithTheirGameNumber()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.EnterByHand(new RecordedGameFacts(Halo3));
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        seeded.SeasonMatch.AddGame(game, 1);
        await Context.SaveChangesAsync();
        Context.Matches.Add(Domain.Halo.Match.Seed(MatchID));
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "of this match's game", FoundAt));
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, null, "of an unrelated match", FoundAt));
        await Context.SaveChangesAsync();

        var flags = await _staff.OpenFlagsForSeasonMatchAsync(seeded.SeasonMatch.SeasonMatchID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags, Has.Count.EqualTo(1));
            Assert.That(flags[0].Reason, Is.EqualTo("of this match's game"));
            Assert.That(flags[0].SeasonMatchID, Is.EqualTo(seeded.SeasonMatch.SeasonMatchID));
            Assert.That(flags[0].GameNumber, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task TheFlagsReturned_NameThePlayer_WhenTheRuleIsAboutOne()
    {
        var seeded = await SeedAsync(Context);
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, seeded.HomeUsers[0].Id, "a player's stat", FoundAt));
        await Context.SaveChangesAsync();

        var flags = await _staff.OpenFlagsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags[0].PlayerUserID, Is.EqualTo(seeded.HomeUsers[0].Id));
            Assert.That(flags[0].PlayerName, Is.EqualTo("GT" + HomeXbox[0]));
        }
    }

    [Test]
    public async Task TheFlagsReturned_NameNoPlayer_WhenTheirPlayerHasBeenMergedAway()
    {
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, 999, "a merged-away player's stat", FoundAt));
        await Context.SaveChangesAsync();

        var flags = await _staff.OpenFlagsAsync();

        Assert.That(flags[0].PlayerName, Is.Null);
    }

    [Test]
    public async Task TheFlagsReturned_ShowTheGamesDateAndTeams_WhenItsARecordedGameOutsideAnySeries()
    {
        // #548: an imported scrim, or a Late League matchmaking game, has no page to link to (no SeasonMatchID, no
        // MatchID): shown by its date and sides instead
        var game = RecordedGame.Seed("g1", gameVersionID: Halo3, playedAt: FoundAt.AddDays(-1));
        game.SeedSide(1, outcome: Outcomes.Won, score: 3, name: "Reds");
        game.SeedSide(2, outcome: Outcomes.Lost, score: 1, name: "Blues");
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "reason", FoundAt));
        await Context.SaveChangesAsync();

        var flags = await _staff.OpenFlagsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags[0].SeasonMatchID, Is.Null);
            Assert.That(flags[0].GamePlayedAt, Is.EqualTo(game.PlayedAt));
            Assert.That(flags[0].GameTeams, Is.EqualTo("Reds vs Blues"));
        }
    }

    [Test]
    public async Task TheFlagsReturned_ShowNoGameTeams_WhenTheGameIsInASeries()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.EnterByHand(new RecordedGameFacts(Halo3));
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        seeded.SeasonMatch.AddGame(game, 1);
        await Context.SaveChangesAsync();
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "reason", FoundAt));
        await Context.SaveChangesAsync();

        var flags = await _staff.OpenFlagsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags[0].SeasonMatchID, Is.Not.Null);
            Assert.That(flags[0].GameTeams, Is.Null);
            Assert.That(flags[0].GamePlayedAt, Is.Null);
        }
    }

    [Test]
    public async Task Dismiss_RecordsWhoAndWhen()
    {
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "reason", FoundAt);
        Context.StatFlags.Add(flag);
        await Context.SaveChangesAsync();

        await _staff.Dismiss(flag.StatFlagID);

        var dismissed = await Context.StatFlags.AsNoTracking().SingleAsync(f => f.StatFlagID == flag.StatFlagID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dismissed.IsOpen, Is.False);
            Assert.That(dismissed.DismissedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
        }
    }

    [Test]
    public void Dismiss_ThatDoesNotExist_Throws()
        => Assert.That(async () => await _staff.Dismiss(999), Throws.InstanceOf<StatFlagNotFoundException>());

    [Test]
    public async Task Dismiss_RecordsWhoSignedIn_WhenSomeoneIs()
    {
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "reason", FoundAt);
        Context.StatFlags.Add(flag);
        await Context.SaveChangesAsync();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.GetCurrentUserId().Returns(3);
        var staff = new StaffStatFlagService(Context, _time, currentUser);

        await staff.Dismiss(flag.StatFlagID);

        var dismissed = await Context.StatFlags.AsNoTracking().SingleAsync(f => f.StatFlagID == flag.StatFlagID);
        Assert.That(dismissed.DismissedByUserID, Is.EqualTo(3));
    }

    [Test]
    public async Task OpenFlagCountAsync_WithNoTimeProviderGiven_StillWorks()
    {
        var game = SeedGame("g1");
        await Context.SaveChangesAsync();
        Context.StatFlags.Add(StatFlag.Found(StatFlagRule.NegativeStat, null, game.RecordedGameID, null, null, "reason", FoundAt));
        await Context.SaveChangesAsync();

        Assert.That(await new StaffStatFlagService(Context).OpenFlagCountAsync(), Is.EqualTo(1));
    }
}
