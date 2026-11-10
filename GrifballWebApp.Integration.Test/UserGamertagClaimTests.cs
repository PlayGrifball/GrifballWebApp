using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Security.Claims;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Infrastructure.Historical;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A user claims a past gamertag's placeholder from their profile (owner decision E); a sysadmin approves (an audited
/// merge, UserClaim) or rejects it with a reason the user sees.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserGamertagClaimTests
{
    private GrifballContext _context;
    private FakeTimeProvider _time;
    private UserGamertagClaimService _service;
    private GamertagClaimService _sysadmin;
    private User _user;
    private User _placeholder;
    private Season _season;
    private static readonly string[] expected = new[] { "Winter League 2019 (2019)" };

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _service = New(5);
        _sysadmin = new GamertagClaimService(_context, new UserMergeService(_context, _time));
        _user = User.Seed(userName: "me", xboxUser: XboxUser.FromXbox(10, "Me Now"));
        _placeholder = User.Seed(userName: "historical:Old Me", normalizedUserName: "HISTORICAL:OLD ME", displayName: "Old Me", isDummyUser: true);
        _season = Season.Seed(seasonName: "Winter League 2019", historicalSourceID: "wl19/Stats").WithDates(seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.AddRange(_user, _placeholder, _season);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Me", Kills = 7 }));
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private UserGamertagClaimService New(int perDay)
        => new(_context, new GamertagMatching(_context), _time, Options.Create(new UserClaimOptions { SubmissionsPerDay = perDay }));

    private static SubmitGamertagClaimRequest Request(string gamertag = "Old Me") => new()
    {
        Gamertag = gamertag,
        FirstSeen = new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        LastSeen = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Test]
    public async Task Preview_ShowsThePlaceholderAndItsSeasons()
    {
        var preview = await _service.Preview(_user.Id, " old  me ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((preview.Gamertag, preview.PlaceholderUserID, preview.PlaceholderName, preview.Refusal), Is.EqualTo(("old me", (int?)_placeholder.Id, "Old Me", (string?)null)));
            Assert.That(preview.Seasons.Select(s => $"{s.SeasonName} ({s.Year})"), Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task Preview_OfNothing_SaysWhy()
    {
        Assert.That((await _service.Preview(_user.Id, "  ")).Refusal, Is.EqualTo("Give the gamertag"));
        var none = await _service.Preview(_user.Id, "Nobody");
        Assert.That((none.PlaceholderUserID, none.Refusal), Is.EqualTo(((int?)null, "No past records use that gamertag")));
    }

    [Test]
    public async Task Submit_MakesAPendingClaim_TheUserSeesOnTheirProfile()
    {
        var claim = await _service.Submit(_user.Id, Request());

        var mine = await _service.ClaimsOf(_user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((claim.Kind, claim.Status, claim.ClaimantUserID, claim.PlaceholderUserID, claim.XboxUserID), Is.EqualTo(("UserClaim", "Pending", (int?)_user.Id, (int?)_placeholder.Id, (long?)10)));
            Assert.That(mine.Select(c => c.GamertagClaimID), Is.EqualTo(new[] { claim.GamertagClaimID }));
        }
    }

    [Test]
    public async Task Submit_TheSameGamertagTwice_IsRefused()
    {
        await _service.Submit(_user.Id, Request());

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(_user.Id, Request("OLD ME")));

        Assert.That(ex!.Message, Is.EqualTo("You have claimed that gamertag already"));
    }

    [Test]
    public async Task Submit_AnotherRealUsersCurrentGamertag_IsRefused()
    {
        _context.Users.Add(User.Seed(userName: "other", xboxUser: XboxUser.FromXbox(20, "Old Me")));
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(_user.Id, Request()));

        Assert.That(ex!.Message, Does.StartWith("That gamertag belongs to another user's Xbox account"));
    }

    [Test]
    public async Task Submit_AnotherRealUsersPastGamertag_IsRefused()
    {
        _context.Users.Add(User.Seed(userName: "other", xboxUser: XboxUser.FromXbox(20, "Other Now")));
        await _context.SaveChangesAsync();
        (await _context.XboxUsers.WithPastGamertags().SingleAsync(x => x.XboxUserID == 20)).RecordPastGamertag("Old Me", null, null, GamertagHistorySource.Manual);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(_user.Id, Request()));

        Assert.That(ex!.Message, Does.StartWith("That gamertag was another user's Xbox account's"));
    }

    [Test]
    public async Task Submit_APlaceholdersXboxAccountsGamertag_IsAllowed()
    {
        // An Xbox account nobody signed up with is no other user
        _context.Users.Add(User.Seed(userName: "historical:Old Me:20", isDummyUser: true, xboxUser: XboxUser.FromXbox(20, "Old Me")));
        await _context.SaveChangesAsync();

        Assert.That((await _service.Submit(_user.Id, Request())).Status, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task Submit_IsRateLimited()
    {
        _context.Users.AddRange(
            User.Seed(userName: "historical:A", normalizedUserName: "HISTORICAL:A", isDummyUser: true),
            User.Seed(userName: "historical:B", normalizedUserName: "HISTORICAL:B", isDummyUser: true));
        await _context.SaveChangesAsync();
        var service = New(perDay: 2);
        await service.Submit(_user.Id, Request("A"));
        await service.Submit(_user.Id, Request("B"));

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => service.Submit(_user.Id, Request()));
        _time.Advance(TimeSpan.FromDays(1.5));
        var later = await service.Submit(_user.Id, Request());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("2 claims a day"));
            Assert.That(later.Status, Is.EqualTo("Pending"));
        }
    }

    [Test]
    public async Task Submit_BadInput_IsRefused()
    {
        Assert.That((await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(_user.Id, Request(" "))))!.Message, Is.EqualTo("Give the gamertag"));
        var backwards = Request();
        backwards.FirstSeen = backwards.LastSeen!.Value.AddDays(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(_user.Id, backwards)))!.Message, Does.Contain("after its end"));
            Assert.That((await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(_placeholder.Id, Request())))!.Message, Does.Contain("signed-up user"));
        }
    }

    [Test]
    public async Task Approve_MergesThePlaceholderIntoTheClaimant_AndRecordsTheGamertag()
    {
        var claim = await _service.Submit(_user.Id, Request());

        await _sysadmin.Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        var merge = await _context.UserMerges.SingleAsync();
        var history = await _context.Set<XboxGamertagHistory>().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((merge.Trigger, merge.Reason, merge.TargetUserID), Is.EqualTo((MergeTrigger.UserClaim, $"claim {claim.GamertagClaimID}", _user.Id)));
            Assert.That((history.XboxUserID, history.Gamertag, history.FirstSeen, history.LastSeen), Is.EqualTo((10L, "Old Me", Request().FirstSeen, Request().LastSeen)));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_user.Id));
            Assert.That((await _service.ClaimsOf(_user.Id)).Single().Status, Is.EqualTo("Approved"));
        }
    }

    [Test]
    public async Task Approve_ForAClaimantWithNoXbox_RecordsNoHistory()
    {
        var noXbox = User.Seed(userName: "noxbox");
        _context.Users.Add(noXbox);
        await _context.SaveChangesAsync();
        var claim = await _service.Submit(noXbox.Id, Request());

        await _sysadmin.Approve(claim.GamertagClaimID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Set<XboxGamertagHistory>().AnyAsync(), Is.False);
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(noXbox.Id));
        }
    }

    [Test]
    public async Task Reject_WithAReason_TheClaimantSeesIt_AndMayClaimAgain()
    {
        var claim = await _service.Submit(_user.Id, Request());

        await _sysadmin.Reject(claim.GamertagClaimID, "  Not you, that was Bob  ");

        var mine = (await _service.ClaimsOf(_user.Id)).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((mine.Status, mine.RejectionReason), Is.EqualTo(("Rejected", "Not you, that was Bob")));
            Assert.That((await _service.Submit(_user.Id, Request())).Status, Is.EqualTo("Pending"));
        }
    }

    [Test]
    public async Task TheClaimsPage_ListsUserClaims_WithTheClaimant()
    {
        await _service.Submit(_user.Id, Request());

        var listed = (await _sysadmin.List(GamertagClaimStatus.Pending)).Single();

        Assert.That((listed.Kind, listed.ClaimantName, listed.UserID, listed.PlaceholderSeasons.Count), Is.EqualTo(("UserClaim", "me", (int?)_user.Id, 1)));
    }

    [Test]
    public async Task MergingTheClaimant_MovesTheirClaims()
    {
        await _service.Submit(_user.Id, Request());
        var other = User.Seed(userName: "other");
        _context.Users.Add(other);
        await _context.SaveChangesAsync();

        await new UserMergeService(_context).Merge(other.Id, _user.Id);

        Assert.That(await _context.GamertagClaims.Select(c => c.ClaimantUserID).SingleAsync(), Is.EqualTo(other.Id));
    }

    private MyProfileController Controller(int? userId)
    {
        var controller = new MyProfileController(_context, _service);
        var identity = userId is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], "test");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return controller;
    }

    [Test]
    public async Task Controller_PreviewsSubmitsAndLists_ForTheSignedInUser()
    {
        var controller = Controller(_user.Id);

        var preview = await controller.PreviewClaim("Old Me", CancellationToken.None);
        var claim = await controller.ClaimGamertag(Request(), CancellationToken.None);
        var mine = await controller.GamertagClaims(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Value!.PlaceholderUserID, Is.EqualTo(_placeholder.Id));
            Assert.That(claim.Value!.Status, Is.EqualTo("Pending"));
            Assert.That(mine.Value, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Controller_WithNoUser_IsUnauthorized()
    {
        var controller = Controller(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await controller.PreviewClaim("Old Me", CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
            Assert.That((await controller.ClaimGamertag(Request(), CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
            Assert.That((await controller.GamertagClaims(CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
            Assert.That(await controller.SetRegion(new SetRegionRequest { RegionID = 1 }, CancellationToken.None), Is.TypeOf<UnauthorizedResult>());
        }
    }

    [Test]
    public async Task RejectClaim_PassesTheReason()
    {
        var claim = await _service.Submit(_user.Id, Request());
        var controller = new GamertagClaimsController(_sysadmin,
            new XuidLookupService(_context, NSubstitute.Substitute.For<Surprenant.Grunt.Core.IHaloInfiniteClient>(), new GamertagMatching(_context), Options.Create(new XuidLookupOptions()), _time),
            new XuidLookupSignal());

        await controller.RejectClaim(claim.GamertagClaimID, new RejectGamertagClaimRequest { Reason = "No" }, CancellationToken.None);

        Assert.That((await _service.ClaimsOf(_user.Id)).Single().RejectionReason, Is.EqualTo("No"));
    }
}
