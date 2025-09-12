using GrifballWebApp.Server.Controllers;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Core.Storage;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AdminControllerTests
{
    private ILogger<AdminController> _logger;
    private IHaloInfiniteClientFactory _haloInfiniteClientFactory;
    private IAccountAuthorization _accountAuthorization;
    private IDataPullService _dataPullService;
    private IOptionsMonitor<ClientConfiguration> _options;
    private IUserManagementService _userManagementService;
    private AdminController _controller;

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<AdminController>>();
        _haloInfiniteClientFactory = Substitute.For<IHaloInfiniteClientFactory>();
        _accountAuthorization = Substitute.For<IAccountAuthorization>();
        _dataPullService = Substitute.For<IDataPullService>();
        _options = Substitute.For<IOptionsMonitor<ClientConfiguration>>();
        _userManagementService = Substitute.For<IUserManagementService>();
        _controller = new AdminController(_logger, _haloInfiniteClientFactory, _accountAuthorization, _dataPullService, _options, _userManagementService);
    }

    [Test]
    public void Get_ShouldThrowException_WhenGameIsNotGuid()
    {
        var ex = Assert.ThrowsAsync<Exception>(async () => await _controller.Get("not-a-guid"));
        Assert.That(ex.Message, Is.EqualTo("Not valid guid"));
    }

    [Test]
    public async Task Get_ShouldCallDataPullService_WhenGuidIsValid()
    {
        var guid = Guid.NewGuid();
        await _controller.Get(guid.ToString());
        await _dataPullService.Received(1).GetAndSaveMatch(guid);
    }

    [Test]
    public async Task SetCode_ShouldReturnBadRequest_WhenCodeIsMissing()
    {
        var result = await _controller.SetCode("");
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SetCode_ShouldCallSetCodeAsync_AndReturnOk()
    {
        var code = "testcode";
        var result = await _controller.SetCode(code);
        await _accountAuthorization.Received(1).SetCodeAsync(code);
        Assert.That(result, Is.TypeOf<OkResult>());
    }

    [Test]
    public void DeleteTokens_ShouldReturnOk()
    {
        var result = _controller.DeleteTokens();
        Assert.That(result, Is.TypeOf<OkResult>());
    }

    [Test]
    public async Task CheckStatus_ShouldReturnGood_WhenStatsGetMatchStatsSucceeds()
    {
        _haloInfiniteClientFactory.StatsGetMatchStats(Arg.Any<string>())
            .Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(new MatchStats(), null));
        var result = await _controller.CheckStatus();
        Assert.That(result, Is.EqualTo("Good"));
    }

    [Test]
    public async Task CheckStatus_ShouldReturnAuthUrl_WhenStatsGetMatchStatsThrows()
    {
        _haloInfiniteClientFactory.StatsGetMatchStats(Arg.Any<string>())
            .ThrowsAsync(callInfo => throw new Exception("fail"));
        var config = new ClientConfiguration { ClientId = "cid", RedirectUrl = "http://redir" };
        _options.CurrentValue.Returns(config);
        var result = await _controller.CheckStatus();
        Assert.That(result, Is.EqualTo("https://login.live.com/oauth20_authorize.srf?client_id=cid&response_type=code&approval_prompt=auto&scope=Xboxlive.signin+Xboxlive.offline_access&redirect_uri=http%3a%2f%2fredir"));
    }
}
