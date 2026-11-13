using System.Security.Claims;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// CurrentUserService: a set id wins over the ambient user, which wins over the HTTP context's claims; and what
/// SetCurrentUserIdFromClaims does (and doesn't) set. No database, host or network.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CurrentUserServiceUnitTests
{
    private static CurrentUserService Service(IHttpContextAccessor? http = null, IAmbientCurrentUser? ambient = null) =>
        new(http ?? Substitute.For<IHttpContextAccessor>(), ambient ?? Substitute.For<IAmbientCurrentUser>());

    private static ClaimsPrincipal Authenticated(string? nameIdentifier) => new(new ClaimsIdentity(
        nameIdentifier is null ? [] : [new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "test"));

    [Test]
    public void GetCurrentUserId_ASetId_WinsOverTheAmbientUser()
    {
        var ambient = Substitute.For<IAmbientCurrentUser>();
        ambient.UserId.Returns(99);
        var service = Service(ambient: ambient);
        service.SetCurrentUserId(5);

        Assert.That(service.GetCurrentUserId(), Is.EqualTo(5));
    }

    [Test]
    public void GetCurrentUserId_NoSetId_UsesTheAmbientUser_NeverTouchingTheHttpContext()
    {
        var ambient = Substitute.For<IAmbientCurrentUser>();
        ambient.UserId.Returns(7);
        var http = Substitute.For<IHttpContextAccessor>();
        http.HttpContext.Returns(_ => throw new InvalidOperationException("Should not be read: the ambient user answers first"));
        var service = Service(http, ambient);

        Assert.That(service.GetCurrentUserId(), Is.EqualTo(7));
    }

    [Test]
    public void GetCurrentUserId_NoSetIdOrAmbientUser_FallsBackToTheHttpContextsAuthenticatedClaim()
    {
        var http = Substitute.For<IHttpContextAccessor>();
        http.HttpContext.Returns(new DefaultHttpContext { User = Authenticated("42") });
        var service = Service(http);

        Assert.That(service.GetCurrentUserId(), Is.EqualTo(42));
    }

    [Test]
    public void GetCurrentUserId_NothingAnywhere_IsNull()
        => Assert.That(Service().GetCurrentUserId(), Is.Null);

    [Test]
    public void SetCurrentUserIdFromClaims_Null_DoesNothing()
    {
        var service = Service();
        service.SetCurrentUserIdFromClaims(null);

        Assert.That(service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void SetCurrentUserIdFromClaims_NotAuthenticated_IsIgnored()
    {
        var service = Service();
        // ClaimsIdentity with no authenticationType: IsAuthenticated is false
        service.SetCurrentUserIdFromClaims(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "9")])));

        Assert.That(service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void SetCurrentUserIdFromClaims_AuthenticatedWithNoNameIdentifier_IsIgnored()
    {
        var service = Service();
        service.SetCurrentUserIdFromClaims(Authenticated(null));

        Assert.That(service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void SetCurrentUserIdFromClaims_NameIdentifierNotAnInteger_IsIgnored()
    {
        var service = Service();
        service.SetCurrentUserIdFromClaims(Authenticated("not-a-number"));

        Assert.That(service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void SetCurrentUserIdFromClaims_Authenticated_SetsTheId()
    {
        var service = Service();
        service.SetCurrentUserIdFromClaims(Authenticated("123"));

        Assert.That(service.GetCurrentUserId(), Is.EqualTo(123));
    }
}
