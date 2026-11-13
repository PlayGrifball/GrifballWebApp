using System.Diagnostics;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Ollama;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OllamaSharp;
using Quartz;
using Quartz.Diagnostics;
using XboxUser = GrifballWebApp.Domain.Players.XboxUser;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// How some services are put together, checked by what they do: the distributed cache (memory without Redis; Redis,
/// under the app's prefix, with it), the bot's chat client, a recurring job's run left out of the traces, the Halo calls'
/// shared pace (Redis's, when there is one), and the hourly job that gives Xbox accounts nobody holds a placeholder (on a
/// temp-file SQLite GrifballContext). Nothing connects to Redis, Ollama or a database server.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class WiringPartsTests : SqliteContextFixture
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Test]
    public async Task DistributedCache_WithoutRedis_IsInMemory_AndKeepsWhatsSet()
    {
        await using var services = new ServiceCollection().AddLogging().AddAppDistributedCache(Configuration()).BuildServiceProvider();
        var cache = services.GetRequiredService<IDistributedCache>();

        await cache.SetStringAsync("k", "v");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cache, Is.TypeOf<MemoryDistributedCache>());
            Assert.That(await cache.GetStringAsync("k"), Is.EqualTo("v"));
        }
    }

    [Test]
    public async Task DistributedCache_WithRedis_KeepsItsKeysUnderTheAppsPrefix_OnTheAppsConnection()
    {
        await using var services = new ServiceCollection().AddLogging()
            .AddAppDistributedCache(Configuration(("ConnectionStrings:Redis", "redis.test:6379,abortConnect=false"), (AppRedisExtensions.ChannelPrefixKey, "grif-test")))
            .BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<RedisCacheOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.InstanceName, Is.EqualTo("grif-test:"));
            Assert.That(services.GetRequiredService<IDistributedCache>(), Is.TypeOf<RedisCache>());
        }
    }

    [Test]
    public async Task HaloCallPacer_WithRedis_SharesItsPaceThere_UnderTheAppsPrefix()
    {
        var configuration = Configuration(("ConnectionStrings:Redis", "redis.test:6379,abortConnect=false"), (AppRedisExtensions.ChannelPrefixKey, "grif-test"));
        await using var services = new ServiceCollection().AddLogging().AddHaloCallPacer(configuration).BuildServiceProvider();

        var slots = services.GetRequiredService<IHaloCallSlots>();

        Assert.That(slots, Is.TypeOf<RedisHaloCallSlots>().And.Property(nameof(RedisHaloCallSlots.Key)).EqualTo("grif-test:halo-call-pace"));
    }

    [Test]
    public void ChatClient_TalksToTheConfiguredOllama_AndRunsTheBotsFunctions()
    {
        var client = new OllamaChatClients(Configuration(("Ollama:Url", "http://ollama.test:11434/"))).Create();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(client, Is.TypeOf<FunctionInvokingChatClient>());
            Assert.That(client.GetService<OllamaApiClient>()?.Uri, Is.EqualTo(new Uri("http://ollama.test:11434/")));
            Assert.That(client.GetService<OllamaApiClient>()?.SelectedModel, Is.EqualTo(OllamaChatClients.Model));
        }
    }

    [Test]
    public void DontRecordThisRun_TakesAJobsRunOutOfTheTraces()
    {
        using var source = new ActivitySource(QuartzInstrumentation.ActivitySourceName);
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var run = source.StartActivity("run")!;

        JobTracing.DontRecordThisRun();

        Assert.That((run.IsAllDataRequested, run.Recorded), Is.EqualTo((false, false)));
    }

    [Test]
    public void DontRecordThisRun_LeavesAnyOtherActivityAlone()
    {
        using var source = new ActivitySource("GrifballWebApp.Tests.Other");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var other = source.StartActivity("request")!;

        JobTracing.DontRecordThisRun();

        Assert.That((other.IsAllDataRequested, other.Recorded), Is.EqualTo((true, true)));
    }

    [Test]
    public async Task XboxPlaceholders_AnAccountNobodyHolds_GetsAPlaceholderUser()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(4242, "Loner"));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await new XboxPlaceholderJob(new GamertagMatching(Context), NullLogger<XboxPlaceholderJob>.Instance).Execute(Substitute.For<IJobExecutionContext>());

        Context.ChangeTracker.Clear();
        var holder = await Context.Users.AsNoTracking().SingleAsync(u => u.XboxUserID == 4242);
        Assert.That(holder.UserName, Is.EqualTo(HistoricalKeys.DummyUserName("Loner")));
    }

    [Test]
    public async Task XboxPlaceholders_EveryAccountHeld_MakesNone()
    {
        await new XboxPlaceholderJob(new GamertagMatching(Context), NullLogger<XboxPlaceholderJob>.Instance).Execute(Substitute.For<IJobExecutionContext>());

        Assert.That(await Context.Users.AnyAsync(), Is.False);
    }
}
