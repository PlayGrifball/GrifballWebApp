using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using StackExchange.Redis;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// RedisHaloCallSlots: a Halo call's turn reserved in Redis, the pods' shared pace. The connection is substituted
/// (AppRedis takes how it connects), so no Redis: what the script is given (the key, the pace in microseconds) and the
/// wait it answers with. And the key, under the app's Redis prefix.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RedisHaloCallSlotsTests
{
    private readonly IConnectionMultiplexer _connection = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();

    [SetUp]
    public void SetUp() => _connection.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_database);

    private void ScriptAnswers(long micros)
    {
        var result = RedisResult.Create((RedisValue)micros);
        _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]?>(), Arg.Any<RedisValue[]?>(), Arg.Any<CommandFlags>()).Returns(result);
    }

    [Test]
    public async Task Reserve_WaitsWhatTheScriptAnswers_ForThePaceInMicroseconds()
    {
        ScriptAnswers(1_500);
        await using var redis = new AppRedis(_ => Task.FromResult(_connection));
        await using var slots = new RedisHaloCallSlots(redis, "grif:halo-call-pace");

        var wait = await slots.ReserveAsync(TimeSpan.FromTicks(6_666_666), CancellationToken.None);

        Assert.That(wait, Is.EqualTo(TimeSpan.FromMicroseconds(1_500)));
        await _database.Received(1).ScriptEvaluateAsync(Arg.Is<string>(s => s.Contains("redis.call('TIME')")),
            Arg.Is<RedisKey[]?>(k => k != null && k.Length == 1 && k[0] == "grif:halo-call-pace"),
            Arg.Is<RedisValue[]?>(v => v != null && v.Length == 1 && (long)v[0] == 666_667), Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task Reserve_ATurnDueNow_IsNoWait()
    {
        ScriptAnswers(0);
        await using var redis = new AppRedis(_ => Task.FromResult(_connection));
        await using var slots = new RedisHaloCallSlots(redis, "k");

        Assert.That(await slots.ReserveAsync(TimeSpan.FromSeconds(1), CancellationToken.None), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task Reserve_StoppedWhileConnecting_IsCancelled()
    {
        var never = new TaskCompletionSource<IConnectionMultiplexer>();
        var redis = new AppRedis(_ => never.Task);
        var slots = new RedisHaloCallSlots(redis, "k");
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();

        await Assert.ThatAsync(async () => await slots.ReserveAsync(TimeSpan.FromSeconds(1), stop.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase(null, "GrifballWebApp:halo-call-pace")]
    [TestCase("grif-test", "grif-test:halo-call-pace")]
    public void KeyFor_IsUnderTheAppsRedisPrefix(string? prefix, string key)
        => Assert.That(RedisHaloCallSlots.KeyFor(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AppRedisExtensions.ChannelPrefixKey] = prefix }).Build()), Is.EqualTo(key));
}
