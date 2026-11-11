using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Database;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The leaderboards' cache (<see cref="AppDistributedCacheExtensions.AddAppDistributedCache"/>): the process's memory with
/// one process, Redis shared by every API pod when scaled out, so a reload on any pod shows the same boards, against a
/// real Valkey.
/// </summary>
[TestFixture]
[NonParallelizable]
public class LeaderboardsCacheTests
{
    private static readonly string[] FirstOnly = ["First"];

    private IContainer _redis = null!;
    private string _connectionString = null!;
    private GrifballContext _context = null!;

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

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>An API pod: the app's cache and the leaderboards, over the test's database.</summary>
    private ServiceProvider Pod(Dictionary<string, string?> settings) => new ServiceCollection()
        .AddLogging()
        .AddSingleton(_context)
        .AddSingleton<IApplicationDbContext>(_context)
        .AddTransient<SeasonStatsService>()
        .AddTransient<MatchmakingStatsService>()
        .AddTransient<LeaderboardsService>()
        .AddAppDistributedCache(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
        .BuildServiceProvider();

    /// <summary>A season won by a team captained by <paramref name="gamertag"/>.</summary>
    private async Task<int> AddChampion(string gamertag, int year)
    {
        var season = Season.Seed(seasonName: $"Season {year}").WithDates(seasonStart: new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var captain = await _context.AddUser(gamertag.ToLowerInvariant(), gamertag: gamertag);
        var team = await _context.AddTeam(season, gamertag + "'s Team", captain, 1);
        await _context.SeedChampionsAsync(season.SeasonID, "Open", team.TeamID, null, null);
        _context.ChangeTracker.Clear();
        return captain.Id;
    }

    private static async Task<string[]> RingLeaders(ServiceProvider pod)
        => (await pod.GetRequiredService<LeaderboardsService>().GetAsync(null)).RingLeaders.Select(r => r.Gamertag).ToArray();

    /// <summary>How many the spotlight ranks the player among on the championships board.</summary>
    private static async Task<int?> RingsRanked(ServiceProvider pod, int userID)
        => (await pod.GetRequiredService<LeaderboardsService>().GetPlayerRankAsync(null, userID, null))?.Rings.TotalRanked;

    [Test]
    public async Task Without_Redis_the_boards_are_cached_in_the_process()
    {
        await using var pod = Pod(new());
        var first = await AddChampion("First", 2020);

        var before = await RingLeaders(pod);
        var rankedBefore = await RingsRanked(pod, first);
        await AddChampion("Second", 2021);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pod.GetRequiredService<IDistributedCache>(), Is.InstanceOf<MemoryDistributedCache>());
            Assert.That(before, Is.EqualTo(FirstOnly));
            Assert.That(await RingLeaders(pod), Is.EqualTo(before), "Cached: the new title shows once it expires");
            Assert.That(await RingsRanked(pod, first), Is.EqualTo(rankedBefore).And.EqualTo(1), "The spotlight ranks on the cached counts too");
        }
    }

    [Test]
    public async Task With_Redis_every_pod_shows_the_boards_one_pod_cached()
    {
        // The test's own prefix: keys of other runs (and environments sharing a server) are not its
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = _connectionString,
            ["SignalR:RedisChannelPrefix"] = $"test-{Guid.NewGuid():N}",
        };
        await using var podA = Pod(settings);
        await using var podB = Pod(settings);
        var first = await AddChampion("First", 2020);

        var fromA = await RingLeaders(podA);
        var rankedFromA = await RingsRanked(podA, first);
        // A result reported after pod A cached the boards: pod B shows what pod A cached, not a newer count of its own
        await AddChampion("Second", 2021);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(podA.GetRequiredService<IDistributedCache>(), Is.InstanceOf<RedisCache>());
            Assert.That(fromA, Is.EqualTo(FirstOnly));
            Assert.That(await RingLeaders(podB), Is.EqualTo(fromA));
            Assert.That(await RingsRanked(podB, first), Is.EqualTo(rankedFromA).And.EqualTo(1));
        }

        // Under the app's prefix, on the pods' one connection
        var redis = await podA.GetRequiredService<AppRedis>().ConnectAsync();
        Assert.That(await redis.GetDatabase().KeyExistsAsync($"{settings["SignalR:RedisChannelPrefix"]}:leaderboards:boards:all"), Is.True);
    }

    [Test]
    public async Task With_Redis_down_the_boards_are_computed_not_failed()
    {
        // Nothing listens there, and the connect gives up at once
        await using var pod = Pod(new() { ["ConnectionStrings:Redis"] = "127.0.0.1:9,connectTimeout=200" });
        await AddChampion("First", 2020);

        Assert.That(await RingLeaders(pod), Is.EqualTo(FirstOnly));
    }
}
