using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AvailabilityTimeslotDto = GrifballWebApp.Server.Availability.TimeslotDto;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileControllerSetGamertagTests
{
    private GrifballContext _context;
    private ISetGamertagService _setGamertag;
    private ProfileController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _setGamertag = Substitute.For<ISetGamertagService>();
        _controller = new ProfileController(_context, _setGamertag, new GrifballWebApp.Server.Stats.SeasonStatsService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void SetGamertag_RequiresPlayerRole()
    {
        Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(ProfileController), nameof(ProfileController.SetGamertag))!.Roles, Is.EqualTo("Player"));
    }

    [Test]
    public async Task SetGamertag_OwnUser_CallsService()
    {
        _controller.WithUser("42", "me", "Player");
        _setGamertag.SetGamertag(42, "NewGT", Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _controller.SetGamertag(42, "NewGT", CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        await _setGamertag.Received(1).SetGamertag(42, "NewGT", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_ServiceReturnsError_ReturnsBadRequestWithMessage()
    {
        _controller.WithUser("42", "me", "Player");
        _setGamertag.SetGamertag(42, "NewGT", Arg.Any<CancellationToken>()).Returns("User does not exist");

        var result = await _controller.SetGamertag(42, "NewGT", CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("User does not exist"));
    }

    [Test]
    public async Task SetGamertag_OtherUser_ReturnsForbid()
    {
        _controller.WithUser("42", "me", "Player");

        var result = await _controller.SetGamertag(43, "NewGT", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ForbidResult>());
        _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!, default);
    }

    [TestCase(null)]
    [TestCase("garbage")]
    public async Task SetGamertag_NoOrInvalidId_ReturnsForbid(string? claim)
    {
        if (claim is null) _controller.WithAnonymousUser(); else _controller.WithUser(claim);

        var result = await _controller.SetGamertag(1, "NewGT", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }
}
