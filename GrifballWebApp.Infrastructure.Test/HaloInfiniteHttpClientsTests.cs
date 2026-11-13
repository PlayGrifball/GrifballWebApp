using System.Net;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The HttpClients AddHaloInfinite configures: the gateway's /health client (its address and time limit), and, calling
/// Halo directly, the retries of Halo's 429s and transient errors with the delays IRetryDelayCalculator gives. A fake
/// primary handler answers in place of Halo, so no network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloInfiniteHttpClientsTests
{
    private static WebApplicationBuilder Builder(params (string Key, string? Value)[] values)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.Services.AddLogging();
        builder.Configuration.AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)));
        return builder;
    }

    /// <summary>Answers with each status in turn (the last one again after), counting the requests.</summary>
    private sealed class Answers(params HttpStatusCode[] codes) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(codes[Math.Min(Requests++, codes.Length - 1)]));
    }

    [Test]
    public void Gateway_TheHealthClient_GoesToTheGateway_WithItsTimeLimit()
    {
        var builder = Builder((HaloInfiniteSettings.ModeKey, "Gateway"), (HaloInfiniteSettings.BaseUrlKey, "http://gw.test:8080"));
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), new RetryDelayCalculator());
        using var services = builder.Services.BuildServiceProvider();

        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(HaloInfiniteServiceExtensions.GatewayStatusClientName);

        Assert.That((client.BaseAddress, client.Timeout), Is.EqualTo((new Uri("http://gw.test:8080/"), HaloInfiniteServiceExtensions.GatewayStatusTimeout)));
    }

    private static (ServiceProvider Services, Answers Halo, IRetryDelayCalculator Delays) Direct(params HttpStatusCode[] codes)
    {
        var builder = Builder((HaloInfiniteSettings.ModeKey, "Direct"));
        var delays = Substitute.For<IRetryDelayCalculator>();
        delays.CalculateDelay(Arg.Any<int>(), Arg.Any<HttpResponseMessage?>()).Returns(TimeSpan.Zero);
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), delays);
        var halo = new Answers(codes);
        // In place of Halo: the client's own handlers (the retries) stay in front of it
        builder.Services.AddHttpClient(nameof(Surprenant.Grunt.Core.IHaloInfiniteClient)).ConfigurePrimaryHttpMessageHandler(() => halo);
        return (builder.Services.BuildServiceProvider(), halo, delays);
    }

    [Test]
    public async Task Direct_Halos429sAndServerErrors_AreRetried_WithTheCalculatorsDelays()
    {
        var (services, halo, delays) = Direct(HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        await using var _ = services;
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(Surprenant.Grunt.Core.IHaloInfiniteClient));

        using var response = await client.GetAsync(new Uri("https://halostats.svc.halowaypoint.com/x"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(halo.Requests, Is.EqualTo(3));
        }
        delays.Received(1).CalculateDelay(1, Arg.Is<HttpResponseMessage?>(r => r != null && r.StatusCode == HttpStatusCode.TooManyRequests));
        delays.Received(1).CalculateDelay(2, Arg.Is<HttpResponseMessage?>(r => r != null && r.StatusCode == HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Direct_AClientError_IsNotRetried()
    {
        var (services, halo, delays) = Direct(HttpStatusCode.NotFound);
        await using var _ = services;
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(Surprenant.Grunt.Core.IHaloInfiniteClient));

        using var response = await client.GetAsync(new Uri("https://halostats.svc.halowaypoint.com/x"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(halo.Requests, Is.EqualTo(1));
        }
        delays.DidNotReceiveWithAnyArgs().CalculateDelay(default, default);
    }

    [Test]
    public async Task Direct_GivesUpAfterFiveRetries()
    {
        var (services, halo, _) = Direct(HttpStatusCode.TooManyRequests);
        await using var provider = services;
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(Surprenant.Grunt.Core.IHaloInfiniteClient));

        using var response = await client.GetAsync(new Uri("https://halostats.svc.halowaypoint.com/x"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(halo.Requests, Is.EqualTo(6), "the first try and five more");
        }
    }
}
