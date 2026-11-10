using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The Halo calls' turns shared through Redis (RedisHaloCallSlots), against a real Valkey: two pods' pacers keep to one
/// quota together, as several workers pulling at once must (GrifballWebApp#237).
/// </summary>
[TestFixture]
[NonParallelizable]
public class RedisHaloCallSlotsTests
{
    private IContainer _redis = null!;
    private string _connectionString = null!;

    [OneTimeSetUp]
    public async Task StartRedis()
    {
        _redis = new ContainerBuilder("docker.io/valkey/valkey:9.1-alpine")
            .WithPortBinding(6379, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await _redis.StartAsync();
        _connectionString = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";
    }

    [OneTimeTearDown]
    public async Task StopRedis() => await _redis.DisposeAsync();

    private static string NewKey() => $"test-{Guid.NewGuid():N}{RedisHaloCallSlots.KeySuffix}";

    [Test]
    public async Task Two_pods_take_turns_one_after_another()
    {
        var key = NewKey();
        await using var podA = new RedisHaloCallSlots(_connectionString, key);
        await using var podB = new RedisHaloCallSlots(_connectionString, key);
        var perCall = TimeSpan.FromMilliseconds(200);
        // Connected first, so the reservations below are taken close together
        await podA.ReserveAsync(TimeSpan.Zero, CancellationToken.None);
        await podB.ReserveAsync(TimeSpan.Zero, CancellationToken.None);

        var waits = (await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => (i % 2 == 0 ? podA : podB).ReserveAsync(perCall, CancellationToken.None)))).Order().ToList();

