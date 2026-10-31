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
public class GlobalExceptionHandlerTests
{
    // GlobalExceptionHandler is internal sealed; build it via reflection with a logger closed over the internal type.
    private static (IExceptionHandler Handler, object Logger) Create()
    {
        var type = typeof(QueueService).Assembly.GetType("GrifballWebApp.Server.GlobalExceptionHandler", throwOnError: true)!;
        var loggerType = typeof(RecordingLogger<>).MakeGenericType(type);
        var logger = Activator.CreateInstance(loggerType)!;
        var handler = (IExceptionHandler)Activator.CreateInstance(type, logger)!;
        return (handler, logger);
    }

    private static IReadOnlyList<RecordingLogger<object>.Entry> Entries(object logger)
    {
        var entries = (System.Collections.IEnumerable)logger.GetType().GetProperty("Entries")!.GetValue(logger)!;
        return entries.Cast<object>().Select(e =>
        {
            var t = e.GetType();
            return new RecordingLogger<object>.Entry((LogLevel)t.GetProperty("Level")!.GetValue(e)!, (string)t.GetProperty("Message")!.GetValue(e)!, (Exception?)t.GetProperty("Exception")!.GetValue(e));
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
            Assert.That(problem.Detail, Is.EqualTo("An unexpected error occurred. If it keeps happening, report it with the trace id."), "The exception's message is not sent");
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo("Exception occurred: boom"));
            Assert.That(entry.Exception, Is.SameAs(exception));
        });
    }

    private static IEnumerable<TestCaseData> ExpectedRefusals()
    {
        yield return new TestCaseData(new GrifballWebApp.Server.Teams.TeamServiceException("Player is already on a team"), 400, "Player is already on a team").SetName("TeamServiceException_Is400");
        yield return new TestCaseData(new GrifballWebApp.Server.Signups.SignupsClosedException(), 400, "Signups are closed for this season").SetName("SignupsClosedException_Is400");
        yield return new TestCaseData(new GrifballWebApp.Server.Profile.MergeConflictException("Both users are on different teams"), 409, "Both users are on different teams").SetName("MergeConflictException_Is409");
        yield return new TestCaseData(new GrifballWebApp.Server.Profile.UserHasHistoricalStatsException(7), 409, "User has historical stats; merge instead.").SetName("UserHasHistoricalStatsException_Is409");
        yield return new TestCaseData(new GrifballWebApp.Server.Profile.UserNotFoundException(7), 404, "Did not find user 7").SetName("UserNotFoundException_Is404");
    }

    [TestCaseSource(nameof(ExpectedRefusals))]
    public async Task TryHandleAsync_ExpectedRefusal_KeepsItsMessage_WithA4xx(Exception exception, int status, string detail)
    {
        var (handler, logger) = Create();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(status));
            Assert.That(problem!.Status, Is.EqualTo(status));
            Assert.That(problem.Detail, Is.EqualTo(detail));
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
            Assert.That(Entries(logger).Single().Level, Is.EqualTo(LogLevel.Information), "A refusal is not an error");
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
