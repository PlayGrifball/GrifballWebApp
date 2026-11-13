using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GrifballWebApp.Server.Test;

/// <summary>The hubs' page (SignalRHubsPage): sysadmins only, the rest of the site untouched.</summary>
[TestFixture]
public class SignalRHubsPageTests
{
    private static (DefaultHttpContext Context, Func<bool> Passed, RequestDelegate Next) Request(string path, params string[] roles)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (roles.Length > 0)
        {
            var claims = roles.Where(r => r.Length > 0).Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.NameIdentifier, "7"));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        }
        var passed = false;
        return (context, () => passed, _ => { passed = true; return Task.CompletedTask; });
    }

    [TestCase("/signalr-dev")]
    [TestCase("/signalr-dev/spec.json")]
    public async Task RequireSysadmin_RefusesAVisitor_With401AndNoRedirect(string path)
    {
        var (context, passed, next) = Request(path);

        await SignalRHubsPage.RequireSysadmin(context, next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(passed(), Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(context.Response.Headers.Location.ToString(), Is.Empty);
        }
    }

    [Test]
    public async Task RequireSysadmin_Refuses_ARequestWithNoIdentityAtAll_With401()
    {
        var (context, passed, next) = Request("/signalr-dev");
        context.User = new ClaimsPrincipal();

        await SignalRHubsPage.RequireSysadmin(context, next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(passed(), Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        }
    }

    [Test]
    public async Task RequireSysadmin_RefusesASignedInUserWhoIsNotASysadmin_With403()
    {
        var (context, passed, next) = Request("/signalr-dev/index.html", "Commissioner");

        await SignalRHubsPage.RequireSysadmin(context, next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(passed(), Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }
    }

    [Test]
    public async Task RequireSysadmin_ShowsASysadminThePage()
    {
        var (context, passed, next) = Request("/signalr-dev/spec.json", "Sysadmin");

        await SignalRHubsPage.RequireSysadmin(context, next);

        Assert.That(passed(), Is.True);
    }

    [TestCase("/seasons")]
    [TestCase("/TeamsHub/negotiate")]
    [TestCase("/signalr-developers")]
    public async Task RequireSysadmin_LeavesTheRestAlone(string path)
    {
        var (context, passed, next) = Request(path);

        await SignalRHubsPage.RequireSysadmin(context, next);

        Assert.That(passed(), Is.True);
    }
}
