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

namespace GrifballWebApp.Test.CovE;

[TestFixture]
public class GlobalExceptionHandlerTests_E
{
    // GlobalExceptionHandler is internal sealed; build it via reflection with a logger closed over the internal type.
    private static (IExceptionHandler Handler, object Logger) Create()
    {
        var type = typeof(QueueService).Assembly.GetType("GrifballWebApp.Server.GlobalExceptionHandler", throwOnError: true)!;
        var loggerType = typeof(RecordingLogger_E<>).MakeGenericType(type);
        var logger = Activator.CreateInstance(loggerType)!;
        var handler = (IExceptionHandler)Activator.CreateInstance(type, logger)!;
        return (handler, logger);
    }

    private static IReadOnlyList<RecordingLogger_E<object>.Entry> Entries(object logger)
    {
        var entries = (System.Collections.IEnumerable)logger.GetType().GetProperty("Entries")!.GetValue(logger)!;
        return entries.Cast<object>().Select(e =>
        {
            var t = e.GetType();
            return new RecordingLogger_E<object>.Entry((LogLevel)t.GetProperty("Level")!.GetValue(e)!, (string)t.GetProperty("Message")!.GetValue(e)!, (Exception?)t.GetProperty("Exception")!.GetValue(e));
        }).ToList();
    }

    [Test]
    public async Task TryHandleAsync_WritesProblemDetailsWith500_AndLogsError()
    {
        var (handler, logger) = Create();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("boom");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var entry = Entries(logger).Single();
        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(context.Response.ContentType, Does.StartWith("application/json"));
            Assert.That(problem!.Status, Is.EqualTo(500));
            Assert.That(problem.Title, Is.EqualTo("Server error"));
            Assert.That(problem.Detail, Is.EqualTo("boom"));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo("Exception occurred: boom"));
            Assert.That(entry.Exception, Is.SameAs(exception));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenClientDisconnected_LogsWarningAndWritesNothing()
    {
        var (handler, logger) = Create();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handled = await handler.TryHandleAsync(context, new TaskCanceledException(), cts.Token);

        var entry = Entries(logger).Single();
        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.Body.Length, Is.EqualTo(0));
            Assert.That(context.Response.StatusCode, Is.EqualTo(200), "Status code is left untouched");
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Message, Is.EqualTo("Client has disconnected"));
        });
    }
}

[TestFixture]
public class HubFilterTests_E
{
    public class FakeHub_E : Hub
    {
        public void Ping() { }
    }

    private static HubInvocationContext Invocation(ClaimsPrincipal? user = null)
    {
        var caller = Substitute.For<HubCallerContext>();
        caller.User.Returns(user);
        return new HubInvocationContext(caller, Substitute.For<IServiceProvider>(), new FakeHub_E(), typeof(FakeHub_E).GetMethod(nameof(FakeHub_E.Ping))!, Array.Empty<object?>());
    }

    [Test]
    public async Task ExceptionLogHubFilter_PassesThroughResult()
    {
        var logger = new RecordingLogger_E<ExceptionLogHubFilter>();
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
        var logger = new RecordingLogger_E<ExceptionLogHubFilter>();
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
        var logger = new RecordingLogger_E<ExceptionLogHubFilter>();
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
