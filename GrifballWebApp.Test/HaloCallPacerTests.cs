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

    [Test]
    public void A_long_jobs_spans_have_room_in_the_export_queue()
    {
        var batches = new BatchExportProcessorOptions<Activity>();
        MetricsAndTracingExtensions.ConfigureTraceBatches(batches);
        Assert.That((batches.MaxQueueSize, batches.MaxExportBatchSize, batches.ScheduledDelayMilliseconds), Is.EqualTo((32768, 512, 1000)));
    }
}
