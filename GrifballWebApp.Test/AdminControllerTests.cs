using GrifballWebApp.Server.Controllers;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.UserManagement;
using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
[Parallelizable(ParallelScope.All)]
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
        _controller = Controller(Direct);
    }

    private static readonly HaloInfiniteSettings Direct = new(HaloInfiniteMode.Direct, new Uri(HaloInfiniteSettings.DefaultGatewayBaseUrl), null, HaloInfiniteSettings.DefaultGatewayAdminUrl);

    private static readonly HaloInfiniteSettings Gateway = new(HaloInfiniteMode.Gateway, new Uri("http://gateway.test:8080/"), "the-key", "https://gateway.test/admin");

    private IHaloGatewayAccountStatus _gateway = Substitute.For<IHaloGatewayAccountStatus>();

    /// <summary>The controller as Program.cs builds it: in Gateway mode there is no IAccountAuthorization.</summary>
    private AdminController Controller(HaloInfiniteSettings settings)
    {
        var check = new HaloInfiniteHealthCheck(_haloInfiniteClientFactory, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10),
            TimeProvider.System, NullLogger<HaloInfiniteHealthCheck>.Instance, gateway: settings.IsGateway ? _gateway : null);
        return new AdminController(_logger, _haloInfiniteClientFactory, settings, check, _dataPullService, _options, _userManagementService,
            settings.IsGateway ? null : _accountAuthorization);
    }

    private void GatewayAnswers(string accountStatus, int apiCode)
    {
        _gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, accountStatus, null));
        _haloInfiniteClientFactory.StatsGetMatchStats(Arg.Any<string>())
            .Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(new MatchStats(), new HaloApiErrorContainer { Code = apiCode }));
    }

    [Test]
    public async Task Gateway_SetCode_Conflict_AndNothingIsStored()
    {
        var controller = Controller(Gateway);

        var result = await controller.SetCode("testcode");

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        Assert.That(((ConflictObjectResult)result).Value, Is.EqualTo(AdminController.GatewayModeMessage));
        await _accountAuthorization.DidNotReceiveWithAnyArgs().SetCodeAsync(default!);
    }

    [Test]
    public void Gateway_DeleteTokens_Conflict()
    {
        var result = Controller(Gateway).DeleteTokens();

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        Assert.That(((ConflictObjectResult)result).Value, Does.Contain("Halo gateway"));
    }

    [Test]
    public async Task Direct_HaloStatus_IsOnlyTheMode_AndProbesNothing()
    {
        var status = await _controller.HaloStatus(CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(status.Mode, Is.EqualTo(HaloInfiniteMode.Direct));
            Assert.That(status.GatewayUrl, Is.Null);
            Assert.That(status.Healthy, Is.False);
            await _haloInfiniteClientFactory.DidNotReceiveWithAnyArgs().StatsGetMatchStats(default(string)!);
        });
    }

    [Test]
    public async Task Gateway_HaloStatus_ReportsTheGatewayAndAFreshProbe()
    {
        GatewayAnswers("Healthy", 200);
        var controller = Controller(Gateway);

        var first = await controller.HaloStatus(CancellationToken.None);
        var second = await controller.HaloStatus(CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(first.Mode, Is.EqualTo(HaloInfiniteMode.Gateway));
            Assert.That(first.GatewayUrl, Is.EqualTo("http://gateway.test:8080/"));
            Assert.That(first.GatewayAdminUrl, Is.EqualTo("https://gateway.test/admin"));
            Assert.That(first.ApiKeyConfigured, Is.True);
            Assert.That(first.Reachable, Is.True);
            Assert.That(first.AccountStatus, Is.EqualTo("Healthy"));
            Assert.That(first.ApiCode, Is.EqualTo(200));
            Assert.That(first.Healthy, Is.True);
            Assert.That(first.Message, Does.Contain("200"));
            Assert.That(second.Healthy, Is.True);
            // Each request probes again, whatever the health check has cached.
            await _haloInfiniteClientFactory.Received(2).StatsGetMatchStats(Arg.Any<string>());
        });
    }

    [Test]
    public async Task Gateway_HaloStatus_AccountNeedsSignIn_NotHealthy()
    {
        GatewayAnswers("Degraded", 200);

        var status = await Controller(Gateway).HaloStatus(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(status.Healthy, Is.False);
            Assert.That(status.AccountStatus, Is.EqualTo("Degraded"));
            Assert.That(status.Message, Does.Contain("Degraded"));
        });
    }

    [Test]
    public async Task Gateway_HaloStatus_NeverReturnsTheKey()
    {
        GatewayAnswers("Healthy", 200);

        var status = await Controller(Gateway).HaloStatus(CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(status, GrifballWebApp.Server.ApiJson.Options());

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("the-key"));
            Assert.That(json, Does.Contain("\"mode\":\"Gateway\""));
        });
    }

    [TestCase("Healthy", 200, "Good")]
    [TestCase("Degraded", 200, "Halo gateway account Degraded")]
    [TestCase("Healthy", 401, "API key")]
    public async Task Gateway_CheckStatus_GoodOrWhatIsWrong_NeverASignInUrl(string account, int code, string expected)
    {
        GatewayAnswers(account, code);

        var result = await Controller(Gateway).CheckStatus();

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain(expected));
            Assert.That(result, Does.Not.StartWith("https://login.live.com"));
        });
    }

    [Test]
    public async Task Get_ShouldThrowException_WhenGameIsNotGuid()
    {
        var ex = await Assert.ThrowsAsync<Exception>(async () => await _controller.Get("not-a-guid"));
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

    [Test]
    public async Task GeneratePasswordResetLink_ShouldReturnBadRequest_WhenUsernameIsEmpty()
    {
        var request = new GeneratePasswordResetLinkRequestDto { Username = "" };
        var result = await _controller.GeneratePasswordResetLink(request, CancellationToken.None);
        
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result.Result;
        Assert.That(badRequest.Value, Is.EqualTo("Username is required"));
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldReturnBadRequest_WhenServiceFails()
    {
        var request = new GeneratePasswordResetLinkRequestDto { Username = "testuser" };
        _userManagementService.GeneratePasswordResetLink("testuser", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((false, "User not found", null, null));

        // Mock the User.FindFirstValue to return a valid user ID
        var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
        };

        var result = await _controller.GeneratePasswordResetLink(request, CancellationToken.None);
        
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result.Result;
        Assert.That(badRequest.Value, Is.EqualTo("User not found"));
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldReturnOk_WhenSuccessful()
    {
        var request = new GeneratePasswordResetLinkRequestDto { Username = "testuser" };
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        _userManagementService.GeneratePasswordResetLink("testuser", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((true, "Password reset link generated successfully", "abc123token", expiresAt));

        // Mock the User.FindFirstValue to return a valid user ID
        var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
        };

        var result = await _controller.GeneratePasswordResetLink(request, CancellationToken.None);
        
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result;
        var response = (GeneratePasswordResetLinkResponseDto)okResult.Value;
        Assert.That(response.ResetLink, Is.EqualTo("/reset-password?token=abc123token"));
        Assert.That(response.ExpiresAt, Is.EqualTo(expiresAt));
    }

    [Test]
    public async Task CleanupExpiredPasswordResetLinks_ShouldReturnOk()
    {
        await _userManagementService.CleanupExpiredPasswordResetLinks(Arg.Any<CancellationToken>());
        
        var result = await _controller.CleanupExpiredPasswordResetLinks(CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.EqualTo("Expired password reset links cleaned up"));
        await _userManagementService.Received(1).CleanupExpiredPasswordResetLinks(Arg.Any<CancellationToken>());
    }
}
