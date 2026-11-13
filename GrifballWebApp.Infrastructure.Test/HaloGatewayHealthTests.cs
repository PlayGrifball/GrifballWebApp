using System.Net;
using System.Text;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using static GrifballWebApp.Infrastructure.Test.HaloInfiniteHealthCheckTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The Halo gateway's own account status (HaloGatewayAccountStatus, over a fake HTTP handler: what /health answers) and
/// HaloInfiniteHealthCheck through the gateway: the account's status beside the test call, the gateway's own hints for
/// a rejected key or its quota, and a gateway that doesn't answer in time (a fake clock). No network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloGatewayHealthTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly IHaloInfiniteClientFactory _client = Substitute.For<IHaloInfiniteClientFactory>();
    private readonly IHaloGatewayAccountStatus _gateway = Substitute.For<IHaloGatewayAccountStatus>();

    // ---- HaloGatewayAccountStatus ----

    /// <summary>Answers every request as told, and remembers the last one's address.</summary>
    private sealed class FakeGateway(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public Uri? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request.RequestUri;
            return Task.FromResult(answer());
        }
    }

    private static HttpResponseMessage Answer(HttpStatusCode code, string body) => new(code) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };

    private static (HaloGatewayAccountStatus Status, FakeGateway Handler) StatusOn(Func<HttpResponseMessage> answer)
    {
        var handler = new FakeGateway(answer);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HaloInfiniteServiceExtensions.GatewayStatusClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://halo-gateway.test/") });
        return (new HaloGatewayAccountStatus(factory, NullLogger<HaloGatewayAccountStatus>.Instance), handler);
    }

    [TestCase(HttpStatusCode.OK, " Healthy\n", "Healthy", true)]
    [TestCase(HttpStatusCode.OK, "Degraded", "Degraded", false)]
    [TestCase(HttpStatusCode.ServiceUnavailable, "Unhealthy", "Unhealthy", false)]
    public async Task Account_TheStatusWordHealthAnswersWith(HttpStatusCode code, string body, string status, bool healthy)
    {
        var (gateway, handler) = StatusOn(() => Answer(code, body));

        var account = await gateway.GetAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(account, Is.EqualTo(new HaloGatewayAccount(true, status, null)));
            Assert.That(account.Healthy, Is.EqualTo(healthy));
            Assert.That(handler.Asked, Is.EqualTo(new Uri("http://halo-gateway.test/health")));
        }
    }

    [Test]
    public async Task Account_ALongAnswer_IsCutToAStatusWord()
    {
        var (gateway, _) = StatusOn(() => Answer(HttpStatusCode.OK, new string('x', 100)));

        Assert.That((await gateway.GetAsync()).Status, Is.EqualTo(new string('x', 32)));
    }

    [TestCase(HttpStatusCode.InternalServerError, "Healthy")]
    [TestCase(HttpStatusCode.OK, "  ")]
    public async Task Account_AnAnswerWithNoStatus_IsReachable_SayingWhatItAnswered(HttpStatusCode code, string body)
    {
        var (gateway, _) = StatusOn(() => Answer(code, body));

        Assert.That(await gateway.GetAsync(), Is.EqualTo(new HaloGatewayAccount(true, null, $"gateway /health answered {(int)code}")));
    }

    [Test]
    public async Task Account_NoAnswer_IsUnreachable_ByTheFailuresKind()
    {
        var (gateway, _) = StatusOn(() => throw new HttpRequestException("connection refused"));

        var account = await gateway.GetAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(account, Is.EqualTo(HaloGatewayAccount.Unreachable("HttpRequestException")));
            Assert.That(account.Healthy, Is.False);
        }
    }

    // ---- HaloInfiniteHealthCheck through the gateway ----

    private HaloInfiniteHealthCheck Check() => new(_client, TimeSpan.FromMinutes(5), Timeout, _time, NullLogger<HaloInfiniteHealthCheck>.Instance, gateway: _gateway);

    private static HealthCheckContext Context(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("halo_infinite", check, HealthStatus.Degraded, null),
    };

    private void GatewaySays(HaloGatewayAccount account) => _gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(account);

    [Test]
    public async Task ViaGateway_AHealthyAccountAndTestCall_IsHealthy()
    {
        Answering(200, _client);
        GatewaySays(new HaloGatewayAccount(true, "Healthy", null));
        var check = Check();

        var result = await check.CheckHealthAsync(Context(check));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.ViaGateway, Is.True);
            Assert.That((result.Status, result.Description), Is.EqualTo((HealthStatus.Healthy, (string?)"Halo gateway account Healthy; test call: Halo Infinite API answered 200")));
        }
    }

    [Test]
    public async Task ViaGateway_ADegradedAccount_FailsTheCheck_EvenWithTheTestCallAnswered()
    {
        Answering(200, _client);
        GatewaySays(new HaloGatewayAccount(true, "Degraded", null));
        var check = Check();

        var result = await check.CheckHealthAsync(Context(check));

        Assert.That((result.Status, result.Description), Is.EqualTo((HealthStatus.Degraded, (string?)"Halo gateway account Degraded; test call: Halo Infinite API answered 200")));
    }

    [Test]
    public async Task ViaGateway_AnUnreachableGateway_SaysWhy()
    {
        Answering(200, _client);
        GatewaySays(HaloGatewayAccount.Unreachable("HttpRequestException"));
        var check = Check();

        var result = await check.CheckHealthAsync(Context(check));

        Assert.That(result.Description, Is.EqualTo("Halo gateway unreachable (HttpRequestException); test call: Halo Infinite API answered 200"));
    }

    [Test]
    public async Task ViaGateway_AGatewayAnsweringWithNoStatus_SaysWhatItAnswered()
    {
        Answering(200, _client);
        GatewaySays(new HaloGatewayAccount(true, null, "gateway /health answered 500"));
        var check = Check();

        var result = await check.CheckHealthAsync(Context(check));

        Assert.That(result.Description, Is.EqualTo("Halo gateway reachable (gateway /health answered 500); test call: Halo Infinite API answered 200"));
    }

    [TestCase(401, " (the gateway rejected the API key, HaloGateway:ApiKey)")]
    [TestCase(429, " (the gateway's quota for this key)")]
    [TestCase(500, "")]
    public async Task ViaGateway_TheGatewaysOwnAnswers_CarryAHint(int code, string hint)
    {
        Answering(code, _client);
        GatewaySays(new HaloGatewayAccount(true, "Healthy", null));
        var check = Check();

        var result = await check.CheckHealthAsync(Context(check));

        Assert.That(result.Description, Is.EqualTo($"Halo gateway account Healthy; test call: Halo Infinite API answered {code}{hint}"));
    }

    [Test]
    public async Task ViaGateway_AStatusCallThatThrows_IsUnreachable()
    {
        Answering(200, _client);
        _gateway.GetAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("no client"));
        var check = Check();

        var result = await check.CheckHealthAsync(Context(check));

        Assert.That(result.Description, Does.StartWith("Halo gateway unreachable (InvalidOperationException)"));
    }

    [Test]
    public async Task ViaGateway_AStatusCallThatNeverAnswers_GivesUpAtTheTimeout()
    {
        Answering(200, _client);
        var never = new TaskCompletionSource<HaloGatewayAccount>();
        _gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(never.Task);
        var check = Check();

        var checking = check.CheckHealthAsync(Context(check));
        _time.Advance(Timeout);
        var result = await checking;

        Assert.That(result.Description, Does.StartWith("Halo gateway unreachable (no answer within 10 s)"));
    }

    [Test]
    public async Task ProbeNow_ProbesAgain_EvenWithAFreshResult()
    {
        Answering(200, _client);
        GatewaySays(new HaloGatewayAccount(true, "Healthy", null));
        var check = Check();
        await check.CheckHealthAsync(Context(check));

        var probe = await check.ProbeNowAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((probe.Passed, probe.ApiCode, probe.Gateway), Is.EqualTo((true, (int?)200, (HaloGatewayAccount?)new HaloGatewayAccount(true, "Healthy", null))));
            Assert.That(probe.At, Is.EqualTo(_time.GetUtcNow()));
        }
        await _client.Received(2).StatsGetMatchStats(Arg.Any<string>());
    }
}
