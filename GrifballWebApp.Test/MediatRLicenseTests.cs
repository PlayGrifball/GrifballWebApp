using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Test;

/// <summary>
/// MediatR 13 and later (Lucky Penny Software) look for a license key (MediatR's configuration, Mediator.LicenseKey, or the
/// MEDIATR_LICENSE_KEY / LUCKYPENNY_LICENSE_KEY environment variables) when the mediator is built. The app sets none: this
/// pins down that without one MediatR still dispatches, and only logs a warning (category
/// LuckyPennySoftware.MediatR.License), never an error. MediatR 12 has no license check, so nothing is logged at all.
/// </summary>
[TestFixture]
[NonParallelizable] // reads the process environment
public class MediatRLicenseTests
{
    private const string LicenseCategory = "LuckyPennySoftware.MediatR.License";

    public record Ping(int Value) : INotification;

    public class PingHandler(List<int> received) : INotificationHandler<Ping>
    {
        public Task Handle(Ping notification, CancellationToken cancellationToken)
        {
            received.Add(notification.Value);
            return Task.CompletedTask;
        }
    }

    private sealed record Entry(string Category, LogLevel Level, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<Entry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);

        public void Dispose() { }

        private sealed class Logger(string category, ConcurrentQueue<Entry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new Entry(category, logLevel, formatter(state, exception)));
        }
    }

    [Test]
    public async Task Mediator_Should_Dispatch_WithoutALicenseKey_AndLogNothingAboveAWarning()
    {
        Assume.That(Environment.GetEnvironmentVariable("MEDIATR_LICENSE_KEY"), Is.Null.Or.Empty);
        Assume.That(Environment.GetEnvironmentVariable("LUCKYPENNY_LICENSE_KEY"), Is.Null.Or.Empty);

        var logs = new CapturingLoggerProvider();
        var received = new List<int>();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        // As Program.cs registers it: no license key.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServerProgram).Assembly));
        services.AddSingleton(received);
        services.AddTransient<INotificationHandler<Ping>, PingHandler>();

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IPublisher>().Publish(new Ping(1));
        await provider.GetRequiredService<IMediator>().Publish(new Ping(2));

        Assert.That(received, Is.EqualTo(new[] { 1, 2 }));

        var mediatRMajor = typeof(IMediator).Assembly.GetName().Version!.Major;
        if (mediatRMajor >= 13)
        {
            // The check runs on a background thread started with the mediator: wait for its warning.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!logs.Entries.Any(e => e.Category == LicenseCategory) && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            var license = logs.Entries.Where(e => e.Category == LicenseCategory).ToList();
            Assert.That(license, Is.Not.Empty, $"MediatR {mediatRMajor} logged nothing about its license");
            Assert.That(license.Select(e => e.Level), Has.All.EqualTo(LogLevel.Warning));
            Assert.That(license.Select(e => e.Message), Has.Some.Contains("do not have a valid license key"));
        }

        var mediatR = logs.Entries
            .Where(e => e.Category.StartsWith("LuckyPennySoftware", StringComparison.Ordinal)
                        || e.Category.StartsWith("MediatR", StringComparison.Ordinal))
            .ToList();
        Assert.That(mediatR.Where(e => e.Level >= LogLevel.Error), Is.Empty,
            string.Join("\n", mediatR.Select(e => $"{e.Level} {e.Category}: {e.Message}")));
        TestContext.Out.WriteLine($"MediatR {typeof(IMediator).Assembly.GetName().Version}: "
            + string.Join(" | ", mediatR.Select(e => $"{e.Level}: {e.Message}")));
    }
}
