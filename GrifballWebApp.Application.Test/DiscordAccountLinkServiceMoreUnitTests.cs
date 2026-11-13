using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>DiscordAccountLinkService's paths DiscordAccountLinkServiceUnitTests doesn't reach: a placeholder that can't be
/// merged (a claim for a sysadmin instead), and TryLinkAsync undoing a save that failed.</summary>
[TestFixture]
internal sealed class DiscordAccountLinkServiceMoreUnitTests : ApplicationDbContextTests
{
    private IUserMergeService _merges = null!;
    private DiscordAccountLinkService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _merges = Substitute.For<IUserMergeService>();
        _service = new DiscordAccountLinkService(Context, Substitute.For<ILogger<DiscordAccountLinkService>>(), _merges);
    }

    [TestCase("historical:Old Tag", "Shown Name", "Old Tag")]
    [TestCase("not-a-placeholder-name", "Shown Name", "Shown Name")]
    [TestCase("not-a-placeholder-name", null, "2")]
    public async Task LinkAsync_PlaceholderCantBeMerged_ClaimsItForASysadminAndLeavesTheLinkAlone(string placeholderUserName, string? placeholderDisplayName, string claimedGamertag)
    {
        var user = User.Seed(userName: "real", id: 1);
        var placeholder = User.Seed(userName: placeholderUserName, displayName: placeholderDisplayName, isDummyUser: true, discordUserID: 555, id: 2);
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(555, "OldName"));
        Context.Users.AddRange(user, placeholder);
        await Context.SaveChangesAsync();
        var conflict = Task.FromException(new MergeConflictException("both hold a title of the same season"));
        _merges.Merge(1, 2, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(conflict);

        var result = await _service.LinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "NewName");

        Context.ChangeTracker.Clear();
        var claim = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        var reloaded = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == 1);
        var discordUser = await Context.DiscordUsers.AsNoTracking().SingleAsync(d => d.DiscordUserID == 555);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.DiscordLinkedToAnotherUser));
            Assert.That(claim.Kind, Is.EqualTo(GamertagClaimKind.DiscordMatch));
            Assert.That(claim.Status, Is.EqualTo(GamertagClaimStatus.Pending));
            Assert.That(claim.ClaimantUserID, Is.EqualTo(1));
            Assert.That(claim.PlaceholderUserID, Is.EqualTo(2));
            Assert.That(claim.Gamertag, Is.EqualTo(claimedGamertag));
            Assert.That(reloaded.DiscordUserID, Is.Null, "the user isn't linked while a sysadmin decides");
            Assert.That(discordUser.DiscordUsername, Is.EqualTo("OldName"), "nothing renamed");
        }
        await _merges.Received(1).Merge(1, 2, Arg.Is<MergeOptions>(o => o.Trigger == MergeTrigger.DiscordMatch && o.TriggeredByUserID == 1), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TryLinkAsync_SaveFails_ReturnsFailedAndUndoesThePendingChanges()
    {
        var user = User.Seed(userName: "real", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        // A pending row the save refuses: its Discord link points at no DiscordUsers row (a foreign key)
        Context.Users.Add(User.Seed(userName: "orphan", discordUserID: 999, id: 3));

        var result = await _service.TryLinkAsync(user, DiscordAccountLinkService.DiscordProvider, "555", "NewName");

        var pending = Context.ChangeTracker.Entries().Count(e => e.State is EntityState.Added or EntityState.Modified);
        var userDiscordID = user.DiscordUserID;
        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.Failed));
            Assert.That(pending, Is.Zero, "added rows detached, modified ones set back");
            Assert.That(userDiscordID, Is.Null, "the user's link is set back");
            Assert.That(await Context.DiscordUsers.CountAsync(), Is.Zero);
            Assert.That(await Context.Users.CountAsync(), Is.EqualTo(1));
            Assert.That((await Context.Users.SingleAsync()).DiscordUserID, Is.Null);
        }
    }
}
