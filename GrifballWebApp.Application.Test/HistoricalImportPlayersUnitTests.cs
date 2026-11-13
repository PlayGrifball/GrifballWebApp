using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The players lists (HistoricalImportService.Players): a Discord ID given for two gamertags, linked to another
/// placeholder already, a signed-up user's own, a new placeholder's, a placeholder linked to another one; a Discord ID a
/// signed-up user has (no placeholder, both tied to Xbox accounts, a merge by the account's past gamertag, a merge that
/// conflicts and becomes a claim, a merge that fails the lists). The merge service is a substitute: what it is asked to
/// do is what these tests check (UserMergeServiceUnitTests covers the merge itself).
/// </summary>
[TestFixture]
internal sealed class HistoricalImportPlayersUnitTests : ApplicationDbContextTests
{
    private IUserMergeService _merges = null!;
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _merges = Substitute.For<IUserMergeService>();
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions()),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System, _merges);
    }

    private static HistoricalArchiveInput Players(params ArchivePlayerInput[] players) => new() { Seasons = new UnifiedArchive(), Players = [.. players] };

    private async Task<User> Placeholder(string gamertag, long? xboxUserID = null, long? discordUserID = null)
    {
        var placeholder = User.ImportPlaceholder(HistoricalKeys.DummyUserName(gamertag), gamertag, xboxUserID);
        if (discordUserID is long discord)
        {
            Context.DiscordUsers.Add(DiscordUser.FromDiscord(discord, $"discord{discord}"));
            placeholder.LinkDiscord(discord);
        }
        Context.Users.Add(placeholder);
        await Context.SaveChangesAsync();
        return placeholder;
    }

    /// <summary>A signed-up user with the Discord ID (and the Xbox account, when given).</summary>
    private async Task<User> Holder(long discordUserID, XboxUser? xbox = null)
    {
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(discordUserID, "RealDiscord"));
        var holder = User.Seed(userName: "real", discordUserID: discordUserID, xboxUser: xbox);
        Context.Users.Add(holder);
        await Context.SaveChangesAsync();
        return holder;
    }

    [Test]
    public async Task ADiscordIdGivenForTwoGamertags_IsNeitherLinkedNorMerged()
    {
        var alpha = await Placeholder("Alpha");

        var report = await _service.Import(Players(
            new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "42", Region = "East" },
            new ArchivePlayerInput { Gamertag = "Bravo", DiscordId = "42" }));

        var reread = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == alpha.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("Players lists: Discord ID 42 is given for more than one gamertag (Alpha, Bravo); nothing linked or merged automatically"));
            Assert.That(report.DiscordLinked, Is.Zero);
            Assert.That(reread.DiscordUserID, Is.Null);
            Assert.That(reread.RegionID, Is.EqualTo(Regions.EastNorthAmerica), "Its region is still filled in");
            Assert.That(report.RegionsSet, Is.EqualTo(1));
            Assert.That(await Context.Users.CountAsync(), Is.EqualTo(1), "No placeholder made for Bravo");
        }
    }

    [Test]
    public async Task ADiscordIdAnotherPlaceholderHas_IsLeftAsItIs()
    {
        await Placeholder("Alpha", discordUserID: 42);
        var bravo = await Placeholder("Bravo");

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Bravo", DiscordId = "42" }));

        var reread = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == bravo.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("Players lists: Discord ID 42 of Bravo is linked to the placeholder historical:Alpha; left as it is"));
            Assert.That(reread.DiscordUserID, Is.Null);
            Assert.That(report.DiscordLinked, Is.Zero);
        }
    }

    [Test]
    public async Task ASignedUpUsersGamertag_WithADiscordIdNobodyHas_IsLeftToTheirOwnSignIn()
    {
        var real = User.Seed(userName: "noah", xboxUser: XboxUser.FromXbox(5, "Alpha"));
        Context.Users.Add(real);
        await Context.SaveChangesAsync();

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "77" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("Players lists: Alpha (Discord ID 77) is noah's gamertag; their Discord is linked when they sign in with it"));
            Assert.That((await Context.Users.AsNoTracking().SingleAsync()).DiscordUserID, Is.Null);
            Assert.That(await Context.DiscordUsers.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task AGamertagWithNoPlaceholder_GetsOne_LinkedToTheDiscordId()
    {
        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Fresh Player", DiscordId = "55", DiscordUsername = "fresh" }));

        var made = await Context.Users.AsNoTracking().SingleAsync();
        var discord = await Context.DiscordUsers.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.DummyUsersCreated, report.DiscordLinked), Is.EqualTo((1, 1)));
            Assert.That((made.UserName, made.DiscordUserID), Is.EqualTo(("historical:Fresh Player", (long?)55)));
            Assert.That(discord.DiscordUserID, Is.EqualTo(55));
        }
    }

    [Test]
    public async Task APlaceholderLinkedToAnotherDiscordId_IsLeftAsItIs()
    {
        var alpha = await Placeholder("Alpha", discordUserID: 41);

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "42" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("Players lists: Alpha's placeholder has Discord ID 41, the list says 42; left as it is"));
            Assert.That((await Context.Users.AsNoTracking().SingleAsync(u => u.Id == alpha.Id)).DiscordUserID, Is.EqualTo(41));
            Assert.That(report.DiscordLinked, Is.Zero);
        }
    }

    [Test]
    public async Task AHeldDiscordId_ForAGamertagWithNoPlaceholder_IsTheHoldersRow()
    {
        var holder = await Holder(42);

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Nobody Yet", DiscordId = "42", Region = "East" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.DiscordMerged, report.DiscordClaimsCreated, report.RegionsSet), Is.EqualTo((0, 0, 1)));
            Assert.That((await Context.Users.AsNoTracking().SingleAsync(u => u.Id == holder.Id)).RegionID, Is.EqualTo(Regions.EastNorthAmerica), "The holder's region");
            await _merges.DidNotReceiveWithAnyArgs().Merge(default, default, default, default);
        }
    }

    [Test]
    public async Task AHeldDiscordId_WhosePlaceholderIsAnotherXboxAccounts_IsLeftForASysadmin()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(6, "Alpha Now"));
        await Context.SaveChangesAsync();
        await Placeholder("Alpha", xboxUserID: 6);
        await Holder(42, XboxUser.FromXbox(5, "Real Tag"));

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "42" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("Players lists: Alpha's placeholder is another Xbox account's than real's (Discord ID 42); left for a sysadmin"));
            Assert.That(report.DiscordMerged, Is.Zero);
            await _merges.DidNotReceiveWithAnyArgs().Merge(default, default, default, default);
        }
    }

    [Test]
    public async Task AHeldDiscordId_WhoseAccountHadTheGamertag_MergesThePlaceholderIntoTheHolder()
    {
        var placeholder = await Placeholder("Alpha");
        var holder = await Holder(42, XboxUser.FromXbox(5, "Renamed Since"));
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(5, "Alpha"));
        await Context.SaveChangesAsync();

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "42" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.DiscordMerged, Is.EqualTo(1));
            Assert.That(report.Errors, Is.Empty);
            await _merges.Received(1).Merge(holder.Id, placeholder.Id,
                Arg.Is<MergeOptions>(o => o.Trigger == MergeTrigger.DiscordMatch && o.Reason == "Discord ID match"), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task AHeldDiscordId_WhoseMergeConflicts_BecomesAClaimForASysadmin()
    {
        var placeholder = await Placeholder("Alpha");
        var holder = await Holder(42);
        _merges.Merge(holder.Id, placeholder.Id, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new MergeConflictException("both on a team in one season")));

        var report = await _service.Import(Players(new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "42" }));

        var claim = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("Players lists: Alpha's placeholder can't be merged into real (Discord ID 42): both on a team in one season"));
            Assert.That((report.DiscordMerged, report.DiscordClaimsCreated), Is.EqualTo((0, 1)));
            Assert.That((claim.Kind, claim.ClaimantUserID, claim.PlaceholderUserID, claim.Gamertag), Is.EqualTo((GamertagClaimKind.DiscordMatch, (int?)holder.Id, (int?)placeholder.Id, "Alpha")));
        }
    }

    [Test]
    public async Task AMergeThatFails_RollsTheListsBack_AndIsReported()
    {
        var placeholder = await Placeholder("Alpha");
        var holder = await Holder(42);
        _merges.Merge(holder.Id, placeholder.Id, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("the merge broke")));

        var report = await _service.Import(Players(
            new ArchivePlayerInput { Gamertag = "Bravo", DiscordId = "55" },
            new ArchivePlayerInput { Gamertag = "Alpha", DiscordId = "42" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.EqualTo(MergeBroke));
            Assert.That(report.DiscordLinked, Is.Zero, "Nothing of the lists is counted");
            Assert.That(await Context.Users.CountAsync(u => u.UserName == "historical:Bravo"), Is.Zero, "Bravo's placeholder, made before the failure, was rolled back");
        }
    }

    private static readonly string[] MergeBroke = ["Players lists: the merge broke"];
}
