using System.Diagnostics;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;

namespace GrifballWebApp.Test;

/// <summary>HaloCallPacer: a pull's Halo calls spaced to the gateway key's quota, or not at all.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloCallPacerTests
{
    private static HaloInfiniteSettings Settings(HaloInfiniteMode mode) =>
        new(mode, new Uri(HaloInfiniteSettings.DefaultGatewayBaseUrl), null, HaloInfiniteSettings.DefaultGatewayAdminUrl);

    private static IConfiguration Configuration(string? permitsPerMinute) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [HaloCallPacer.PermitsPerMinuteKey] = permitsPerMinute })
        .Build();

    [TestCase(null)]
    [TestCase("0")]
    public async Task Without_a_quota_calls_go_at_once(string? permitsPerMinute)
    {
        await using var pacer = new HaloCallPacer(Configuration(permitsPerMinute), Settings(HaloInfiniteMode.Gateway));
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
            await pacer.WaitAsync();
        Assert.That(pacer.PerCall, Is.EqualTo(TimeSpan.Zero));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Direct_mode_has_no_gateway_quota_to_keep_to()
    {
        await using var pacer = new HaloCallPacer(Configuration("120"), Settings(HaloInfiniteMode.Direct));
        Assert.That(pacer.PerCall, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task With_a_quota_calls_are_spaced_a_little_under_it()
    {
        // 600 a minute, 90% used: one every 111 ms
        await using var pacer = new HaloCallPacer(Configuration("600"), Settings(HaloInfiniteMode.Gateway));
        Assert.That(pacer.PerCall.TotalMilliseconds, Is.EqualTo(60_000 / (600 * HaloCallPacer.QuotaShare)).Within(0.001));

        var clock = Stopwatch.StartNew();
        // The first goes at once, the next three each a PerCall later
        for (var i = 0; i < 4; i++)
            await pacer.WaitAsync();
        Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(pacer.PerCall * 3 - TimeSpan.FromMilliseconds(40)));
    }

    [Test]
    public async Task A_cancelled_wait_stops_waiting()
    {
        await using var pacer = new HaloCallPacer(6);
        await pacer.WaitAsync();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.CatchAsync<OperationCanceledException>(async () => await pacer.WaitAsync(cancel.Token));
    }

    [Test]
    public async Task Once_the_app_is_stopping_a_wait_is_cancelled_not_let_through_unpaced()
    {
        var pacer = new HaloCallPacer(600);
        await pacer.DisposeAsync();

        await Assert.CatchAsync<OperationCanceledException>(async () => await pacer.WaitAsync());
    }

    [Test]
    public async Task A_call_already_waiting_when_the_app_stops_is_cancelled_not_let_through_unpaced()
    {
        // 6 a minute: after the first, the next waits about 11 s
        var pacer = new HaloCallPacer(6);
        await pacer.WaitAsync();
        var waiting = pacer.WaitAsync().AsTask();
        await Task.Delay(100);
        Assert.That(waiting.IsCompleted, Is.False, "Waiting its turn");

        await pacer.DisposeAsync();

        await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
    }

    /// <summary>Turns shared with other pods, standing in for Redis: each reservation waits <paramref name="wait"/>, or throws.</summary>
    private sealed class Slots(TimeSpan wait, Exception? fails = null) : IHaloCallSlots
    {
        public readonly List<TimeSpan> Reserved = [];

        public Task<TimeSpan> ReserveAsync(TimeSpan perCall, CancellationToken ct)
        {
            lock (Reserved)
                Reserved.Add(perCall);
            return fails is null ? Task.FromResult(wait) : Task.FromException<TimeSpan>(fails);
        }
    }

    [Test]
    public async Task Shared_turns_are_waited_for_instead_of_this_pods_own()
    {
        var slots = new Slots(TimeSpan.FromMilliseconds(300));
        await using var pacer = new HaloCallPacer(600, slots);
        Assert.That(pacer.Shared, Is.True);

        var clock = Stopwatch.StartNew();
        await pacer.WaitAsync();
        await pacer.WaitAsync();

        // Each waited the turn Redis gave it, not the pod's own bucket (whose first call goes at once)
        Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(580)));
        Assert.That(slots.Reserved, Is.EqualTo(new[] { pacer.PerCall, pacer.PerCall }));
    }

    [Test]
    public async Task Without_a_quota_the_shared_turns_are_never_asked()
    {
        var slots = new Slots(TimeSpan.FromSeconds(10));
        await using var pacer = new HaloCallPacer(0, slots);
        await pacer.WaitAsync();
        Assert.That((pacer.Shared, slots.Reserved.Count), Is.EqualTo((false, 0)));
    }

    [Test]
    public async Task Shared_turns_that_cant_be_reached_fall_back_to_this_pods_pace_for_a_while()
    {
        var slots = new Slots(TimeSpan.Zero, new InvalidOperationException("Redis is down"));
        await using var pacer = new HaloCallPacer(600, slots, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
            await pacer.WaitAsync();

        // Paced here (two waits of a PerCall), and Redis asked once in SharedRetryAfter, not once a call
        Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(pacer.PerCall * 2 - TimeSpan.FromMilliseconds(40)));
        Assert.That(slots.Reserved, Has.Count.EqualTo(1));

        await using var retrying = new HaloCallPacer(600, slots) { SharedRetryAfter = TimeSpan.Zero };
        await retrying.WaitAsync();
        await retrying.WaitAsync();
        Assert.That(slots.Reserved, Has.Count.EqualTo(3), "Asked again once SharedRetryAfter is past");
    }

    [Test]
    public async Task A_call_waiting_for_its_shared_turn_when_the_app_stops_is_cancelled()
    {
        var pacer = new HaloCallPacer(600, new Slots(TimeSpan.FromSeconds(30)));
        var waiting = pacer.WaitAsync().AsTask();
        await Task.Delay(100);
        Assert.That(waiting.IsCompleted, Is.False, "Waiting its turn");

        await pacer.DisposeAsync();

        await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
    }

    [Test]
    public async Task A_cancelled_wait_for_a_shared_turn_stops_waiting()
    {
        await using var pacer = new HaloCallPacer(600, new Slots(TimeSpan.FromSeconds(30)));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.CatchAsync<OperationCanceledException>(async () => await pacer.WaitAsync(cancel.Token));
    }

    [Test]
    public async Task A_shared_turn_cancelled_while_reserving_is_cancelled_not_paced_here()
    {
        await using var pacer = new HaloCallPacer(600, new Slots(TimeSpan.Zero, new OperationCanceledException()));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        await Assert.CatchAsync<OperationCanceledException>(async () => await pacer.WaitAsync(cancel.Token));
    }

    [Test]
    public void A_long_jobs_spans_have_room_in_the_export_queue()
    {
        var batches = new BatchExportProcessorOptions<Activity>();
        MetricsAndTracingExtensions.ConfigureTraceBatches(batches);
        Assert.That((batches.MaxQueueSize, batches.MaxExportBatchSize, batches.ScheduledDelayMilliseconds), Is.EqualTo((32768, 512, 1000)));
    }
}
