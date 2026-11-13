using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// SetGamertagService's paths SetGamertagServiceUnitTests doesn't reach: the user given a gamertag during the lookup (a
/// race), a placeholder that can't be merged, and ReconcilePastGamertags (the account's past gamertags' placeholders
/// merged, claimed for a sysadmin when another account's history has the gamertag too, and the current gamertag's
/// namesake claimed). IUserMergeService is substituted: UserMergeServiceUnitTests covers the merge itself.
/// </summary>
[TestFixture]
internal sealed class SetGamertagServiceMoreUnitTests : ApplicationDbContextTests
{
    private const long Xuid = 111;
    private IGetsertXboxUserService _xboxService = null!;
    private IUserMergeService _mergeService = null!;
    private SetGamertagService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _xboxService = Substitute.For<IGetsertXboxUserService>();
        _mergeService = Substitute.For<IUserMergeService>();
        _service = new SetGamertagService(Context, _xboxService, _mergeService, new GamertagMatching(Context));
    }

    /// <summary>The signing-up user (1) and the Xbox account the lookup finds, saved, with its past gamertags.</summary>
    private async Task<XboxUser> SeedUserAndAccount(params string[] pastGamertags)
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        var xbox = XboxUser.FromXbox(Xuid, "GamerOne");
        foreach (var past in pastGamertags)
            _ = xbox.RecordPastGamertag(past, null, null, GamertagHistorySource.Manual);
        Context.XboxUsers.Add(xbox);
        await Context.SaveChangesAsync();
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));
        return xbox;
    }

    /// <summary>An import placeholder of a gamertag, untied to any Xbox account.</summary>
    private static User Placeholder(string gamertag, int id) => User.Seed(userName: $"historical:{gamertag}", normalizedUserName: $"HISTORICAL:{gamertag.ToUpperInvariant()}",
        displayName: gamertag, isDummyUser: true, id: id);

    private async Task<long?> SavedXboxUserIDOf(int userID)
    {
        Context.ChangeTracker.Clear();
        return await Context.Users.AsNoTracking().Where(u => u.Id == userID).Select(u => u.XboxUserID).SingleAsync();
    }

    [Test]
    public async Task SetGamertag_UserGivenAGamertagDuringTheLookup_IsRefused()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        var xbox = XboxUser.FromXbox(Xuid, "GamerOne");
        // Another request ties the (tracked) user to an account while the lookup runs
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(call =>
        {
            _ = user.TieToXbox(222L);
            return ((XboxUser?)xbox, (string?)null);
        });

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("Gamertag is already set for this user. Contact sysadmin if it needs to be changed"));
            Assert.That(await SavedXboxUserIDOf(1), Is.Null, "nothing saved");
        }
    }

    [Test]
    public async Task SetGamertag_HeldByAPlaceholderThatCantBeMerged_ReturnsTheConflictAndSavesNothing()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        var xbox = XboxUser.FromXbox(Xuid, "GamerOne");
        Context.XboxUsers.Add(xbox);
        Context.Users.Add(User.Seed(userName: "historical:GamerOne", displayName: "GamerOne", xboxUser: xbox, isDummyUser: true, id: 2));
        await Context.SaveChangesAsync();
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));
        var conflict = Task.FromException(new MergeConflictException("Both played the same game."));
        _mergeService.Merge(1, 2, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(conflict);

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("That gamertag's placeholder account could not be merged into yours: Both played the same game. Contact a commissioner."));
            Assert.That(await SavedXboxUserIDOf(1), Is.Null);
        }
    }

    [Test]
    public async Task SetGamertag_APastGamertagsPlaceholderFitsTheHistory_IsMerged()
    {
        await SeedUserAndAccount("OldTag");
        Context.Users.Add(Placeholder("OldTag", id: 2));
        await Context.SaveChangesAsync();

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(await SavedXboxUserIDOf(1), Is.EqualTo(Xuid));
            Assert.That(await Context.GamertagClaims.CountAsync(), Is.Zero);
        }
        await _mergeService.Received(1).Merge(1, 2,
            Arg.Is<MergeOptions>(o => o.Trigger == MergeTrigger.SetGamertag && o.Reason == "past gamertag OldTag" && o.TriggeredByUserID == 1), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_APastGamertagsPlaceholderCantBeMerged_ReturnsTheConflictAndSavesNothing()
    {
        await SeedUserAndAccount("OldTag");
        Context.Users.Add(Placeholder("OldTag", id: 2));
        await Context.SaveChangesAsync();
        var conflict = Task.FromException(new MergeConflictException("Both played the same game."));
        _mergeService.Merge(1, 2, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(conflict);

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("That gamertag's placeholder account could not be merged into yours: Both played the same game. Contact a commissioner."));
            Assert.That(await SavedXboxUserIDOf(1), Is.Null);
        }
    }

    [Test]
    public async Task SetGamertag_AnotherAccountsHistoryHasThePastGamertagToo_ClaimsItForASysadmin()
    {
        await SeedUserAndAccount("OldTag");
        var other = XboxUser.FromXbox(222, "SomeoneElse");
        _ = other.RecordPastGamertag("OldTag", null, null, GamertagHistorySource.Manual);
        Context.XboxUsers.Add(other);
        Context.Users.Add(Placeholder("OldTag", id: 2));
        await Context.SaveChangesAsync();

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        Context.ChangeTracker.Clear();
        var claim = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(claim.Kind, Is.EqualTo(GamertagClaimKind.SetGamertag));
            Assert.That(claim.XboxUserID, Is.EqualTo(Xuid));
            Assert.That(claim.Gamertag, Is.EqualTo("OldTag"));
            Assert.That(claim.PlaceholderUserID, Is.EqualTo(2));
            Assert.That(await SavedXboxUserIDOf(1), Is.EqualTo(Xuid));
        }
        await _mergeService.DidNotReceive().Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_APastGamertagWithNoPlaceholder_IsSkipped()
    {
        await SeedUserAndAccount("OldTag");

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(await SavedXboxUserIDOf(1), Is.EqualTo(Xuid));
            Assert.That(await Context.GamertagClaims.CountAsync(), Is.Zero);
        }
        await _mergeService.DidNotReceive().Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_ThePlaceholderOfTheCurrentGamertag_IsClaimedForASysadmin()
    {
        await SeedUserAndAccount();
        Context.Users.Add(Placeholder("GamerOne", id: 2));
        await Context.SaveChangesAsync();

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        Context.ChangeTracker.Clear();
        var claim = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(claim.Kind, Is.EqualTo(GamertagClaimKind.SetGamertag));
            Assert.That(claim.XboxUserID, Is.EqualTo(Xuid));
            Assert.That(claim.Gamertag, Is.EqualTo("GamerOne"));
            Assert.That(claim.PlaceholderUserID, Is.EqualTo(2));
        }
        await _mergeService.DidNotReceive().Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>());
    }
}
