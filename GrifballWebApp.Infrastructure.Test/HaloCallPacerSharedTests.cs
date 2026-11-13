using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// HaloCallPacer with the shared pace (IHaloCallSlots, Redis in the app) substituted: a turn the shared pace gives at
/// once goes straight away; while the shared pace can't be reached, the pod paces its calls alone (and says so) and
/// doesn't ask again until SharedRetryAfter; a stop while asking is a cancellation. And the pace configured for the
/// gateway. A high pace, so a call paced alone waits a few milliseconds at most.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloCallPacerSharedTests
{
    private const int FastPace = 600_000;
    private readonly IHaloCallSlots _slots = Substitute.For<IHaloCallSlots>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    /// <summary>The arguments of each ILogger.Log call (level, event, state, exception, formatter).</summary>
    private List<object?[]> Logged() => _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).Select(c => c.GetArguments()).ToList();

    [Test]
    public async Task ASharedTurnDueNow_GoesAtOnce_WithoutThePodsOwnPace()
    {
        _slots.ReserveAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(TimeSpan.Zero);
        await using var pacer = new HaloCallPacer(60, _slots);

        // At 60 a minute the pod's own pace would make the second call wait over 11 seconds: both go at once
        await pacer.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await pacer.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        await _slots.Received(2).ReserveAsync(pacer.PerCall, Arg.Any<CancellationToken>());
        Assert.That(pacer.Shared, Is.True);
    }

    [Test]
    public async Task TheSharedPaceUnreachable_PacesThePodAlone_AndAsksAgainOnlyAfterTheRetryWait()
    {
        _slots.ReserveAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("redis down"));
        await using var pacer = new HaloCallPacer(FastPace, _slots, _logger) { SharedRetryAfter = TimeSpan.FromHours(1) };

        await pacer.WaitAsync();
        await pacer.WaitAsync();

        await _slots.Received(1).ReserveAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        Assert.That(Logged().Select(c => ((LogLevel)c[0]!, c[3]?.GetType())), Is.EqualTo(new[] { (LogLevel.Warning, typeof(TimeoutException)) }), "said once, when it fell back");
    }

    [Test]
    public async Task StoppedWhileAskingTheSharedPace_IsACancellation()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        _slots.ReserveAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        await using var pacer = new HaloCallPacer(FastPace, _slots, _logger);

        await Assert.ThatAsync(async () => await pacer.WaitAsync(stop.Token), Throws.InstanceOf<OperationCanceledException>().With.Message.EqualTo("The app is stopping"));
        Assert.That(Logged(), Is.Empty, "a stop isn't the shared pace being down");
    }

    [TestCase(HaloInfiniteMode.Gateway, 600, true)]
    [TestCase(HaloInfiniteMode.Direct, 600, false)]
    public async Task FromConfiguration_OnlyTheGatewaysCallsArePaced(HaloInfiniteMode mode, int permitsPerMinute, bool paced)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HaloCallPacer.PermitsPerMinuteKey] = permitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build();
        var settings = new HaloInfiniteSettings(mode, new Uri(HaloInfiniteSettings.DefaultGatewayBaseUrl), null, HaloInfiniteSettings.DefaultGatewayAdminUrl);

        await using var pacer = new HaloCallPacer(configuration, settings);

        Assert.That(pacer.PerCall, Is.EqualTo(paced ? TimeSpan.FromMinutes(1) / (permitsPerMinute * HaloCallPacer.QuotaShare) : TimeSpan.Zero));
    }
}
