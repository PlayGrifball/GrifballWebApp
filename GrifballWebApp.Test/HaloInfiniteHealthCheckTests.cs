using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using QosServer = Surprenant.Grunt.Models.HaloInfinite.Server;

namespace GrifballWebApp.Test;

[TestFixture]
public class HaloInfiniteHealthCheckTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private IHaloInfiniteClientFactory _factory = null!;
    private FakeTimeProvider _time = null!;
    private HaloInfiniteHealthCheck _check = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = Substitute.For<IHaloInfiniteClientFactory>();
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        _check = new HaloInfiniteHealthCheck(_factory, Interval, Timeout, _time, NullLogger<HaloInfiniteHealthCheck>.Instance);
    }

    /// <summary>A factory whose probe answers with <paramref name="code"/>.</summary>
    public static IHaloInfiniteClientFactory Answering(int code, IHaloInfiniteClientFactory? factory = null)
    {
        factory ??= Substitute.For<IHaloInfiniteClientFactory>();
        factory.LobbyGetQosServers().Returns(Response(code));
        return factory;
    }

    private static HaloApiResultContainer<List<QosServer>, HaloApiErrorContainer> Response(int code) =>
        new([], new HaloApiErrorContainer { Code = code });

    private static HealthCheckContext Context(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("halo_infinite", check, HealthStatus.Degraded, null),
    };

    private Task<HealthCheckResult> Check(CancellationToken cancellationToken = default) =>
        _check.CheckHealthAsync(Context(_check), cancellationToken);

    [Test]
    public async Task ApiAnswers_Healthy()
    {
        Answering(200, _factory);

        var result = await Check();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Description, Does.Contain("200"));
        });
    }

    [TestCase(401)]
    [TestCase(503)]
    [TestCase(0)]
    public async Task ApiFails_DegradedNotUnhealthy(int code)
    {
        Answering(code, _factory);

        var result = await Check();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain(code.ToString()));
        });
    }

    [Test]
    public async Task NullResponse_Degraded()
    {
        _factory.LobbyGetQosServers().Returns((HaloApiResultContainer<List<QosServer>, HaloApiErrorContainer>)null!);

        Assert.That((await Check()).Status, Is.EqualTo(HealthStatus.Degraded));
    }

    [Test]
    public async Task SignInThrows_Degraded_AndNothingEscapes()
    {
        var failure = new InvalidOperationException("Failed to get halo token.");
        _factory.LobbyGetQosServers().ThrowsAsync(failure);

        var result = await Check();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Exception, Is.SameAs(failure));
            Assert.That(result.Description, Does.Contain(nameof(InvalidOperationException)));
        });
    }

    [Test]
    public async Task ThrowsSynchronously_Degraded()
    {
        _factory.LobbyGetQosServers().Throws(new Exception("ClientId is null or empty"));

        Assert.That((await Check()).Status, Is.EqualTo(HealthStatus.Degraded));
    }

    [Test]
    public async Task Result_IsCachedForTheInterval()
    {
        Answering(200, _factory);

        await Check();
        _time.Advance(Interval - TimeSpan.FromSeconds(1));
        var cached = await Check();
        await _factory.Received(1).LobbyGetQosServers();

        _factory.LobbyGetQosServers().Returns(Response(500));
        _time.Advance(TimeSpan.FromSeconds(1));
        var reprobed = await Check();

        Assert.Multiple(async () =>
        {
            Assert.That(cached.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(reprobed.Status, Is.EqualTo(HealthStatus.Degraded));
            await _factory.Received(2).LobbyGetQosServers();
        });
    }

    [Test]
    public async Task Failure_IsCachedToo()
    {
        _factory.LobbyGetQosServers().ThrowsAsync(new HttpRequestException("down"));

        await Check();
        var second = await Check();

        Assert.Multiple(async () =>
        {
            Assert.That(second.Status, Is.EqualTo(HealthStatus.Degraded));
            await _factory.Received(1).LobbyGetQosServers();
        });
    }

    [Test]
    public async Task NoAnswer_TimesOut_Degraded_AndIsCached()
    {
        var never = new TaskCompletionSource<HaloApiResultContainer<List<QosServer>, HaloApiErrorContainer>>();
        _factory.LobbyGetQosServers().Returns(never.Task);

        var pending = Check();
        Assert.That(pending.IsCompleted, Is.False);
        _time.Advance(Timeout);
        var result = await pending;
        var cached = await Check();

        Assert.Multiple(async () =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("10 s"));
            Assert.That(cached.Description, Is.EqualTo(result.Description));
            await _factory.Received(1).LobbyGetQosServers();
        });
    }

    [Test]
    public async Task ConcurrentChecks_ShareOneProbe()
    {
        var answer = new TaskCompletionSource<HaloApiResultContainer<List<QosServer>, HaloApiErrorContainer>>();
        _factory.LobbyGetQosServers().Returns(answer.Task);

        var first = Check();
        var second = Check();
        answer.SetResult(Response(200));
        var results = await Task.WhenAll(first, second);

        Assert.Multiple(async () =>
        {
            Assert.That(results.Select(r => r.Status), Is.All.EqualTo(HealthStatus.Healthy));
            await _factory.Received(1).LobbyGetQosServers();
        });
    }

    [Test]
    public async Task CallerCancels_FailureStatus_NotCached()
    {
        var never = new TaskCompletionSource<HaloApiResultContainer<List<QosServer>, HaloApiErrorContainer>>();
        _factory.LobbyGetQosServers().Returns(never.Task);
        using var cancellation = new CancellationTokenSource();

        var pending = Check(cancellation.Token);
        await cancellation.CancelAsync();
        var result = await pending;

        Answering(200, _factory);
        var next = await Check();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("cancelled"));
            Assert.That(next.Status, Is.EqualTo(HealthStatus.Healthy), "the cancelled run left no cached result");
        });
    }

    [Test]
    public void FromConfiguration_ReadsIntervalAndTimeout()
    {
        var services = new ServiceCollection()
            .AddSingleton(_factory)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .BuildServiceProvider();

        var configured = HaloInfiniteHealthCheck.FromConfiguration(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:HaloInfinite:Interval"] = "00:10:00",
            ["HealthChecks:HaloInfinite:Timeout"] = "00:00:03",
        }).Build());
        var invalid = HaloInfiniteHealthCheck.FromConfiguration(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:HaloInfinite:Interval"] = "soon",
            ["HealthChecks:HaloInfinite:Timeout"] = "-00:00:01",
        }).Build());

        Assert.Multiple(() =>
        {
            Assert.That(configured.Interval, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(configured.Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(invalid.Interval, Is.EqualTo(HaloInfiniteHealthCheck.DefaultInterval));
            Assert.That(invalid.Timeout, Is.EqualTo(HaloInfiniteHealthCheck.DefaultTimeout));
        });
    }

    [Test]
    public void AddAppHealthChecks_RegistersHaloAsDegradedOnly_AndOneSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Answering(200));
        services.AddAppHealthChecks(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single(r => r.Name == "halo_infinite");

        Assert.Multiple(() =>
        {
            Assert.That(registration.FailureStatus, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(registration.Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registration.Factory(provider), Is.SameAs(registration.Factory(provider)), "the cache lives in one instance");
            Assert.That(provider.GetRequiredService<HaloInfiniteHealthCheck>().Interval, Is.EqualTo(HaloInfiniteHealthCheck.DefaultInterval));
        });
    }
}
