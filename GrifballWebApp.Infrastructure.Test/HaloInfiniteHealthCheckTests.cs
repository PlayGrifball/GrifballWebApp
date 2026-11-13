using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// HaloInfiniteHealthCheck: the Halo Infinite API probe, its caching and its FromConfiguration reader (the Server's
/// AddAppHealthChecks registration is GrifballWebApp.Server.Test's). No network: IHaloInfiniteClient
/// is substituted, and time is a FakeTimeProvider.
/// </summary>
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

    private static HealthCheckContext Context(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("halo_infinite", check, HealthStatus.Degraded, null),
    };

    private Task<HealthCheckResult> Check(CancellationToken cancellationToken = default) =>
        _check.CheckHealthAsync(Context(_check), cancellationToken);

    private static HaloApiResultContainer<MatchStats, HaloApiErrorContainer> Response(int code) =>
        new(new MatchStats(), new HaloApiErrorContainer { Code = code });

    private void Answering(int code) => _factory.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(code));

    [Test]
    public async Task ApiAnswers_Healthy()
    {
        Answering(200);

        var result = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Description, Does.Contain("200"));
        }
    }

    [Test]
    public async Task Probe_FetchesTheConfiguredMatch()
    {
        Answering(200);
        var check = new HaloInfiniteHealthCheck(_factory, Interval, Timeout, _time, NullLogger<HaloInfiniteHealthCheck>.Instance, "my-match");

        await check.CheckHealthAsync(Context(check));
        await Check();

        await _factory.Received(1).StatsGetMatchStats("my-match");
        await _factory.Received(1).StatsGetMatchStats(HaloInfiniteHealthCheck.DefaultMatchId);
    }

    [TestCase(401)]
    [TestCase(403)] // what the lobby qosservers endpoint answered on test, which is why the probe moved to match stats
    [TestCase(404)]
    [TestCase(503)]
    [TestCase(0)]
    public async Task ApiFails_DegradedNotUnhealthy(int code)
    {
        Answering(code);

        var result = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain(code.ToString()));
        }
    }

    [Test]
    public async Task NullResponse_Degraded()
    {
        _factory.StatsGetMatchStats(Arg.Any<string>()).Returns((HaloApiResultContainer<MatchStats, HaloApiErrorContainer>)null!);

        Assert.That((await Check()).Status, Is.EqualTo(HealthStatus.Degraded));
    }

    [Test]
    public async Task SignInThrows_Degraded_AndNothingEscapes()
    {
        var failure = new InvalidOperationException("Failed to get halo token.");
        _factory.StatsGetMatchStats(Arg.Any<string>()).ThrowsAsync(failure);

        var result = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Exception, Is.SameAs(failure));
            Assert.That(result.Description, Does.Contain(nameof(InvalidOperationException)));
        }
    }

    [Test]
    public async Task ThrowsSynchronously_Degraded()
    {
#pragma warning disable NS5003 // A synchronous throw from the async call is the case under test
        _factory.StatsGetMatchStats(Arg.Any<string>()).Throws(new Exception("ClientId is null or empty"));
#pragma warning restore NS5003

        Assert.That((await Check()).Status, Is.EqualTo(HealthStatus.Degraded));
    }

    [Test]
    public async Task Result_IsCachedForTheInterval()
    {
        Answering(200);

        await Check();
        _time.Advance(Interval - TimeSpan.FromSeconds(1));
        var cached = await Check();
        await _factory.Received(1).StatsGetMatchStats(Arg.Any<string>());

        _factory.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(500));
        _time.Advance(TimeSpan.FromSeconds(1));
        var reprobed = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cached.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(reprobed.Status, Is.EqualTo(HealthStatus.Degraded));
            await _factory.Received(2).StatsGetMatchStats(Arg.Any<string>());
        }
    }

    [Test]
    public async Task Failure_IsCachedToo()
    {
        _factory.StatsGetMatchStats(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("down"));

        await Check();
        var second = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Status, Is.EqualTo(HealthStatus.Degraded));
            await _factory.Received(1).StatsGetMatchStats(Arg.Any<string>());
        }
    }

    [Test]
    public async Task NoAnswer_TimesOut_Degraded_AndIsCached()
    {
        var never = new TaskCompletionSource<HaloApiResultContainer<MatchStats, HaloApiErrorContainer>>();
        _factory.StatsGetMatchStats(Arg.Any<string>()).Returns(never.Task);

        var pending = Check();
        Assert.That(pending.IsCompleted, Is.False);
        _time.Advance(Timeout);
        var result = await pending;
        var cached = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("10 s"));
            Assert.That(cached.Description, Is.EqualTo(result.Description));
            await _factory.Received(1).StatsGetMatchStats(Arg.Any<string>());
        }
    }

    [Test]
    public async Task ConcurrentChecks_ShareOneProbe()
    {
        var answer = new TaskCompletionSource<HaloApiResultContainer<MatchStats, HaloApiErrorContainer>>();
        _factory.StatsGetMatchStats(Arg.Any<string>()).Returns(answer.Task);

        var first = Check();
        var second = Check();
        answer.SetResult(Response(200));
        var results = await Task.WhenAll(first, second);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Select(r => r.Status), Is.All.EqualTo(HealthStatus.Healthy));
            await _factory.Received(1).StatsGetMatchStats(Arg.Any<string>());
        }
    }

    [Test]
    public async Task CallerCancels_FailureStatus_NotCached()
    {
        var never = new TaskCompletionSource<HaloApiResultContainer<MatchStats, HaloApiErrorContainer>>();
        _factory.StatsGetMatchStats(Arg.Any<string>()).Returns(never.Task);
        using var cancellation = new CancellationTokenSource();

        var pending = Check(cancellation.Token);
        await cancellation.CancelAsync();
        var result = await pending;

        Answering(200);
        var next = await Check();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("cancelled"));
            Assert.That(next.Status, Is.EqualTo(HealthStatus.Healthy), "the cancelled run left no cached result");
        }
    }

    [Test]
    public void FromConfiguration_ReadsIntervalTimeoutAndMatchId()
    {
        var services = new ServiceCollection()
            .AddSingleton<IHaloInfiniteClient>(_factory)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .BuildServiceProvider();

        var configured = HaloInfiniteHealthCheck.FromConfiguration(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:HaloInfinite:Interval"] = "00:10:00",
            ["HealthChecks:HaloInfinite:Timeout"] = "00:00:03",
            ["HealthChecks:HaloInfinite:MatchId"] = " my-match ",
        }).Build());
        var invalid = HaloInfiniteHealthCheck.FromConfiguration(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:HaloInfinite:Interval"] = "soon",
            ["HealthChecks:HaloInfinite:Timeout"] = "-00:00:01",
            ["HealthChecks:HaloInfinite:MatchId"] = " ",
        }).Build());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configured.Interval, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(configured.Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(invalid.Interval, Is.EqualTo(HaloInfiniteHealthCheck.DefaultInterval));
            Assert.That(invalid.Timeout, Is.EqualTo(HaloInfiniteHealthCheck.DefaultTimeout));
            Assert.That(configured.MatchId, Is.EqualTo("my-match"));
            Assert.That(invalid.MatchId, Is.EqualTo(HaloInfiniteHealthCheck.DefaultMatchId));
        }
    }

    [Test]
    public void FromConfiguration_NoGatewayRegistered_NotViaGateway()
    {
        var services = new ServiceCollection()
            .AddSingleton<IHaloInfiniteClient>(_factory)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .BuildServiceProvider();

        var check = HaloInfiniteHealthCheck.FromConfiguration(services, new ConfigurationBuilder().Build());

        Assert.That(check.ViaGateway, Is.False);
    }
}
