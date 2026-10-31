using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Core.Storage;
using Surprenant.Grunt.Gateway.Client;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using System.Net;

namespace GrifballWebApp.Test;

/// <summary><c>HaloInfinite:Mode</c>: the settings, what each mode registers, and the health check through the gateway.</summary>
[TestFixture]
public class HaloGatewayModeTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    // Settings

    [Test]
    public void Settings_Default_GatewayInTheCluster()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config());

        Assert.Multiple(() =>
        {
            Assert.That(settings.Mode, Is.EqualTo(HaloInfiniteMode.Gateway));
            Assert.That(settings.IsGateway, Is.True);
            Assert.That(settings.GatewayBaseUrl, Is.EqualTo(new Uri("http://halo-gateway.halo-gateway.svc:8080/")));
            Assert.That(settings.GatewayApiKey, Is.Null);
            Assert.That(settings.GatewayAdminUrl, Is.EqualTo("https://grunt.grifball.xyz/admin"));
        });
    }

    [TestCase("Direct", HaloInfiniteMode.Direct)]
    [TestCase("direct", HaloInfiniteMode.Direct)]
    [TestCase(" GATEWAY ", HaloInfiniteMode.Gateway)]
    [TestCase("", HaloInfiniteMode.Gateway)]
    public void Settings_Mode_AnyCase(string value, HaloInfiniteMode expected)
    {
        Assert.That(HaloInfiniteSettings.FromConfiguration(Config((HaloInfiniteSettings.ModeKey, value))).Mode, Is.EqualTo(expected));
    }

    [TestCase("Sideways")]
    [TestCase("7")]
    public void Settings_UnknownMode_Throws(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => HaloInfiniteSettings.FromConfiguration(Config((HaloInfiniteSettings.ModeKey, value))));
        Assert.That(ex!.Message, Does.Contain("HaloInfinite:Mode").And.Contain(value));
    }

    [TestCase("http://gw.test:8080", "http://gw.test:8080/")]
    [TestCase("https://gw.test/prefix", "https://gw.test/prefix/")]
    [TestCase("https://gw.test/prefix/", "https://gw.test/prefix/")]
    public void Settings_BaseUrl_EndsWithASlash(string value, string expected)
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config((HaloInfiniteSettings.BaseUrlKey, value)));
        Assert.That(settings.GatewayBaseUrl.ToString(), Is.EqualTo(expected));
    }

    [TestCase("not a url")]
    [TestCase("ftp://gw.test/")]
    [TestCase("/relative")]
    public void Settings_BadBaseUrl_Throws(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => HaloInfiniteSettings.FromConfiguration(Config((HaloInfiniteSettings.BaseUrlKey, value))));
        Assert.That(ex!.Message, Does.Contain("HaloGateway:BaseUrl"));
    }

    [Test]
    public void Settings_ToString_NeverShowsTheKey()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config((HaloInfiniteSettings.ApiKeyKey, "secret-key"), (HaloInfiniteSettings.AdminUrlKey, "https://gw.test/admin")));

        Assert.Multiple(() =>
        {
            Assert.That(settings.GatewayApiKey, Is.EqualTo("secret-key"));
            Assert.That(settings.GatewayAdminUrl, Is.EqualTo("https://gw.test/admin"));
            Assert.That(settings.ToString(), Does.Not.Contain("secret-key").And.Contain("API key set"));
        });
    }

    // What each mode registers

    private static WebApplicationBuilder Builder(params (string Key, string? Value)[] values)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)));
        return builder;
    }

    private static ServiceProvider Register(WebApplicationBuilder builder)
    {
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), new RetryDelayCalculator());
        return builder.Services.BuildServiceProvider();
    }

    [Test]
    public void Gateway_RegistersTheGatewayClient_AndNoSignIn()
    {
        using var services = Register(Builder(
            (HaloInfiniteSettings.BaseUrlKey, "http://gw.test:8080"),
            (HaloInfiniteSettings.ApiKeyKey, "the-key"),
            ("HaloGateway:Retry:MaxAttempts", "5")));

        var options = services.GetRequiredService<IOptions<HaloGatewayClientOptions>>().Value;
        Assert.Multiple(() =>
        {
            Assert.That(services.GetRequiredService<IHaloInfiniteClient>(), Is.TypeOf<HaloGatewayClient>());
            Assert.That(options.BaseAddress, Is.EqualTo(new Uri("http://gw.test:8080/")));
            Assert.That(options.ApiKey, Is.EqualTo("the-key"));
            Assert.That(options.Retry.MaxAttempts, Is.EqualTo(5));
            Assert.That(services.GetService<IHaloInfiniteClientFactory>(), Is.Null);
            Assert.That(services.GetService<IAccountAuthorization>(), Is.Null);
            Assert.That(services.GetService<IHaloGatewayAccountStatus>(), Is.TypeOf<HaloGatewayAccountStatus>());
            Assert.That(services.GetRequiredService<IHttpClientFactory>().CreateClient(HaloInfiniteServiceExtensions.GatewayStatusClientName).BaseAddress,
                Is.EqualTo(new Uri("http://gw.test:8080/")));
            Assert.That(services.GetRequiredService<HaloInfiniteHealthCheck>().ViaGateway, Is.True);
            Assert.That(services.GetRequiredService<HaloInfiniteSettings>().Mode, Is.EqualTo(HaloInfiniteMode.Gateway));
        });
    }

    [Test]
    public async Task Gateway_ClientCallsTheGateway_WithTheApiKey()
    {
        var builder = Builder((HaloInfiniteSettings.BaseUrlKey, "http://gw.test:8080/"), (HaloInfiniteSettings.ApiKeyKey, "the-key"));
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), new RetryDelayCalculator());
        var gateway = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("bad key") });
        builder.Services.AddHttpClient<HaloGatewayClient>().ConfigurePrimaryHttpMessageHandler(() => gateway);
        using var services = builder.Services.BuildServiceProvider();

        var response = await services.GetRequiredService<IHaloInfiniteClient>().StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

        Assert.Multiple(() =>
        {
            var request = gateway.Requests.Single();
            Assert.That(request.RequestUri, Is.EqualTo(new Uri("http://gw.test:8080/halo/v1/stats/matches/1fde4c2a-7935-4fb0-9706-e226f4d13683")));
            Assert.That(request.Headers.GetValues("X-Api-Key"), Is.EqualTo(new[] { "the-key" }));
            Assert.That(response.Error.Code, Is.EqualTo(401));
        });
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task Direct_TheFactorysHttpClientRetriesHalosThrottlingAndErrors(HttpStatusCode first)
    {
        var builder = Builder((HaloInfiniteSettings.ModeKey, "Direct"));
        var delays = Substitute.For<IRetryDelayCalculator>();
        delays.CalculateDelay(Arg.Any<int>(), Arg.Any<HttpResponseMessage?>()).Returns(TimeSpan.Zero);
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), delays);
        var answers = new Queue<HttpStatusCode>([first, HttpStatusCode.OK]);
        var halo = new StubHandler(_ => new HttpResponseMessage(answers.Dequeue()));
        // The client the factory's HaloInfiniteClients are built on, with Halo stubbed underneath the retry policy.
        builder.Services.AddHttpClient(nameof(IHaloInfiniteClient)).ConfigurePrimaryHttpMessageHandler(() => halo);
        using var services = builder.Services.BuildServiceProvider();

        using var response = await services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IHaloInfiniteClient))
            .GetAsync("https://halo.test/hi/stats");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(halo.Requests, Has.Count.EqualTo(2));
            delays.Received(1).CalculateDelay(1, Arg.Any<HttpResponseMessage?>());
        });
    }

    [Test]
    public void Direct_RegistersTheFactoryAsTheClient()
    {
        using var services = Register(Builder((HaloInfiniteSettings.ModeKey, "Direct")));

        Assert.Multiple(() =>
        {
            Assert.That(services.GetRequiredService<IHaloInfiniteClientFactory>(), Is.TypeOf<HaloInfiniteClientFactory>());
            Assert.That(services.GetRequiredService<IHaloInfiniteClient>(), Is.SameAs(services.GetRequiredService<IHaloInfiniteClientFactory>()));
            Assert.That(services.GetServices<IHaloInfiniteClient>().Count(), Is.EqualTo(1));
            Assert.That(services.GetService<IAccountAuthorization>(), Is.Not.Null);
            Assert.That(services.GetService<IHaloGatewayAccountStatus>(), Is.Null);
            Assert.That(services.GetService<HaloGatewayClient>(), Is.Null);
            Assert.That(services.GetRequiredService<HaloInfiniteHealthCheck>().ViaGateway, Is.False);
            Assert.That(services.GetRequiredService<HaloInfiniteSettings>().Mode, Is.EqualTo(HaloInfiniteMode.Direct));
        });
    }

    // The gateway's account status

    [TestCase(HttpStatusCode.OK, "Healthy", true, "Healthy")]
    [TestCase(HttpStatusCode.OK, "Degraded", false, "Degraded")]
    [TestCase(HttpStatusCode.ServiceUnavailable, "Unhealthy", false, "Unhealthy")]
    public async Task AccountStatus_ReadsTheStatusWord(HttpStatusCode code, string body, bool healthy, string status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(code) { Content = new StringContent(body + "\n") });

        var account = await AccountStatus(handler, "http://gw.test:8080/prefix/").GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(account.Reachable, Is.True);
            Assert.That(account.Status, Is.EqualTo(status));
            Assert.That(account.Healthy, Is.EqualTo(healthy));
            Assert.That(handler.Requests.Single().RequestUri, Is.EqualTo(new Uri("http://gw.test:8080/prefix/health")));
        });
    }

    [Test]
    public async Task AccountStatus_OtherAnswer_ReachableWithoutStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("") });

        var account = await AccountStatus(handler).GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(account.Reachable, Is.True);
            Assert.That(account.Status, Is.Null);
            Assert.That(account.Healthy, Is.False);
            Assert.That(account.Error, Does.Contain("404"));
        });
    }

    [Test]
    public async Task AccountStatus_NoAnswer_Unreachable_NothingThrown()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        var account = await AccountStatus(handler).GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(account.Reachable, Is.False);
            Assert.That(account.Healthy, Is.False);
            Assert.That(account.Error, Is.EqualTo(nameof(HttpRequestException)));
        });
    }

    [Test]
    public async Task AccountStatus_LongAnswer_Truncated()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 500)) });

        var account = await AccountStatus(handler).GetAsync();

        Assert.That(account.Status, Has.Length.EqualTo(32));
    }

    [Test]
    public async Task AccountStatus_CallerCancels_Throws()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () => AccountStatus(handler).GetAsync(cancelled.Token));
    }

    private static HaloGatewayAccountStatus AccountStatus(StubHandler handler, string baseUrl = "http://gw.test:8080/")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HaloInfiniteServiceExtensions.GatewayStatusClientName).Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri(baseUrl) });
        return new HaloGatewayAccountStatus(factory, NullLogger<HaloGatewayAccountStatus>.Instance);
    }

    // The health check through the gateway

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static HaloApiResultContainer<MatchStats, HaloApiErrorContainer> Response(int code) =>
        new(new MatchStats(), new HaloApiErrorContainer { Code = code });

    private static (HaloInfiniteHealthCheck Check, IHaloInfiniteClient Client, IHaloGatewayAccountStatus Gateway, FakeTimeProvider Time) GatewayCheck()
    {
        var client = Substitute.For<IHaloInfiniteClient>();
        var gateway = Substitute.For<IHaloGatewayAccountStatus>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        var check = new HaloInfiniteHealthCheck(client, Interval, Timeout, time, NullLogger<HaloInfiniteHealthCheck>.Instance, gateway: gateway);
        return (check, client, gateway, time);
    }

    private static Task<HealthCheckResult> Run(HaloInfiniteHealthCheck check) => check.CheckHealthAsync(new HealthCheckContext
    {
        Registration = new HealthCheckRegistration("halo_infinite", check, HealthStatus.Degraded, null),
    });

    [Test]
    public async Task Check_GatewayHealthy_AndTestCallWorks_Healthy()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, "Healthy", null));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(200));

        var result = await Run(check);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Description, Does.Contain("Halo gateway account Healthy").And.Contain("200"));
        });
    }

    [Test]
    public async Task Check_AccountNeedsSignIn_Degraded_EvenWithACachedAnswer()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, "Degraded", null));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(200));

        var result = await Run(check);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("Degraded"));
        });
    }

    [Test]
    public async Task Check_GatewayDown_DegradedNeverUnhealthy()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(HaloGatewayAccount.Unreachable(nameof(HttpRequestException)));
        client.StatsGetMatchStats(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("connection refused"));

        var result = await Run(check);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("Halo gateway unreachable").And.Contain(nameof(HttpRequestException)));
            Assert.That(result.Exception, Is.TypeOf<HttpRequestException>());
        });
    }

    [Test]
    public async Task Check_KeyRejected_SaysSo()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, "Healthy", null));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(401));

        var result = await Run(check);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("401").And.Contain("HaloGateway:ApiKey"));
        });
    }

    [Test]
    public async Task Check_QuotaSpent_SaysSo()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, "Healthy", null));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(429));

        var result = await Run(check);

        Assert.That(result.Description, Does.Contain("429").And.Contain("quota"));
    }

    [Test]
    public async Task Check_GatewayAnswersWithoutAStatus_Degraded()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, null, "gateway /health answered 404"));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(200));

        var result = await Run(check);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("Halo gateway reachable (gateway /health answered 404)"));
        });
    }

    [Test]
    public async Task Check_GatewayStatusThrows_Unreachable_NothingEscapes()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(200));

        var result = await Run(check);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("Halo gateway unreachable (InvalidOperationException)"));
        });
    }

    [Test]
    public async Task StatusDto_WithoutAKey_SaysSo()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(HaloGatewayAccount.Unreachable("HttpRequestException"));
        client.StatsGetMatchStats(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("down"));
        var settings = HaloInfiniteSettings.FromConfiguration(Config());

        var dto = HaloStatusDto.Gateway(settings, await check.ProbeNowAsync());

        Assert.Multiple(() =>
        {
            Assert.That(dto.ApiKeyConfigured, Is.False);
            Assert.That(dto.Reachable, Is.False);
            Assert.That(dto.AccountStatus, Is.Null);
            Assert.That(dto.ApiCode, Is.Null);
            Assert.That(dto.Healthy, Is.False);
            Assert.That(dto.GatewayUrl, Is.EqualTo(HaloInfiniteSettings.DefaultGatewayBaseUrl));
            Assert.That(dto.GatewayAdminUrl, Is.EqualTo(HaloInfiniteSettings.DefaultGatewayAdminUrl));
        });
    }

    [Test]
    public async Task Check_GatewayStatusHangs_TimesOut_Degraded()
    {
        var (check, client, gateway, time) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<HaloGatewayAccount>().Task);
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(200));

        var pending = Run(check);
        time.Advance(Timeout);
        var result = await pending;

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("no answer within 10 s"));
        });
    }

    [Test]
    public async Task ProbeNow_IgnoresTheCache_AndRefreshesIt()
    {
        var (check, client, gateway, _) = GatewayCheck();
        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, "Degraded", null));
        client.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(200));
        var before = await Run(check);

        gateway.GetAsync(Arg.Any<CancellationToken>()).Returns(new HaloGatewayAccount(true, "Healthy", null));
        var probe = await check.ProbeNowAsync();
        var after = await Run(check);

        Assert.Multiple(async () =>
        {
            Assert.That(before.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(probe.Passed, Is.True);
            Assert.That(probe.ApiCode, Is.EqualTo(200));
            Assert.That(probe.Gateway!.Status, Is.EqualTo("Healthy"));
            Assert.That(after.Status, Is.EqualTo(HealthStatus.Healthy), "the health check now serves the fresh probe");
            await client.Received(2).StatsGetMatchStats(Arg.Any<string>());
        });
    }

    [Test]
    public void FromConfiguration_UsesTheGatewayStatus_WhenRegistered()
    {
        var gatewayServices = new ServiceCollection().AddLogging()
            .AddSingleton(Substitute.For<IHaloInfiniteClient>())
            .AddSingleton(Substitute.For<IHaloGatewayAccountStatus>())
            .BuildServiceProvider();
        var directServices = new ServiceCollection().AddLogging()
            .AddSingleton(Substitute.For<IHaloInfiniteClient>())
            .BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(HaloInfiniteHealthCheck.FromConfiguration(gatewayServices, Config()).ViaGateway, Is.True);
            Assert.That(HaloInfiniteHealthCheck.FromConfiguration(directServices, Config()).ViaGateway, Is.False);
        });
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
