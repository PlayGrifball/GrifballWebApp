using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GrifballWebApp.Server.Test;

[TestFixture]
public class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // GlobalExceptionHandler is internal sealed; build it via reflection with a logger closed over the internal type.
    private static (IExceptionHandler Handler, object Logger) Create()
    {
        var type = typeof(GrifballWebApp.Server.Program).Assembly.GetType("GrifballWebApp.Server.GlobalExceptionHandler", throwOnError: true)!;
        var loggerType = typeof(RecordingLogger<>).MakeGenericType(type);
        var logger = Activator.CreateInstance(loggerType)!;
        var handler = (IExceptionHandler)Activator.CreateInstance(type, logger)!;
        return (handler, logger);
    }

    private static List<RecordingLogger<object>.Entry> Entries(object logger)
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
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, WebJson);
        var entry = Entries(logger).Single();
        using (Assert.EnterMultipleScope())
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
        }
    }

    private static IEnumerable<TestCaseData> ExpectedRefusals()
    {
        yield return new TestCaseData(new GrifballWebApp.Application.Teams.TeamServiceException("Player is already on a team"), 400, "Player is already on a team").SetName("TeamServiceException_Is400");
        yield return new TestCaseData(new GrifballWebApp.Application.Signups.SignupsClosedException(), 400, "Signups are closed for this season").SetName("SignupsClosedException_Is400");
        yield return new TestCaseData(new GrifballWebApp.Application.Profile.MergeConflictException("Both users are on different teams"), 409, "Both users are on different teams").SetName("MergeConflictException_Is409");
        yield return new TestCaseData(new GrifballWebApp.Application.Profile.UserHasHistoricalStatsException(7), 409, "User has historical stats; merge instead.").SetName("UserHasHistoricalStatsException_Is409");
        yield return new TestCaseData(new GrifballWebApp.Application.Historical.GamertagClaimException("There is no claim 7"), 400, "There is no claim 7").SetName("GamertagClaimException_Is400");
        yield return new TestCaseData(new GrifballWebApp.Application.Historical.GamertagClaimConflictException("Changed"), 409, "Changed").SetName("GamertagClaimConflictException_Is409");
        yield return new TestCaseData(new GrifballWebApp.Application.EventOrganizer.SeasonRefusedException("Name taken", 409), 409, "Name taken").SetName("SeasonRefusedException_KeepsItsStatus");
        yield return new TestCaseData(new GrifballWebApp.Application.EventOrganizer.SeasonRefusedException("Read only"), 400, "Read only").SetName("SeasonRefusedException_Is400ByDefault");
        yield return new TestCaseData(new GrifballWebApp.Application.Profile.UserNotFoundException(7), 404, "Did not find user 7").SetName("UserNotFoundException_Is404");
        yield return new TestCaseData(new GrifballWebApp.Application.Reschedule.RescheduleNotAllowedException("This match already has a result."), 400, "This match already has a result.").SetName("RescheduleNotAllowedException_Is400");
        yield return new TestCaseData(new GrifballWebApp.Application.Reschedule.RescheduleForbiddenException(), 403, "Only players on either team, or a commissioner, can request a reschedule for this match.").SetName("RescheduleForbiddenException_Is403");
        yield return new TestCaseData(new GrifballWebApp.Application.Reschedule.RescheduleAlreadyActiveException(), 409, "There is already an active reschedule request for this match.").SetName("RescheduleAlreadyActiveException_Is409");
        yield return new TestCaseData(new GrifballWebApp.Application.Reschedule.RescheduleAlreadyProcessedException(), 409, "Reschedule request has already been processed").SetName("RescheduleAlreadyProcessedException_Is409");
    }

    [TestCaseSource(nameof(ExpectedRefusals))]
    public async Task TryHandleAsync_ExpectedRefusal_KeepsItsMessage_WithA4xx(Exception exception, int status, string detail)
    {
        var (handler, logger) = Create();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, WebJson);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(status));
            Assert.That(problem!.Status, Is.EqualTo(status));
            Assert.That(problem.Detail, Is.EqualTo(detail));
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
            Assert.That(Entries(logger).Single().Level, Is.EqualTo(LogLevel.Information), "A refusal is not an error");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.Body.Length, Is.Zero);
            Assert.That(context.Response.StatusCode, Is.EqualTo(200), "Status code is left untouched");
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Message, Is.EqualTo("Client has disconnected"));
        }
    }
}
