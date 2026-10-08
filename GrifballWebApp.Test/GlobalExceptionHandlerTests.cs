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
