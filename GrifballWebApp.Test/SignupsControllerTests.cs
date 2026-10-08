using System.Net;
using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Database.Models.User;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using SignupButtonInteractions = GrifballWebApp.Server.Signups.ButtonInteractions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupsControllerTests
{
    private GrifballContext _context;
    private SignupsController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new SignupsController(new SignupsService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> AddUser(string name = "player")
    {
        var user = new User { UserName = name, XboxUser = name == "player" ? new XboxUser { XboxUserID = 777, Gamertag = "PlayerGT" } : null };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public void Authorization_SignupAndUpsertRequirePlayerOrSysadmin()
    {
        var t = typeof(SignupsController);
        Assert.Multiple(() =>
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(t), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetSignup))!.Roles, Is.EqualTo("Player,Sysadmin"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.UpsertSignup))!.Roles, Is.EqualTo("Player,Sysadmin"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetSignups)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetTimeslots)), Is.Null);
        });
    }

    [Test]
    public async Task GetSignupDateInfo_UsesPersonIdClaim()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _context.SeasonSignups.Add(new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, TeamName = "x", Timestamp = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("PersonID", user.Id.ToString())], "Test")),
            },
        };

        var result = (SignupDateInfoDto[])((OkObjectResult)await _controller.GetSignupDateInfo(CancellationToken.None)).Value!;

        Assert.That(result.Single(r => r.SeasonID == season.SeasonID).IsSignedUp, Is.True);
    }

    [Test]
    public async Task GetSignups_ReturnsSignupsForSeason()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _context.SeasonSignups.Add(new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, TeamName = "x", Timestamp = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _controller.GetSignups(season.SeasonID, CancellationToken.None);

        Assert.That(result.Single().PersonName, Is.EqualTo("PlayerGT"));
    }

    [Test]
    public async Task UserScopedActions_WithoutUser_Forbid()
    {
        _controller.WithAnonymousUser();

        Assert.That(await _controller.GetTimeslots(1, 0, CancellationToken.None), Is.TypeOf<ForbidResult>());
        Assert.That(await _controller.GetSignup(1, 0, CancellationToken.None), Is.TypeOf<ForbidResult>());
        Assert.That(await _controller.UpsertSignup(new SignupRequestDto { SeasonID = 1 }, CancellationToken.None), Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task UserScopedActions_NonNumericId_Forbid()
    {
        _controller.WithUser("abc");

        Assert.That(await _controller.GetTimeslots(1, 0, CancellationToken.None), Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task UpsertSignup_UsesUserIdFromClaims_NotBody()
    {
        var user = await AddUser();
        var other = await AddUser("other");
        var season = await SignupTestData.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString(), "player", "Player");

        var result = await _controller.UpsertSignup(new SignupRequestDto { SeasonID = season.SeasonID, UserID = other.Id, TeamName = "Mine" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        var signup = await _context.SeasonSignups.SingleAsync();
        Assert.That(signup.UserID, Is.EqualTo(user.Id));

        var get = (SignupResponseDto)((OkObjectResult)await _controller.GetSignup(season.SeasonID, 0, CancellationToken.None)).Value!;
        Assert.That(get.TeamName, Is.EqualTo("Mine"));
        var slots = (TimeslotDto[])((OkObjectResult)await _controller.GetTimeslots(season.SeasonID, 0, CancellationToken.None)).Value!;
        Assert.That(slots, Is.Empty);
    }

    [Test]
    public async Task UpsertSignup_SignupsClosed_ReturnsBadRequest()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context, open: false);
        _controller.WithUser(user.Id.ToString());

        var result = await _controller.UpsertSignup(new SignupRequestDto { SeasonID = season.SeasonID }, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Signups are closed for this season"));
    }
}
