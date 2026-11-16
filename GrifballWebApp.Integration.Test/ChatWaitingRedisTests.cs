using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The chat's waiting changes (<see cref="IChatWaiting"/>) as Program.cs registers them: the process's memory with one process, Redis
/// shared by every API pod when scaled out, where of two answers at once on different pods only one takes the change, against a
/// real Valkey.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ChatWaitingRedisTests
{
    private static readonly ChatAction Action = new("a1", "ollama", "forfeit_season_match(seasonMatchID: 12)",
        [new ChatActionCall("r1", "call-1", "forfeit_season_match", "{\"seasonMatchID\":12}")]);

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

    /// <summary>An API pod's waiting changes, registered as Program.cs does: the application's default, then the app's cache.</summary>
    private static ServiceProvider Pod(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection().AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IChatWaiting, MemoryChatWaiting>();
        return services.AddAppDistributedCache(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()).BuildServiceProvider();
    }

    [Test]
    public void Without_Redis_they_wait_in_the_process()
    {
        using var pod = Pod(new());

        Assert.That(pod.GetRequiredService<IChatWaiting>(), Is.InstanceOf<MemoryChatWaiting>());
    }

    [Test]
    public async Task With_Redis_every_pod_sees_the_change_and_one_answer_takes_it()
    {
        var prefix = $"test-{Guid.NewGuid():N}";
        var settings = new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = _connectionString, ["SignalR:RedisChannelPrefix"] = prefix };
        await using var podA = Pod(settings);
        await using var podB = Pod(settings);
        var onA = podA.GetRequiredService<IChatWaiting>();
        var onB = podB.GetRequiredService<IChatWaiting>();

        await onA.SaveAsync("chat:waiting:site:user:7", Action, TimeSpan.FromMinutes(10));
        var seenOnB = await onB.PeekAsync("chat:waiting:site:user:7");
        var redis = (await podA.GetRequiredService<AppRedis>().ConnectAsync()).GetDatabase();
        var expiresIn = await redis.KeyTimeToLiveAsync($"{prefix}:chat:waiting:site:user:7");
        var taken = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => (i % 2 == 0 ? onA : onB).TakeAsync("chat:waiting:site:user:7")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(onA, Is.InstanceOf<RedisChatWaiting>());
            Assert.That(seenOnB, Is.EqualTo(Action with { Calls = seenOnB!.Calls }));
            Assert.That(seenOnB.Calls, Is.EqualTo(Action.Calls));
            Assert.That(expiresIn, Is.Not.Null.And.LessThanOrEqualTo(TimeSpan.FromMinutes(10)), "Under the app's prefix, expiring");
            Assert.That(taken.Count(a => a is not null), Is.EqualTo(1), "Taken once");
            Assert.That(await onB.PeekAsync("chat:waiting:site:user:7"), Is.Null);
        }

        await onA.SaveAsync("chat:waiting:site:user:7", Action, TimeSpan.FromMinutes(10));
        await onB.ForgetAsync("chat:waiting:site:user:7");
        Assert.That(await onA.PeekAsync("chat:waiting:site:user:7"), Is.Null, "Forgotten everywhere");
    }
}
