using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>DiscordAccountLinkService against the fake IApplicationDbContext: tying "Sign in with Discord" to the
/// bot's link. IUserMergeService is substituted: the merge itself is UserMergeServiceUnitTests', this file only
/// checks that LinkAsync asks for one when a placeholder holds the id.</summary>
[TestFixture]
internal sealed class DiscordAccountLinkServiceUnitTests : ApplicationDbContextTests
{
    private IUserMergeService _merges = null!;
    private DiscordAccountLinkService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _merges = Substitute.For<IUserMergeService>();
        _service = new DiscordAccountLinkService(Context, Substitute.For<ILogger<DiscordAccountLinkService>>(), _merges);
    }

    [TestCase("Discord", "555", true, 555)]
    [TestCase("Google", "555", false, 0)]
    [TestCase("Discord", "not-a-number", false, 0)]
    public void TryGetDiscordUserId_OnlyADiscordProviderWithANumericKeyParses(string provider, string key, bool expected, long expectedId)
    {
        var parsed = DiscordAccountLinkService.TryGetDiscordUserId(provider, key, out var id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.EqualTo(expected));
            Assert.That(id, Is.EqualTo(expectedId));
        }
    }

    [Test]
    public async Task FindBotLinkedUserAsync_NotDiscord_ReturnsNull()
    {
        var found = await _service.FindBotLinkedUserAsync("Google", "555");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task FindBotLinkedUserAsync_RealUserLinkedToTheId_ReturnsThem()
    {
        Context.Users.Add(User.Seed(userName: "real", discordUserID: 555, id: 1));
        await Context.SaveChangesAsync();

        var found = await _service.FindBotLinkedUserAsync(DiscordAccountLinkService.DiscordProvider, "555");

        Assert.That(found!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task FindBotLinkedUserAsync_OnlyAPlaceholderHoldsTheId_ReturnsNull()
    {
        Context.Users.Add(User.Seed(userName: "historical:placeholder", isDummyUser: true, discordUserID: 555, id: 1));
        await Context.SaveChangesAsync();

        var found = await _service.FindBotLinkedUserAsync(DiscordAccountLinkService.DiscordProvider, "555");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task LinkAsync_NotDiscord_ReturnsNotDiscord()
    {
        var user = User.Seed(userName: "real", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.LinkAsync(user, "Google", "555", "name");

        Assert.That(result, Is.EqualTo(DiscordLinkResult.NotDiscord));
    }

    [Test]
    public async Task LinkAsync_FreshLink_CreatesTheDiscordUserRowAndLinks()
    {
        var user = User.Seed(userName: "real", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "NewName");

        var reloadedUser = await Context.Users.SingleAsync(u => u.Id == 1);
        var discordUser = await Context.DiscordUsers.SingleAsync(d => d.DiscordUserID == 555);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.Linked));
            Assert.That(reloadedUser.DiscordUserID, Is.EqualTo(555));
            Assert.That(discordUser.DiscordUsername, Is.EqualTo("NewName"));
        }
    }

    [Test]
    public async Task LinkAsync_ExistingDiscordUserRow_RenamesItRatherThanDuplicating()
    {
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(555, "OldName"));
        var user = User.Seed(userName: "real", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "FreshName");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.DiscordUsers.CountAsync(), Is.EqualTo(1));
            Assert.That((await Context.DiscordUsers.SingleAsync()).DiscordUsername, Is.EqualTo("FreshName"));
        }
    }

    [Test]
    public async Task LinkAsync_AlreadyLinkedToTheSameId_ReturnsAlreadyLinked()
    {
        var user = User.Seed(userName: "real", discordUserID: 555, id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "name");

        Assert.That(result, Is.EqualTo(DiscordLinkResult.AlreadyLinked));
    }

    [Test]
    public async Task LinkAsync_UserLinkedToADifferentId_ReturnsUserLinkedToOtherDiscord()
    {
        var user = User.Seed(userName: "real", discordUserID: 111, id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "name");

        var reloaded = await Context.Users.SingleAsync(u => u.Id == 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.UserLinkedToOtherDiscord));
            Assert.That(reloaded.DiscordUserID, Is.EqualTo(111), "the existing link must not have moved");
        }
    }

    [Test]
    public async Task LinkAsync_IdAlreadyLinkedToAnotherRealUser_ReturnsDiscordLinkedToAnotherUser()
    {
        Context.Users.Add(User.Seed(userName: "other", discordUserID: 555, id: 2));
        var user = User.Seed(userName: "real", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "name");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.DiscordLinkedToAnotherUser));
            Assert.That((await Context.Users.SingleAsync(u => u.Id == 1)).DiscordUserID, Is.Null);
        }
    }

    [Test]
    public async Task LinkAsync_PlaceholderHoldsTheId_MergesItIntoTheSigningInUser()
    {
        var user = User.Seed(userName: "real", id: 1);
        var placeholder = User.Seed(userName: "historical:placeholder", isDummyUser: true, discordUserID: 555, id: 2);
        Context.Users.AddRange(user, placeholder);
        await Context.SaveChangesAsync();

        var result = await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "NewName");

        Assert.That(result, Is.EqualTo(DiscordLinkResult.Linked));
        await _merges.Received(1).Merge(1, 2, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TryLinkAsync_DelegatesToLinkAsync()
    {
        var user = User.Seed(userName: "real", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.TryLinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "NewName");

        Assert.That(result, Is.EqualTo(DiscordLinkResult.Linked));
    }

    [Test]
    public async Task InTransactionAsync_WorkReturnsTrue_Commits()
    {
        var result = await _service.InTransactionAsync(async () =>
        {
            Context.Seasons.Add(Season.Seed(seasonName: "S1"));
            await Context.SaveChangesAsync();
            return true;
        });

        Assert.That(result, Is.True);
        Assert.That(await Context.Seasons.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task InTransactionAsync_WorkReturnsFalse_RollsBack()
    {
        var result = await _service.InTransactionAsync(async () =>
        {
            Context.Seasons.Add(Season.Seed(seasonName: "S1"));
            await Context.SaveChangesAsync();
            return false;
        });

        Assert.That(result, Is.False);
        Assert.That(await Context.Seasons.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task InTransactionAsync_WorkThrows_RollsBackAndRethrows()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.InTransactionAsync(() => throw new InvalidOperationException("boom")));

        Assert.That(ex!.Message, Is.EqualTo("boom"));
    }
}
