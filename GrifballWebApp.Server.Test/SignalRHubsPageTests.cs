using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GrifballWebApp.Server.Test;

/// <summary>The hubs' page (SignalRHubsPage): signed-in users only, the rest of the site untouched.</summary>
[TestFixture]
public class SignalRHubsPageTests
{
    private static (DefaultHttpContext Context, Func<bool> Passed, RequestDelegate Next) Request(string path, bool signedIn)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (signedIn)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "Cookies"));
        var passed = false;
        return (context, () => passed, _ => { passed = true; return Task.CompletedTask; });
    }

    [TestCase("/signalr-dev")]
    [TestCase("/signalr-dev/index.html")]
    [TestCase("/signalr-dev/spec.json")]
    public async Task RequireSignIn_SendsAVisitorToSignIn(string path)
    {
        var (context, passed, next) = Request(path, signedIn: false);

        await SignalRHubsPage.RequireSignIn(context, next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(passed(), Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/login"));
        }
    }

    [Test]
    public async Task RequireSignIn_ShowsTheSignedInThePage()
    {
        var (context, passed, next) = Request("/signalr-dev/spec.json", signedIn: true);

        await SignalRHubsPage.RequireSignIn(context, next);

        Assert.That(passed(), Is.True);
    }

    [TestCase("/seasons")]
    [TestCase("/TeamsHub/negotiate")]
    [TestCase("/signalr-developers")]
    public async Task RequireSignIn_LeavesTheRestAlone(string path)
    {
        var (context, passed, next) = Request(path, signedIn: false);

        await SignalRHubsPage.RequireSignIn(context, next);

        Assert.That(passed(), Is.True);
    }
}
