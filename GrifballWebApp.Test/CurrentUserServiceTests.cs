using System.Net;
using System.Security.Claims;
using System.Text;
using DiscordInterface.Generated;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Extensions;
using Microsoft.AspNetCore.Http;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CurrentUserServiceTests
{
    private IHttpContextAccessor _accessor;
    private IAmbientCurrentUser _ambient;
    private CurrentUserService _service;

    [SetUp]
    public void Setup()
    {
        _accessor = Substitute.For<IHttpContextAccessor>();
        _ambient = Substitute.For<IAmbientCurrentUser>();
        _service = new CurrentUserService(_accessor, _ambient);
    }

    private static ClaimsPrincipal Principal(string? id, bool authenticated = true)
    {
        var claims = id is null ? new List<Claim>() : [new Claim(ClaimTypes.NameIdentifier, id)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
    }

    [Test]
    public void GetCurrentUserId_FromHttpContextClaims()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12") });

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(12));
    }

    [Test]
    public void GetCurrentUserId_NoHttpContext_ReturnsNull()
    {
        _accessor.HttpContext.Returns((HttpContext?)null);

        Assert.That(_service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void GetCurrentUserId_UnauthenticatedOrBadClaim_ReturnsNull()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12", authenticated: false) });
        Assert.That(_service.GetCurrentUserId(), Is.Null);

        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("not-int") });
        Assert.That(_service.GetCurrentUserId(), Is.Null);

        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal(null) });
        Assert.That(_service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void SetCurrentUserId_TakesPrecedenceAndIsCached()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12") });
        _service.SetCurrentUserId(99);

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(99));
        _ = _accessor.DidNotReceive().HttpContext;
    }

    [Test]
    public void SetCurrentUserIdFromClaims_NullIsIgnored_ValidIsStored()
    {
        _service.SetCurrentUserIdFromClaims(null);
        _accessor.HttpContext.Returns((HttpContext?)null);
        Assert.That(_service.GetCurrentUserId(), Is.Null);

        _service.SetCurrentUserIdFromClaims(Principal("7"));
        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(7));
    }

    [Test]
    public void GetCurrentUserId_AmbientUser_UsedBeforeClaims()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12") });
        _ambient.UserId.Returns(34);

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(34));
        _ = _accessor.DidNotReceive().HttpContext;
    }

    [Test]
    public void GetCurrentUserId_SetCurrentUserId_UsedBeforeAmbientUser()
    {
        _ambient.UserId.Returns(34);
        _service.SetCurrentUserId(99);

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(99));
    }

    [Test]
    public void GetCurrentUserId_NoAmbientUser_StillUsesHttpClaims()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12") });
        _ambient.UserId.Returns((int?)null);

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(12));
    }
}
