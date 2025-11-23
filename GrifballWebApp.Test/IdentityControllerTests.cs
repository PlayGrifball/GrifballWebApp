using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class IdentityControllerTests
{
    private ILogger<IdentityController> _logger;
    private UserManager<User> _userManager;
    private SignInManager<User> _signInManager;
    private IOptionsMonitor<BearerTokenOptions> _optionsMonitor;
    private TimeProvider _timeProvider;
    private IUserManagementService _userManagementService;
    private IdentityController _controller;

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<IdentityController>>();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _signInManager = Substitute.For<SignInManager<User>>(
            _userManager, Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), 
            Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
        _optionsMonitor = Substitute.For<IOptionsMonitor<BearerTokenOptions>>();
        _timeProvider = TimeProvider.System;
        _userManagementService = Substitute.For<IUserManagementService>();
        
        _controller = new IdentityController(_logger, _userManager, _signInManager, 
            _optionsMonitor, _timeProvider, _userManagementService);
    }

    [Test]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenIsEmpty()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "", NewPassword = "newpassword" };
        
        var result = await _controller.ResetPassword(request, CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        Assert.That(badRequest.Value, Is.EqualTo("Token is required"));
    }

    [Test]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenIsWhitespace()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "   ", NewPassword = "newpassword" };
        
        var result = await _controller.ResetPassword(request, CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        Assert.That(badRequest.Value, Is.EqualTo("Token is required"));
    }

    [Test]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenNewPasswordIsEmpty()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "validtoken", NewPassword = "" };
        
        var result = await _controller.ResetPassword(request, CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        Assert.That(badRequest.Value, Is.EqualTo("New password is required"));
    }

    [Test]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenNewPasswordIsWhitespace()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "validtoken", NewPassword = "   " };
        
        var result = await _controller.ResetPassword(request, CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        Assert.That(badRequest.Value, Is.EqualTo("New password is required"));
    }

    [Test]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenServiceFails()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "invalidtoken", NewPassword = "newpassword" };
        _userManagementService.UsePasswordResetLink("invalidtoken", "newpassword", Arg.Any<CancellationToken>())
            .Returns((false, "Invalid or already used reset link"));
        
        var result = await _controller.ResetPassword(request, CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        Assert.That(badRequest.Value, Is.EqualTo("Invalid or already used reset link"));
    }

    [Test]
    public async Task ResetPassword_ShouldReturnOk_WhenSuccessful()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "validtoken", NewPassword = "newpassword123" };
        _userManagementService.UsePasswordResetLink("validtoken", "newpassword123", Arg.Any<CancellationToken>())
            .Returns((true, "Password reset successfully"));
        
        var result = await _controller.ResetPassword(request, CancellationToken.None);
        
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.EqualTo("Password reset successfully"));
    }

    [Test]
    public async Task ResetPassword_ShouldCallUserManagementService_WithCorrectParameters()
    {
        var request = new UsePasswordResetLinkRequestDto { Token = "testtoken", NewPassword = "testpassword" };
        _userManagementService.UsePasswordResetLink("testtoken", "testpassword", Arg.Any<CancellationToken>())
            .Returns((true, "Password reset successfully"));
        
        await _controller.ResetPassword(request, CancellationToken.None);
        
        await _userManagementService.Received(1).UsePasswordResetLink(
            "testtoken",
            "testpassword",
            Arg.Any<CancellationToken>()
        );
    }
}
