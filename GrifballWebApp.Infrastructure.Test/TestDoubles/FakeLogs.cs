using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>What a test's <see cref="FakeLogger"/> (or a <see cref="FakeLoggerProvider"/>'s loggers) logged at a level, in order.</summary>
internal static class FakeLogs
{
    public static List<string> At(this FakeLogCollector collector, LogLevel level)
        => [.. collector.GetSnapshot().Where(r => r.Level == level).Select(r => r.Message)];

    public static List<string> At(this FakeLogger logger, LogLevel level) => logger.Collector.At(level);
}
