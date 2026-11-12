using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.Test;

/// <summary>ProfileControllerSetGamertagTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileControllerSetGamertagUnitTests
{
    private readonly ISetGamertagService _setGamertag = Substitute.For<ISetGamertagService>();
    private readonly ProfileController _controller;

    public ProfileControllerSetGamertagUnitTests()
    {
        // SetGamertag asks the gamertag service alone
        _controller = new ProfileController(null!, _setGamertag, null!, null!);
    }

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

        var result = await _controller.SetGamertag(42, new SetGamertagRequest("NewGT"), CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        await _setGamertag.Received(1).SetGamertag(42, "NewGT", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_ServiceReturnsError_IsABadRequestProblem()
    {
        _controller.WithUser("42", "me", "Player");
        _setGamertag.SetGamertag(42, "NewGT", Arg.Any<CancellationToken>()).Returns("User does not exist");

        var result = await _controller.SetGamertag(42, new SetGamertagRequest("NewGT"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "User does not exist");
    }

    [Test]
    public async Task SetGamertag_OtherUser_IsForbidden()
    {
        _controller.WithUser("42", "me", "Player");

        var result = await _controller.SetGamertag(43, new SetGamertagRequest("NewGT"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status403Forbidden, "You can only set your own gamertag");
        _ = _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!, default);
    }

    [TestCase(null)]
    [TestCase("garbage")]
    public async Task SetGamertag_NoOrInvalidId_IsForbidden(string? claim)
    {
        if (claim is null) _controller.WithAnonymousUser(); else _controller.WithUser(claim);

        var result = await _controller.SetGamertag(1, new SetGamertagRequest("NewGT"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status403Forbidden, "You can only set your own gamertag");
    }
}