        // Each turn a PerCall after the one before, whichever pod took it (less the little time between the reservations)
        Assert.That(waits[0], Is.LessThan(TimeSpan.FromMilliseconds(100)));
        for (var i = 1; i < waits.Count; i++)
            Assert.That(waits[i] - waits[i - 1], Is.InRange(TimeSpan.FromMilliseconds(100), perCall + TimeSpan.FromMilliseconds(1)), $"turn {i}");
    }

    [Test]
    public async Task Two_pods_pacers_keep_to_one_quota_together()
    {
        var key = NewKey();
        await using var slotsA = new RedisHaloCallSlots(_connectionString, key);
        await using var slotsB = new RedisHaloCallSlots(_connectionString, key);
        // 600 a minute, 90% used: one every 111 ms, across both pods
        await using var workerA = new HaloCallPacer(600, slotsA);
        await using var workerB = new HaloCallPacer(600, slotsB);
        await slotsA.ReserveAsync(TimeSpan.Zero, CancellationToken.None);
        await slotsB.ReserveAsync(TimeSpan.Zero, CancellationToken.None);

        var clock = Stopwatch.StartNew();
        await Task.WhenAll(Calls(workerA, 3), Calls(workerB, 3));

        // Six calls, the first at once: five turns. Each pod alone would take two.
        Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(workerA.PerCall * 5 - TimeSpan.FromMilliseconds(40)));

        static async Task Calls(HaloCallPacer pacer, int count)
        {
            for (var i = 0; i < count; i++)
                await pacer.WaitAsync();
        }
    }

    [TestCase("127.0.0.1:1,connectTimeout=200,syncTimeout=500,asyncTimeout=500")]
    [TestCase("127.0.0.1:1,abortConnect=true,connectTimeout=200")]
    public async Task Redis_that_cant_be_reached_leaves_each_pod_pacing_its_own_calls(string connectionString)
    {
        await using var slots = new RedisHaloCallSlots(connectionString, NewKey());
        await using var pacer = new HaloCallPacer(600, slots);

        await pacer.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20));
        await pacer.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Test]
    public async Task With_a_Redis_connection_string_the_app_shares_its_turns_under_its_prefix()
    {
        static ServiceProvider Services(Dictionary<string, string?> settings)
        {
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(new HaloInfiniteSettings(HaloInfiniteMode.Gateway, new Uri(HaloInfiniteSettings.DefaultGatewayBaseUrl), null,
                HaloInfiniteSettings.DefaultGatewayAdminUrl));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddHaloCallPacer(configuration);
            return services.BuildServiceProvider();
        }

        await using (var alone = Services(new() { [HaloCallPacer.PermitsPerMinuteKey] = "600", ["ConnectionStrings:Redis"] = " " }))
        {
            Assert.That(alone.GetService<IHaloCallSlots>(), Is.Null);
            Assert.That(alone.GetRequiredService<HaloCallPacer>().Shared, Is.False);
        }

        await using var scaledOut = Services(new() { [HaloCallPacer.PermitsPerMinuteKey] = "600", ["ConnectionStrings:Redis"] = _connectionString, ["SignalR:RedisChannelPrefix"] = "grif-test" });
        Assert.That(((RedisHaloCallSlots)scaledOut.GetRequiredService<IHaloCallSlots>()).Key, Is.EqualTo("grif-test:halo-call-pace"));
        Assert.That(scaledOut.GetRequiredService<HaloCallPacer>().Shared, Is.True);

        Assert.That(RedisHaloCallSlots.KeyFor(new ConfigurationBuilder().Build()), Is.EqualTo("GrifballWebApp:halo-call-pace"));
        Assert.That(RedisHaloCallSlots.KeyFor(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SignalR:RedisChannelPrefix"] = "" }).Build()),
            Is.EqualTo("GrifballWebApp:halo-call-pace"));
    }

    [Test]
    public async Task The_turns_work_on_a_Redis_before_5_which_refuses_a_write_after_TIME_unless_effects_are_replicated()
    {
        // Redis 4: without redis.replicate_commands() the script's SET after TIME fails ("Write commands not allowed
        // after non deterministic commands"), and every pod would pace alone
        await using var redis4 = new ContainerBuilder("docker.io/library/redis:4-alpine")
            .WithPortBinding(6379, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("(?i)ready to accept connections"))
            .Build();
        await redis4.StartAsync();
        await using var slots = new RedisHaloCallSlots($"{redis4.Hostname}:{redis4.GetMappedPublicPort(6379)}", NewKey());

        var first = await slots.ReserveAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        var second = await slots.ReserveAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.That(first, Is.LessThan(TimeSpan.FromSeconds(1)));
        Assert.That(second, Is.InRange(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(10)));
    }

    [Test]
    public async Task The_turns_and_the_SignalR_backplane_share_the_processs_one_Redis_connection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HaloCallPacer.PermitsPerMinuteKey] = "600",
            ["ConnectionStrings:Redis"] = _connectionString,
            ["SignalR:RedisChannelPrefix"] = "grif-shared",
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(new HaloInfiniteSettings(HaloInfiniteMode.Gateway, new Uri(HaloInfiniteSettings.DefaultGatewayBaseUrl), null,
            HaloInfiniteSettings.DefaultGatewayAdminUrl));
        services.AddSingleton<IConfiguration>(configuration);
        GrifballWebApp.Server.SignalR.SignalRBackplaneExtensions.AddAppBackplane(services.AddSignalR(), configuration);
        services.AddHaloCallPacer(configuration);
        await using var provider = services.BuildServiceProvider();

        Assert.That(services.Count(d => d.ServiceType == typeof(GrifballWebApp.Infrastructure.Redis.AppRedis)), Is.EqualTo(1));
        var redis = provider.GetRequiredService<GrifballWebApp.Infrastructure.Redis.AppRedis>();
        var signalR = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.SignalR.StackExchangeRedis.RedisOptions>>().Value;
        var backplane = await signalR.ConnectionFactory!(TextWriter.Null);
        Assert.That(backplane, Is.SameAs(await redis.ConnectAsync()));

        // The turns go through it
        var slots = provider.GetRequiredService<IHaloCallSlots>();
        Assert.That(await slots.ReserveAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None), Is.LessThan(TimeSpan.FromSeconds(1)));
        Assert.That(((RedisHaloCallSlots)slots).Key, Is.EqualTo("grif-shared:halo-call-pace"));
    }
}
