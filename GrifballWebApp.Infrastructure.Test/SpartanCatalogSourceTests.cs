using System.Net;
using System.Text;
using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Infrastructure.Spartan;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlayGrifball.InfiniteSpartanRender.Client;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The render service's catalog: read once per few minutes, mapped to the app's own types, null when the service is down.</summary>
[TestFixture]
public class SpartanCatalogSourceTests
{
    private const string Json = """
        {"cores":[{"name":"Mark VII","regions":[{"kind":"REGION","key":"helmet","region":100,"pieces":[{"permutation":1,"name":"Mark VII"},{"permutation":2,"name":null}]}]}],
         "coatings":["111"],"visors":["Plain"],"pendingCores":["Chimera"]}
        """;

    private sealed class Handler(Func<HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(reply());
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(Json, Encoding.UTF8, "application/json"),
    };

    private static SpartanCatalogSource Source(Handler handler, IMemoryCache? cache = null) => new(
        new InfiniteSpartanRenderClient(new HttpClient(handler) { BaseAddress = new Uri("http://render.test/") }),
        cache ?? new MemoryCache(new MemoryCacheOptions()), NullLogger<SpartanCatalogSource>.Instance);

    [Test]
    public async Task TheCatalog_IsMappedToTheAppsTypes()
    {
        var catalog = await Source(new Handler(Ok)).Get();

        Assert.That(catalog, Is.Not.Null);
        var region = catalog!.Cores.Single().Regions.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(region.Key, Is.EqualTo("helmet"));
            Assert.That(region.Region, Is.EqualTo(100));
            Assert.That(region.Pieces.Select(p => p.Name), Is.EqualTo(new List<string?> { "Mark VII", null }));
            Assert.That(catalog.Coatings, Is.EqualTo(new List<string> { "111" }));
            Assert.That(catalog.PendingCores, Is.EqualTo(new List<string> { "Chimera" }));
        }
    }

    [Test]
    public async Task TheCatalog_IsAskedForOnce_WhileItIsKept()
    {
        var handler = new Handler(Ok);
        var source = Source(handler);

        await source.Get();
        await source.Get();

        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task AServiceThatAnswersWithAnError_GivesNull_AndIsAskedAgainNextTime()
    {
        var handler = new Handler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var source = Source(handler);

        var first = await source.Get();
        var second = await source.Get();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Null);
            Assert.That(second, Is.Null);
        }
        Assert.That(handler.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task AServiceThatCannotBeReached_GivesNull()
    {
        var source = Source(new Handler(() => throw new HttpRequestException("down")));

        Assert.That(await source.Get(), Is.Null);
    }

    [Test]
    public async Task ACancelledAsk_StaysCancelled_InsteadOfLookingLikeADownService()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var source = Source(new Handler(() => throw new TaskCanceledException()));

        await Assert.ThrowsAsync<TaskCanceledException>(() => source.Get(cts.Token));
    }

    [TestCase(null, SpartanRenderServiceExtensions.DefaultBaseAddress)]
    [TestCase("  http://other.test:9/  ", "http://other.test:9/")]
    public async Task TheBaseAddress_IsTheSettingOrTheInClusterService(string? setting, string expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            setting is null ? [] : new Dictionary<string, string?> { [SpartanRenderServiceExtensions.BaseAddressKey] = setting }).Build();
        var seen = new List<Uri?>();
        var services = new ServiceCollection().AddLogging().AddSpartanRender(config);
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => new Recorder(seen)));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<ISpartanCatalogSource>(), Is.TypeOf<SpartanCatalogSource>());

        await scope.ServiceProvider.GetRequiredService<ISpartanCatalogSource>().Get();

        Assert.That(seen.Single()!.GetLeftPart(UriPartial.Authority) + "/", Is.EqualTo(new Uri(expected).GetLeftPart(UriPartial.Authority) + "/"));
    }

    private sealed class Recorder(List<Uri?> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            seen.Add(request.RequestUri);
            return Task.FromResult(Ok());
        }
    }
}
