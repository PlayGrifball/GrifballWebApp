using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Core.Storage;
using Surprenant.Grunt.Gateway.Client;
using System.Net;
using GrifballWebApp.Infrastructure.HaloInfinite;
using static GrifballWebApp.Server.Test.HaloGatewayModeUnitTests;

namespace GrifballWebApp.Integration.Test;

/// <summary><c>HaloInfinite:Mode</c>: the settings, what each mode registers, and the health check through the gateway.</summary>
[TestFixture]
public class HaloGatewayModeTests
{
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
        using (Assert.EnterMultipleScope())
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
        }
    }

    private static readonly string[] expected = new[] { "the-key" };

    [Test]
    public async Task Gateway_ClientCallsTheGateway_WithTheApiKey()
    {
        var builder = Builder((HaloInfiniteSettings.BaseUrlKey, "http://gw.test:8080/"), (HaloInfiniteSettings.ApiKeyKey, "the-key"));
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), new RetryDelayCalculator());
        var gateway = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("bad key") });
        builder.Services.AddHttpClient<HaloGatewayClient>().ConfigurePrimaryHttpMessageHandler(() => gateway);
        using var services = builder.Services.BuildServiceProvider();

        var response = await services.GetRequiredService<IHaloInfiniteClient>().StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

        using (Assert.EnterMultipleScope())
        {
            var request = gateway.Requests.Single();
            Assert.That(request.RequestUri, Is.EqualTo(new Uri("http://gw.test:8080/halo/v1/stats/matches/1fde4c2a-7935-4fb0-9706-e226f4d13683")));
            Assert.That(request.Headers.GetValues("X-Api-Key"), Is.EqualTo(expected));
            Assert.That(response.Error.Code, Is.EqualTo(401));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(halo.Requests, Has.Count.EqualTo(2));
            delays.Received(1).CalculateDelay(1, Arg.Any<HttpResponseMessage?>());
        }
    }

    [Test]
    public void Direct_RegistersTheFactoryAsTheClient()
    {
        using var services = Register(Builder((HaloInfiniteSettings.ModeKey, "Direct")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.GetRequiredService<IHaloInfiniteClientFactory>(), Is.TypeOf<HaloInfiniteClientFactory>());
            Assert.That(services.GetRequiredService<IHaloInfiniteClient>(), Is.SameAs(services.GetRequiredService<IHaloInfiniteClientFactory>()));
            Assert.That(services.GetServices<IHaloInfiniteClient>().Count(), Is.EqualTo(1));
            Assert.That(services.GetService<IAccountAuthorization>(), Is.Not.Null);
            Assert.That(services.GetService<IHaloGatewayAccountStatus>(), Is.Null);
            Assert.That(services.GetService<HaloGatewayClient>(), Is.Null);
            Assert.That(services.GetRequiredService<HaloInfiniteHealthCheck>().ViaGateway, Is.False);
            Assert.That(services.GetRequiredService<HaloInfiniteSettings>().Mode, Is.EqualTo(HaloInfiniteMode.Direct));
        }
    }
}
