using GrifballWebApp.Database;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// The archive's players lists give gamertags' Discord IDs: a placeholder gets the ID when nobody has it (and the
/// person's Discord sign-in later merges it), a signed-up user with the ID gets the placeholder merged when the
/// gamertag fits them (a claim otherwise), an ID with two gamertags never merges, and a re-import changes nothing.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordArchiveImportTests
{
    private const long Discord = 386902644670595072;
    private GrifballContext _context;
    private HistoricalImportService _import;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _import = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions()), NullLogger<HistoricalImportService>.Instance,
            new GamertagMatching(_context), TimeProvider.System, new UserMergeService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private User Placeholder(string gamertag)
    {
        var user = User.Seed(userName: "historical:" + gamertag, normalizedUserName: ("historical:" + gamertag).ToUpperInvariant(), displayName: gamertag, isDummyUser: true);
        _context.Users.Add(user);
        return user;
    }

    private Task<HistoricalImportReport> Import(params ArchivePlayerInput[] players)
        => _import.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive(), Players = [.. players] });

    private static ArchivePlayerInput Player(string gamertag, long? discord = Discord, string? region = null)
        => new() { Gamertag = gamertag, DiscordId = discord?.ToString(), DiscordUsername = "ActiveHate", Region = region };

    [Test]
    public void TheLateLeagueMasterList_ReadsAsPlayers()
    {
        var file = System.Text.Json.JsonSerializer.Deserialize<LateLeagueMasterFile>("""
            { "sourceId": "late-league-aspence-main", "playerMasterlist": [
                { "Gamertag": "ActiveHate", "Discord": "ActiveHate", "Discord ID": "386902644670595072", "Location": "East" },
                { "Gamertag": "  ", "Discord": "nobody" },
                { "Gamertag": "No Id", "Discord": "x", "Discord ID": "n/a", "System": "PC" } ] }
            """, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var players = file.ToPlayers();

        Assert.That(players.Select(p => (p.Gamertag, p.DiscordUserId, p.Region, p.System, p.SourceKey)), Is.EqualTo(new[]
        {
            ("ActiveHate", (long?)Discord, (string?)"East", (string?)null, (string?)"late-league-aspence-main/Player Masterlist"),
            ("No Id", (long?)null, (string?)null, (string?)"PC", (string?)"late-league-aspence-main/Player Masterlist"),
        }));
    }

    [Test]
    public async Task NobodyHasTheId_ItGoesOnThePlaceholder_AndTheirDiscordSignInMergesIt()
    {
        var placeholder = Placeholder("ActiveHate");
        await _context.SaveChangesAsync();

        var report = await Import(Player("ActiveHate"));

        _context.ChangeTracker.Clear();
        Assert.That(report.DiscordLinked, Is.EqualTo(1));
        Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.DiscordUserID).SingleAsync(), Is.EqualTo(Discord));
        Assert.That(await _context.DiscordUsers.Select(d => d.DiscordUsername).SingleAsync(), Is.EqualTo("ActiveHate"));

        // The person signs in with Discord: the sign-in finds no account to join (a placeholder is none), makes theirs,
        // and linking it merges the placeholder in
        var links = new DiscordAccountLinkService(_context, NullLogger<DiscordAccountLinkService>.Instance, new UserMergeService(_context));
        Assert.That(await links.FindBotLinkedUserAsync(DiscordAccountLinkService.DiscordProvider, Discord.ToString()), Is.Null);
        var person = User.Seed(userName: "activehate");
        _context.Users.Add(person);
        await _context.SaveChangesAsync();

        var result = await links.LinkAsync(person, DiscordAccountLinkService.DiscordProvider, Discord.ToString(), "ActiveHate");

        _context.ChangeTracker.Clear();
        var merge = await _context.UserMerges.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.Linked));
            Assert.That(await _context.Users.Where(u => u.Id == person.Id).Select(u => u.DiscordUserID).SingleAsync(), Is.EqualTo(Discord));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.False);
            Assert.That((merge.Trigger, merge.Reason, merge.SourceUserID, merge.TargetUserID), Is.EqualTo((MergeTrigger.DiscordMatch, "Discord sign-in", placeholder.Id, person.Id)));
        });
    }

    [Test]
    public async Task ADiscordSignInWhosePlaceholderCantBeMerged_LeavesItToASysadmin()
    {
        var placeholder = Placeholder("ActiveHate");
        await _context.SaveChangesAsync();
        await Import(Player("ActiveHate"));
        var person = User.Seed(userName: "activehate");
        _context.Users.Add(person);
        await _context.SaveChangesAsync();
        // On different teams in one season: the merge refuses
        var season = await _context.AddSeason("Clash");
        await _context.AddTeam(season, "A", person, 1);
        await _context.AddTeam(season, "B", placeholder, 2);
        var links = new DiscordAccountLinkService(_context, NullLogger<DiscordAccountLinkService>.Instance, new UserMergeService(_context));

        var result = await links.LinkAsync(person, DiscordAccountLinkService.DiscordProvider, Discord.ToString(), "ActiveHate");

        _context.ChangeTracker.Clear();
        var claim = await _context.GamertagClaims.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.DiscordLinkedToAnotherUser));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.True, "Not merged");
            Assert.That((claim.Kind, claim.Status, claim.PlaceholderUserID, claim.NormalizedGamertag), Is.EqualTo((GamertagClaimKind.DiscordMatch, GamertagClaimStatus.Pending, (int?)placeholder.Id, HistoricalKeys.Normalize("ActiveHate"))));
            Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
        });
    }

    /// <summary>A dummy with the Discord ID whose user name isn't an import placeholder's (#301,
    /// <see cref="User.PlaceholderGamertagOf"/> is null): the claim goes by its display name, else its ID.</summary>
    [TestCase("Shown Name", TestName = "ADiscordSignInWhoseUnprefixedDummyCantBeMerged_ClaimsByItsDisplayName")]
    [TestCase(null, TestName = "ADiscordSignInWhoseUnprefixedDummyCantBeMerged_WithoutADisplayName_ClaimsByItsId")]
    public async Task ADiscordSignInWhoseUnprefixedDummyCantBeMerged_ClaimsByItsDisplayNameElseItsId(string? displayName)
    {
        var placeholder = Placeholder("ActiveHate");
        await _context.SaveChangesAsync();
        await Import(Player("ActiveHate"));
        var person = User.Seed(userName: "activehate");
        _context.Users.Add(person);
        await _context.SaveChangesAsync();
        var season = await _context.AddSeason("Clash");
        await _context.AddTeam(season, "A", person, 1);
        await _context.AddTeam(season, "B", placeholder, 2);
        var dummy = await _context.Users.SingleAsync(u => u.Id == placeholder.Id);
        dummy.UserName = "notaplaceholder";
        dummy.NormalizedUserName = "NOTAPLACEHOLDER";
        dummy.Rename(displayName);
        await _context.SaveChangesAsync();
        var links = new DiscordAccountLinkService(_context, NullLogger<DiscordAccountLinkService>.Instance, new UserMergeService(_context));

        var result = await links.LinkAsync(person, DiscordAccountLinkService.DiscordProvider, Discord.ToString(), "ActiveHate");

        _context.ChangeTracker.Clear();
        var claim = await _context.GamertagClaims.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.DiscordLinkedToAnotherUser));
            Assert.That((claim.PlaceholderUserID, claim.NormalizedGamertag),
                Is.EqualTo(((int?)placeholder.Id, HistoricalKeys.Normalize(displayName ?? placeholder.Id.ToString()))));
        });
    }

    [Test]
    public async Task NobodyHasTheIdOrThePlaceholder_ThePlaceholderIsMade()
    {
        var report = await Import(Player("Fresh Face", region: "Central"));

        var made = await _context.Users.SingleAsync();
        Assert.That((report.DummyUsersCreated, made.UserName, made.DiscordUserID, made.RegionID), Is.EqualTo((1, "historical:Fresh Face", (long?)Discord, (int?)Regions.CentralNorthAmerica)));
    }

    [Test]
    public async Task NobodyHasTheId_ButASignedUpUserHasTheGamertag_NothingIsLinked()
    {
        _context.Users.Add(User.Seed(userName: "real", xboxUser: XboxUser.FromXbox(1, "ActiveHate")));
        await _context.SaveChangesAsync();

        var report = await Import(Player("ActiveHate"));

        Assert.That(report.DiscordLinked, Is.Zero);
        Assert.That(await _context.Users.AnyAsync(u => u.DiscordUserID != null), Is.False);
        Assert.That(report.Warnings.Single(), Does.Contain("linked when they sign in"));
    }

    [Test]
    public async Task AUserHasTheId_AndTheGamertagFits_ThePlaceholderIsMergedIntoThem()
    {
        var placeholder = Placeholder("Active Hate");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "ActiveHate"));
        var real = User.Seed(userName: "real", discordUserID: Discord, xboxUser: XboxUser.FromXbox(1, "ActiveHate"));
        _context.Users.Add(real);
        await _context.SaveChangesAsync();

        var report = await Import(Player("Active Hate"));

        _context.ChangeTracker.Clear();
        var merge = await _context.UserMerges.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(report.DiscordMerged, Is.EqualTo(1));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.False);
            Assert.That((merge.Trigger, merge.Reason, merge.TargetUserID), Is.EqualTo((MergeTrigger.DiscordMatch, "Discord ID match", real.Id)));
        });
    }

    [Test]
    public async Task AnIdAlreadyOnAnotherPlaceholder_IsLeftAsItIs_WithAWarning()
    {
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        var other = Placeholder("First Name");
        other.LinkDiscord(Discord);
        var placeholder = Placeholder("ActiveHate");
        await _context.SaveChangesAsync();

        var report = await Import(Player("ActiveHate"));

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(report.Warnings, Has.Some.Contains("is linked to the placeholder historical:First Name"));
            Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.DiscordUserID).SingleAsync(), Is.Null);
            Assert.That(await _context.Users.Where(u => u.Id == other.Id).Select(u => u.DiscordUserID).SingleAsync(), Is.EqualTo(Discord));
        });
    }

    [Test]
    public async Task APlaceholderWithAnotherDiscordId_IsLeftAsItIs_WithAWarning()
    {
        const long elsewhere = 111111111111111111;
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(elsewhere, "y"));
        var placeholder = Placeholder("ActiveHate");
        placeholder.LinkDiscord(elsewhere);
        await _context.SaveChangesAsync();

        var report = await Import(Player("ActiveHate"));

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(report.Warnings, Has.Some.Contains($"placeholder has Discord ID {elsewhere}, the list says {Discord}"));
            Assert.That(report.DiscordLinked, Is.Zero);
            Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.DiscordUserID).SingleAsync(), Is.EqualTo(elsewhere));
        });
    }

    [Test]
    public async Task APlaceholderOfAnotherXboxAccount_IsNotMergedIntoTheUserWithTheId()
    {
        var placeholder = Placeholder("ActiveHate");
        placeholder.TieToXbox(XboxUser.FromXbox(2, "ActiveHate"));
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        _context.Users.Add(User.Seed(userName: "real", discordUserID: Discord, xboxUser: XboxUser.FromXbox(1, "Someone")));
        await _context.SaveChangesAsync();

        var report = await Import(Player("ActiveHate"));

        Assert.Multiple(async () =>
        {
            Assert.That(report.Warnings, Has.Some.Contains("is another Xbox account's than real's"));
            Assert.That((report.DiscordMerged, report.DiscordClaimsCreated), Is.EqualTo((0, 0)));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.True);
        });
    }

    [Test]
    public async Task APlaceholderThatCantBeMerged_IntoTheUserWithTheId_BecomesAClaim()
    {
        var placeholder = Placeholder("Old Name");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        var real = User.Seed(userName: "real", discordUserID: Discord);
        _context.Users.Add(real);
        await _context.SaveChangesAsync();
        // On different teams in one season: the merge refuses
        var season = await _context.AddSeason("Clash");
        await _context.AddTeam(season, "A", real, 1);
        await _context.AddTeam(season, "B", placeholder, 2);

        var report = await Import(Player("Old Name"));

        Assert.Multiple(async () =>
        {
            Assert.That(report.Warnings, Has.Some.Contains("can't be merged into real"));
            Assert.That((report.DiscordMerged, report.DiscordClaimsCreated), Is.EqualTo((0, 1)));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.True);
            Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task AUserWithNoXboxHasTheId_ThePlaceholderIsMergedIntoThem()
    {
        var placeholder = Placeholder("Old Name");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        _context.Users.Add(User.Seed(userName: "real", discordUserID: Discord));
        await _context.SaveChangesAsync();

        var report = await Import(Player("Old Name"));

        Assert.That(report.DiscordMerged, Is.EqualTo(1));
        Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.False);
    }

    [Test]
    public async Task AUserHasTheId_ButTheGamertagIsNotTheirs_ASysadminDecides()
    {
        var placeholder = Placeholder("Someone Else");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        var real = User.Seed(userName: "real", discordUserID: Discord, xboxUser: XboxUser.FromXbox(1, "ActiveHate"));
        _context.Users.Add(real);
        await _context.SaveChangesAsync();

        var report = await Import(Player("Someone Else"));
        var again = await Import(Player("Someone Else"));

        var claim = await _context.GamertagClaims.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((report.DiscordMerged, report.DiscordClaimsCreated, again.DiscordClaimsCreated), Is.EqualTo((0, 1, 0)));
            Assert.That((claim.Kind, claim.Status, claim.ClaimantUserID, claim.PlaceholderUserID, claim.XboxUserID),
                Is.EqualTo((GamertagClaimKind.DiscordMatch, GamertagClaimStatus.Pending, (int?)real.Id, (int?)placeholder.Id, (long?)1)));
            Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task ApprovingTheDiscordClaim_MergesIntoTheUser()
    {
        var placeholder = Placeholder("Someone Else");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        var real = User.Seed(userName: "real", discordUserID: Discord, xboxUser: XboxUser.FromXbox(1, "ActiveHate"));
        _context.Users.Add(real);
        await _context.SaveChangesAsync();
        await Import(Player("Someone Else"));
        var claim = await _context.GamertagClaims.SingleAsync();

        await new GamertagClaimService(_context, new UserMergeService(_context)).Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        Assert.That((await _context.UserMerges.SingleAsync()).Trigger, Is.EqualTo(MergeTrigger.DiscordMatch));
        Assert.That(await _context.XboxGamertagHistory.Select(h => h.Gamertag).SingleAsync(), Is.EqualTo("Someone Else"));
    }

    [Test]
    public async Task AnIdWithTwoGamertags_NeverLinksOrMerges()
    {
        var a = Placeholder("Tag Alpha");
        var b = Placeholder("Tag Bravo");
        await _context.SaveChangesAsync();

        var report = await Import(Player("Tag Alpha"), Player("Tag Bravo"));

        Assert.That(report.DiscordLinked, Is.Zero);
        Assert.That(await _context.Users.AnyAsync(u => u.DiscordUserID != null), Is.False);
        Assert.That(report.Warnings, Has.One.Contains("more than one gamertag"));
    }

    [Test]
    public async Task AnIdWithTwoGamertags_OfAKnownUser_MakesClaimsNotMerges()
    {
        Placeholder("Tag Alpha");
        Placeholder("Tag Bravo");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(Discord, "x"));
        _context.Users.Add(User.Seed(userName: "real", discordUserID: Discord));
        await _context.SaveChangesAsync();

        var report = await Import(Player("Tag Alpha"), Player("Tag Bravo"));

        Assert.That((report.DiscordMerged, report.DiscordClaimsCreated), Is.EqualTo((0, 2)));
    }

    [Test]
    public async Task OneGamertagWrittenTwoWays_IsNoConflict()
    {
        var placeholder = Placeholder("Mr Epic Beast");
        await _context.SaveChangesAsync();

        var report = await Import(Player("Mr Epic Beast"), Player("Mr_Epic_Beast"));

        Assert.That(report.Warnings, Has.None.Contains("more than one gamertag"));
        Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.DiscordUserID).SingleAsync(), Is.EqualTo(Discord));
    }

    [Test]
    public async Task AReImport_ChangesNothing()
    {
        Placeholder("ActiveHate");
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(2, "x"));
        var real = User.Seed(userName: "real", discordUserID: 2, xboxUser: XboxUser.FromXbox(1, "Real Tag"));
        _context.Users.Add(real);
        Placeholder("Real Tag");
        await _context.SaveChangesAsync();
        var players = new[] { Player("ActiveHate", region: "East"), Player("Real Tag", discord: 2), Player("No Discord", discord: null) };
        await Import(players);
        var users = await _context.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.DiscordUserID, u.RegionID }).ToListAsync();

        var again = await Import(players);

        Assert.Multiple(async () =>
        {
            Assert.That((again.DiscordLinked, again.DiscordMerged, again.DiscordClaimsCreated, again.RegionsSet, again.DummyUsersCreated), Is.EqualTo((0, 0, 0, 0, 0)));
            Assert.That(await _context.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.DiscordUserID, u.RegionID }).ToListAsync(), Is.EqualTo(users));
            Assert.That(await _context.UserMerges.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ThePlayersFile_IsReadByTheUpload()
    {
        // The upload reads no source: none is needed
        var controller = new HistoricalImportController(new HistoricalImportJobs(_context, new HistoricalImportSignal()), null!);
        static IFormFile File(string json)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "file.json");
        }

        var result = await controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None,
            File("""{ "sourceId": "ll", "playerMasterlist": [ { "Gamertag": "ActiveHate", "Discord ID": "386902644670595072", "Location": "East" } ] }"""));

        Assert.That(result, Is.TypeOf<AcceptedAtActionResult>());
        var payload = await _context.HistoricalImportJobs.Select(j => j.Payload).SingleAsync();
        var input = await HistoricalArchiveInput.ReadAsync<HistoricalArchiveInput>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload!)));
        Assert.That(input.Players!.Select(p => (p.Gamertag, p.DiscordUserId, p.Region)), Is.EqualTo(new[] { ("ActiveHate", (long?)Discord, (string?)"East") }));
    }
}
