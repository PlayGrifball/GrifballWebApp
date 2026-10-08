using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Security.Claims;
using System.Text.Json;

namespace GrifballWebApp.Test;

[TestFixture]
public class HubFilterTests
{
    public class FakeHub : Hub
    {
        public void Ping() { }
    }

    private static HubInvocationContext Invocation(ClaimsPrincipal? user = null)
    {
        var caller = Substitute.For<HubCallerContext>();
        caller.User.Returns(user);
        return new HubInvocationContext(caller, Substitute.For<IServiceProvider>(), new FakeHub(), typeof(FakeHub).GetMethod(nameof(FakeHub.Ping))!, Array.Empty<object?>());
    }

    [Test]
    public async Task ExceptionLogHubFilter_PassesThroughResult()
    {
        var logger = new RecordingLogger<ExceptionLogHubFilter>();
        var filter = new ExceptionLogHubFilter(logger);
        var invocation = Invocation();
        HubInvocationContext? seen = null;

        var result = await filter.InvokeMethodAsync(invocation, ctx => { seen = ctx; return ValueTask.FromResult<object?>("ok"); });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(seen, Is.SameAs(invocation));
            Assert.That(logger.Entries, Is.Empty);
        });
    }

    [Test]
    public void ExceptionLogHubFilter_SynchronousThrow_IsLoggedAndRethrown()
    {
        var logger = new RecordingLogger<ExceptionLogHubFilter>();
        var filter = new ExceptionLogHubFilter(logger);
        var exception = new InvalidOperationException("bad");

        var thrown = Assert.Throws<InvalidOperationException>(() => filter.InvokeMethodAsync(Invocation(), _ => throw exception));

        var entry = logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(exception));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo("An error occurred in the hub method: Ping"));
            Assert.That(entry.Exception, Is.SameAs(exception));
        });
    }

    [Test]
    public void ExceptionLogHubFilter_AsyncFault_IsNotLogged()
    {
        var logger = new RecordingLogger<ExceptionLogHubFilter>();
        var filter = new ExceptionLogHubFilter(logger);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await filter.InvokeMethodAsync(Invocation(), async _ => { await Task.Yield(); throw new InvalidOperationException("async"); }));

        // BUG: ExceptionLogHubFilter.cs:20 returns next(...) without awaiting it, so exceptions thrown by async hub methods
        // (i.e. nearly all of them) escape the try/catch and are never logged. Expected: one error entry. Actual: none.
        Assert.That(logger.Entries, Is.Empty);
    }

    [Test]
    public async Task UserContextHubFilter_SetsCurrentUserFromCallerClaims_ThenInvokesNext()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        var filter = new UserContextHubFilter(currentUser);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "test"));
        var called = false;

        var result = await filter.InvokeMethodAsync(Invocation(principal), _ =>
        {
            currentUser.Received(1).SetCurrentUserIdFromClaims(principal); // must be set before the hub method runs
            called = true;
            return ValueTask.FromResult<object?>(42);
        });

        Assert.Multiple(() =>
        {
            Assert.That(called, Is.True);
            Assert.That(result, Is.EqualTo(42));
        });
    }

    [Test]
    public async Task UserContextHubFilter_WorksWithAnonymousCaller()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        var filter = new UserContextHubFilter(currentUser);

        await filter.InvokeMethodAsync(Invocation(null), _ => ValueTask.FromResult<object?>(null));

        currentUser.Received(1).SetCurrentUserIdFromClaims(null);
    }
}
