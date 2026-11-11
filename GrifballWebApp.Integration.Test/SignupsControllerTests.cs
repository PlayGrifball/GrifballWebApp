using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using User = GrifballWebApp.Domain.Players.User;
using Team = GrifballWebApp.Domain.League.Team;
using GrifballWebApp.Application.Signups;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupsControllerTests
{
    private GrifballContext _context;
    private SignupsController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _controller = new SignupsController(new SignupsService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> AddUser(string name = "player")
    {
        var user = User.Seed(userName: name, xboxUser: name == "player" ? XboxUser.FromXbox(777, "PlayerGT") : null);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task GetSignups_ShowsTheDisplayName_NeverTheLoginName()
    {
        // Public: a player with only a display name is listed by it; one with no name to show by none, not their login
        var shown = User.Seed(userName: "login-shown", displayName: "Shown Name");
        var nameless = User.Seed(userName: "login-nameless");
        _context.Users.AddRange(shown, nameless);
        await _context.SaveChangesAsync();
        var season = await SignupTestData.OpenSeason(_context);
        _context.SeasonSignups.AddRange(SeasonSignup.Seed(season.SeasonID, shown.Id, timestamp: DateTime.UtcNow, teamName: "a"),
            SeasonSignup.Seed(season.SeasonID, nameless.Id, timestamp: DateTime.UtcNow, teamName: "b"));
        await _context.SaveChangesAsync();

        var result = await _controller.GetSignups(season.SeasonID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Single(r => r.UserID == shown.Id).PersonName, Is.EqualTo("Shown Name"));
            Assert.That(result.Single(r => r.UserID == nameless.Id).PersonName, Is.Empty);
            Assert.That(result.Select(r => r.PersonName), Has.None.StartsWith("login-"));
        }
    }

    [Test]
    public async Task GetSignups_ReturnsSignupsForSeason()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _context.SeasonSignups.Add(SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: DateTime.UtcNow, teamName: "x"));
        await _context.SaveChangesAsync();

        var result = await _controller.GetSignups(season.SeasonID, CancellationToken.None);

        Assert.That(result.Single().PersonName, Is.EqualTo("PlayerGT"));
    }

    [Test]
    public async Task UpsertSignup_UsesUserIdFromClaims_NotBody()
    {
        var user = await AddUser();
        var other = await AddUser("other");
        var season = await SignupTestData.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString(), "player", "Player");

        var result = await _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID, UserID = other.Id, TeamName = "Mine" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var signup = await _context.SeasonSignups.SingleAsync();
        Assert.That(signup.UserID, Is.EqualTo(user.Id));

        var get = (SignupResponseDto)((OkObjectResult)await _controller.GetSignup(season.SeasonID, 0, CancellationToken.None)).Value!;
        Assert.That(get.TeamName, Is.EqualTo("Mine"));
        var slots = (await _controller.GetTimeslots(season.SeasonID, 0, CancellationToken.None)).Value!;
        Assert.That(slots, Is.Empty);
    }

    [Test]
    public async Task UpsertSignup_SignupsClosed_IsRefused()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context, open: false);
        _controller.WithUser(user.Id.ToString());

        // GlobalExceptionHandler answers it as a 400 ProblemDetails
        Assert.That(() => _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID }, CancellationToken.None),
            Throws.InstanceOf<SignupsClosedException>());
    }

    [Test]
    public async Task UpsertSignup_CaptainTeamNameTooLong_IsRefused_AndSavesNothing()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID, WillCaptain = true, TeamName = new string('x', Team.TeamNameMaxLength + 1) }, CancellationToken.None),
                Throws.InstanceOf<SignupTeamNameTooLongException>().With.Message.EqualTo("Team name must be 30 characters or fewer"));
            Assert.That(await _context.SeasonSignups.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task UpsertSignup_CaptainTeamNameAtTheLimit_IsSavedWhole()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString());
        var name = new string('x', Team.TeamNameMaxLength);

        var result = await _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID, WillCaptain = true, TeamName = name }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That((await _context.SeasonSignups.SingleAsync()).TeamName, Is.EqualTo(name));
        }
    }

    [Test]
    public async Task UpsertSignup_CaptainTeamNameAtTheLimitOnceTrimmed_IsSavedTrimmed()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString());
        var name = new string('x', Team.TeamNameMaxLength);

        var result = await _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID, WillCaptain = true, TeamName = "  " + name + " " }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That((await _context.SeasonSignups.SingleAsync()).TeamName, Is.EqualTo(name));
        }
    }

    [Test]
    public async Task UpsertSignup_NotCaptain_LongOldTeamName_DoesNotBlockTheSignup()
    {
        var user = await AddUser();
        var season = await SignupTestData.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString());

        var result = await _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID, WillCaptain = false, TeamName = new string('x', 40) }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task UpsertSignup_WithoutAGamertag_IsRefusedSayingHowToSetOne()
    {
        var user = await AddUser("no-gamertag");
        var season = await SignupTestData.OpenSeason(_context, open: true);
        _controller.WithUser(user.Id.ToString());

        Assert.That(() => _controller.UpsertSignup(season.SeasonID, new SignupRequestDto { SeasonID = season.SeasonID }, CancellationToken.None),
            Throws.InstanceOf<SignupNeedsGamertagException>().With.Message.EqualTo(SignupNeedsGamertagException.Reason));
    }
}
