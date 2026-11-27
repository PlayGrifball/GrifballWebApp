using System.Net;
using System.Text;
using GrifballWebApp.Infrastructure.Spartan;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using PlayGrifball.InfiniteSpartanRender.Client;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The render service health check: Healthy when the catalog answers, Degraded (as registered) when it does not, cached.</summary>
[TestFixture]
public class SpartanRenderHealthCheckTests
{
    private const string Json = """{"cores":[{"name":"Mark VII","regions":[]}],"coatings":[],"visors":[],"pendingCores":[]}""";

    private sealed class Handler(Func<HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(reply());
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent(Json, Encoding.UTF8, "application/json") };

    private static SpartanRenderHealthCheck Check(Handler handler) => new(
        new InfiniteSpartanRenderClient(new HttpClient(handler) { BaseAddress = new Uri("http://render.test/") }),
        new MemoryCache(new MemoryCacheOptions()), NullLogger<SpartanRenderHealthCheck>.Instance);

    private static Task<HealthCheckResult> Run(SpartanRenderHealthCheck check) => check.CheckHealthAsync(new HealthCheckContext
    {
        Registration = new HealthCheckRegistration("spartan_render", check, HealthStatus.Degraded, null),
    });

    [Test]
    public async Task TheCatalogAnswers_Healthy()
    {
        var result = await Run(Check(new Handler(Ok)));

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task TheServiceAnswersAnError_Degraded()
    {
        var result = await Run(Check(new Handler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
    }

    [Test]
    public async Task TheServiceCannotBeReached_Degraded()
    {
        var result = await Run(Check(new Handler(() => throw new HttpRequestException("Connection refused"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("HttpRequestException"));
        }
    }

    [Test]
    public async Task ARepeatedCheck_IsAnsweredFromTheCache_NotTheService()
    {
        var handler = new Handler(Ok);
        var check = Check(handler);

        await Run(check);
        await Run(check);

        Assert.That(handler.Calls, Is.EqualTo(1));
    }
}
