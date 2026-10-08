using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class UserManagementControllerTests
{
    private IUserManagementService _service;
    private IUserMergeService _mergeService;
    private UserManagementController _controller;

    [SetUp]
    public void Setup()
    {
        _service = Substitute.For<IUserManagementService>();
        _mergeService = Substitute.For<IUserMergeService>();
        _controller = new UserManagementController(_service, _mergeService);
    }

    private static UserResponseDto Dto(int id) => new()
    {
        UserID = id,
        UserName = "u" + id,
        Region = null,
        DisplayName = null,
        Gamertag = null,
        Discord = null,
        ExternalAuthCount = 0,
        HasPassword = false,
        Roles = [],
    };

    [Test]
    public void Controller_RequiresSysadminRole()
    {
        var attr = ControllerTestHelpers.ClassAuthorize(typeof(UserManagementController));
        Assert.That(attr, Is.Not.Null);
        Assert.That(attr!.Roles, Is.EqualTo("Sysadmin"));
    }

    [TestCase("  bob  ", "bob")]
    [TestCase(null, "")]
    [TestCase("", "")]
    public async Task GetUsers_TrimsSearchAndPassesFilter(string? search, string expected)
    {
        var filter = new PaginationFilter(2, 5);
        var page = new PaginationResult<UserResponseDto> { TotalCount = 1, Results = [Dto(1)] };
        _service.GetUsers(filter, expected, Arg.Any<CancellationToken>()).Returns(page);

        var result = await _controller.GetUsers(filter, search, CancellationToken.None);

        Assert.That(result, Is.SameAs(page));
        await _service.Received(1).GetUsers(filter, expected, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetUser_ReturnsServiceResult()
    {
        var dto = Dto(4);
        _service.GetUser(4, Arg.Any<CancellationToken>()).Returns(dto);

        Assert.That(await _controller.GetUser(4, CancellationToken.None), Is.SameAs(dto));
        Assert.That(await _controller.GetUser(5, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task CreateUser_NullError_ReturnsOk()
    {
        var req = new CreateUserRequestDto { UserName = "a", Gamertag = "b", DisplayName = "c" };
        _service.CreateUser(req, Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _controller.CreateUser(req, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        await _service.Received(1).CreateUser(req, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateUser_Error_ReturnsBadRequestWithMessage()
    {
        var req = new CreateUserRequestDto { UserName = "a", Gamertag = "b", DisplayName = "c" };
        _service.CreateUser(req, Arg.Any<CancellationToken>()).Returns("User name is taken");

        var result = await _controller.CreateUser(req, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("User name is taken"));
    }

    [Test]
    public async Task EditUser_NullError_ReturnsOk()
    {
        var dto = Dto(2);
        _service.EditUser(dto, Arg.Any<CancellationToken>()).Returns((string?)null);

        Assert.That(await _controller.EditUser(dto, CancellationToken.None), Is.TypeOf<OkResult>());
    }

    [Test]
    public async Task EditUser_Error_ReturnsBadRequest()
    {
        var dto = Dto(2);
        _service.EditUser(dto, Arg.Any<CancellationToken>()).Returns("Failed to set username");

        var result = await _controller.EditUser(dto, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Failed to set username"));
    }

    [Test]
    public async Task MergeUser_CallsMergeServiceWithIdsAndOptions()
    {
        var options = new MergeOptions();
        var req = new MergeRequest { MergeToId = 1, MergeFromId = 2, MergeOptions = options };

        var result = await _controller.MergeUser(req, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        await _mergeService.Received(1).Merge(1, 2, options, Arg.Any<CancellationToken>());
    }
}
