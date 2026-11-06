using GrifballWebApp.Server.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StackExchange.Redis;

namespace GrifballWebApp.Test;

/// <summary>AppRedis: the process's one Redis connection, made once, and made again when a connect failed.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AppRedisTests
{
    [Test]
    public async Task The_connection_is_made_once_and_shared()
    {
        var connects = 0;
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        await using var redis = new AppRedis(_ => { connects++; return Task.FromResult(multiplexer); });

        Assert.That(await redis.ConnectAsync(), Is.SameAs(multiplexer));
        Assert.That(await redis.ConnectAsync(TextWriter.Null), Is.SameAs(multiplexer));
        Assert.That(connects, Is.EqualTo(1));
    }

    [Test]
    public async Task A_connect_that_failed_is_made_again_on_the_next_use_not_kept()
    {
        var connects = 0;
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var redis = new AppRedis(_ => ++connects == 1
            ? throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis isn't there yet")
            : Task.FromResult(multiplexer));

        Assert.ThrowsAsync<RedisConnectionException>(async () => await redis.ConnectAsync());
        Assert.That(await redis.ConnectAsync(), Is.SameAs(multiplexer), "Redis is back");
        Assert.That(connects, Is.EqualTo(2));

        await redis.DisposeAsync();
        await multiplexer.Received(1).DisposeAsync();
    }

    [Test]
    public async Task A_connection_string_that_cant_be_read_fails_the_connect_not_the_caller()
    {
        await using var redis = new AppRedis("not a=valid,,=connection string", "prefix");
        var connect = redis.ConnectAsync();
        Assert.That(connect, Is.Not.Null);
        Assert.CatchAsync<Exception>(async () => await connect);
    }

    [Test]
    public async Task Disposing_before_any_connect_has_nothing_to_close()
    {
        var redis = new AppRedis(_ => throw new InvalidOperationException("never called"));
        await redis.DisposeAsync();
        Assert.Pass();
    }

    [TestCase("127.0.0.1:9", false)]
    [TestCase("127.0.0.1:9,abortConnect=true", true)]
    public void The_options_keep_the_connection_strings_abortConnect_and_put_the_channels_under_the_prefix(string connectionString, bool abort)
    {
        var options = AppRedis.Options(connectionString, "grif-test");
        Assert.That((options.AbortOnConnectFail, options.ChannelPrefix.ToString()), Is.EqualTo((abort, "grif-test")));
    }

    [Test]
    public void It_is_registered_once_and_only_with_a_connection_string()
    {
        static IConfiguration Configuration(string? redis) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = redis }).Build();

        var without = new ServiceCollection();
        Assert.That(without.TryAddAppRedis(Configuration(" ")), Is.False);
        Assert.That(without, Is.Empty);

        var with = new ServiceCollection();
        Assert.That(with.TryAddAppRedis(Configuration("127.0.0.1:9")), Is.True);
        Assert.That(with.TryAddAppRedis(Configuration("127.0.0.1:9")), Is.True);
        Assert.That(with.Count(d => d.ServiceType == typeof(AppRedis)), Is.EqualTo(1));
    }
}
