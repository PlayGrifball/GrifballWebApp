using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using static GrifballWebApp.Server.Test.AppForwardedHeadersTestData;

namespace GrifballWebApp.Server.Test;

/// <summary>AppForwardedHeadersTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class AppForwardedHeadersUnitTests
{
    private static readonly string[] ExpectedNetworks = ["10.42.0.0/16", "fd00::/8"];
    private static readonly string[] ExpectedProxies = ["10.43.0.10"];
    private static readonly string[] ExpectedInvalid = ["not-a-cidr", "nope"];
    private static readonly string[] ExpectedBogus = ["bogus"];
    private static readonly string[] PodNetwork = ["10.42.0.0/16"];
    private static readonly string[] ExpectedFetched = ["173.245.48.0/20", "172.64.0.0/13", "2400:cb00::/32"];
    private static readonly string[] ExpectedTrustedNetworks = ["10.42.0.0/16", "173.245.48.0/20", "172.64.0.0/13", "2400:cb00::/32"];
    private static readonly string[] ExpectedRequested = ["https://mirror.example/v4", "https://mirror.example/v6"];
    private static readonly string[] ExpectedErrors = ["https://cf.example/v4: empty response"];
    private static readonly string[] ExpectedFetchedNetworks = ["173.245.48.0/20", "172.64.0.0/13"];
    private static readonly string[] ExpectedInvalidEntry = ["https://cf.example/v4: invalid entry '<html>oops</html>'"];

    [Test]
    public void Parse_SkipsInvalidEntries_AndReportsThem()
    {
        var (networks, proxies, invalid) = AppForwardedHeaders.Parse(new ForwardedHeadersSettings
        {
            KnownIPNetworks = ["10.42.0.0/16", "not-a-cidr", " ", "fd00::/8"],
            KnownProxies = ["10.43.0.10", "nope"],
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(networks.Select(n => n.ToString()), Is.EqualTo(ExpectedNetworks));
            Assert.That(proxies.Select(p => p.ToString()), Is.EqualTo(ExpectedProxies));
            Assert.That(invalid, Is.EqualTo(ExpectedInvalid));
        }
    }

    [Test]
    public void ConfigureAppForwardedHeaders_AppliesSettings_AndReturnsInvalidEntries()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:KnownIPNetworks:1"] = "bogus",
            ["ForwardedHeaders:KnownProxies:0"] = "10.43.0.10",
            ["ForwardedHeaders:ForwardedForHeaderName"] = " X-Real-IP ",
            ["ForwardedHeaders:ForwardLimit"] = "2",
        }).Build();
        var services = new ServiceCollection();

        var invalid = services.ConfigureAppForwardedHeaders(configuration);
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(invalid, Is.EqualTo(ExpectedBogus));
            Assert.That(options.ForwardedHeaders, Is.EqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost));
            Assert.That(options.KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(PodNetwork));
            Assert.That(options.KnownProxies, Is.EqualTo(new[] { IPAddress.Parse("10.43.0.10") }));
            Assert.That(options.ForwardedForHeaderName, Is.EqualTo("X-Real-IP"));
            Assert.That(options.ForwardLimit, Is.EqualTo(2));
        }
    }

    [Test]
    public void ConfigureAppForwardedHeaders_NothingConfigured_KeepsLoopbackDefaults()
    {
        var services = new ServiceCollection();
        var invalid = services.ConfigureAppForwardedHeaders(new ConfigurationBuilder().Build());
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(invalid, Is.Empty);
            Assert.That(options.KnownProxies, Does.Contain(IPAddress.IPv6Loopback));
            Assert.That(options.ForwardedForHeaderName, Is.EqualTo(ForwardedHeadersDefaults.XForwardedForHeaderName));
            Assert.That(options.ForwardLimit, Is.EqualTo(1));
        }
    }

    // ---- Cloudflare ranges fetched at startup (ForwardedHeaders:FetchCloudflare) ----

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ForwardedHeadersOptions OptionsOf(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

    [Test]
    public async Task FetchCloudflare_Off_ByDefault_MakesNoRequest()
    {
        var handler = Published();
        var services = new ServiceCollection();

        var setup = await services.ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
        }), new HttpClient(handler));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Requested, Is.Empty);
            Assert.That(setup.Fetched, Is.Empty);
            Assert.That(setup.FetchErrors, Is.Empty);
            Assert.That(OptionsOf(services).KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(PodNetwork));
        }
    }

    [Test]
    public async Task FetchCloudflare_On_TrustsTheConfiguredAndPublishedRanges()
    {
        var handler = Published();
        var services = new ServiceCollection();

        var setup = await services.ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:KnownIPNetworks:1"] = "173.245.48.0/20", // also published: trusted once
            ["ForwardedHeaders:FetchCloudflare"] = "true",
        }), new HttpClient(handler));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Requested, Is.EquivalentTo(new[] { ForwardedHeadersSettings.DefaultCloudflareIpsV4Url, ForwardedHeadersSettings.DefaultCloudflareIpsV6Url }));
            Assert.That(setup.Fetched.Select(n => n.ToString()), Is.EquivalentTo(ExpectedFetched));
            Assert.That(setup.FetchErrors, Is.Empty);
            Assert.That(OptionsOf(services).KnownIPNetworks.Select(n => n.ToString()),
                Is.EquivalentTo(ExpectedTrustedNetworks));
        }
    }

    [Test]
    public async Task FetchCloudflare_UsesTheConfiguredUrls()
    {
        var handler = new FakeCloudflare(new()
        {
            ["https://mirror.example/v4"] = (HttpStatusCode.OK, CloudflareV4),
            ["https://mirror.example/v6"] = (HttpStatusCode.OK, CloudflareV6),
        });

        var setup = await new ServiceCollection().ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:FetchCloudflare"] = "true",
            ["ForwardedHeaders:CloudflareIpsV4Url"] = "https://mirror.example/v4",
            ["ForwardedHeaders:CloudflareIpsV6Url"] = "https://mirror.example/v6",
        }), new HttpClient(handler));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Requested, Is.EquivalentTo(ExpectedRequested));
            Assert.That(setup.Fetched, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task FetchCloudflare_Failures_TrustNothingExtra_AndAreReported()
    {
        var handler = new FakeCloudflare(new()
        {
            [ForwardedHeadersSettings.DefaultCloudflareIpsV4Url] = (HttpStatusCode.ServiceUnavailable, "down"),
            [ForwardedHeadersSettings.DefaultCloudflareIpsV6Url] = null, // throws
        });
        var services = new ServiceCollection();

        var setup = await services.ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:FetchCloudflare"] = "true",
        }), new HttpClient(handler));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(setup.Fetched, Is.Empty);
            Assert.That(setup.FetchErrors, Has.Count.EqualTo(2));
            Assert.That(setup.FetchErrors, Has.Some.Contains("HTTP 503"));
            Assert.That(setup.FetchErrors, Has.Some.Contains("HttpRequestException"));
            Assert.That(OptionsOf(services).KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(PodNetwork));
        }
    }

    [Test]
    public async Task FetchRanges_EmptyBody_IsAnError()
    {
        var handler = new FakeCloudflare(new() { ["https://cf.example/v4"] = (HttpStatusCode.OK, " \n \n") });

        var (networks, errors) = await AppForwardedHeaders.FetchRanges(new HttpClient(handler), "https://cf.example/v4");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(networks, Is.Empty);
            Assert.That(errors, Is.EqualTo(ExpectedErrors));
        }
    }

    [Test]
    public async Task FetchRanges_GarbageLine_IsSkipped_TheRestKept()
    {
        var handler = new FakeCloudflare(new() { ["https://cf.example/v4"] = (HttpStatusCode.OK, "173.245.48.0/20\r\n<html>oops</html>\n172.64.0.0/13") });

        var (networks, errors) = await AppForwardedHeaders.FetchRanges(new HttpClient(handler), "https://cf.example/v4");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(networks.Select(n => n.ToString()), Is.EqualTo(ExpectedFetchedNetworks));
            Assert.That(errors, Is.EqualTo(ExpectedInvalidEntry));
        }
    }

    [Test]
    public void ForwardedHeadersSetup_LogTo_ReportsEverything()
    {
        var sink = new CollectingSink();
        using var logger = new Serilog.LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        new ForwardedHeadersSetup(["bogus"], [System.Net.IPNetwork.Parse("172.64.0.0/13")], ["https://cf.example/v4: HTTP 503"]).LogTo(logger);

        Assert.That(sink.Events.Select(e => (e.Level, e.RenderMessage())), Is.EqualTo(new[]
        {
            (Serilog.Events.LogEventLevel.Warning, "Ignoring invalid ForwardedHeaders entry \"bogus\""),
            (Serilog.Events.LogEventLevel.Warning, "Cloudflare range fetch: \"https://cf.example/v4: HTTP 503\""),
            (Serilog.Events.LogEventLevel.Information, "Trusting Cloudflare network \"172.64.0.0/13\""),
        }));
    }

    private sealed class CollectingSink : Serilog.Core.ILogEventSink
    {
        public List<Serilog.Events.LogEvent> Events { get; } = [];
        public void Emit(Serilog.Events.LogEvent logEvent) => Events.Add(logEvent);
    }
}

/// <summary>AppForwardedHeadersTests' helpers, shared with its integration tests.</summary>
internal static class AppForwardedHeadersTestData
{
    internal const string CloudflareV4 = "173.245.48.0/20\n172.64.0.0/13\n";

    internal const string CloudflareV6 = "2400:cb00::/32\n";

    /// <summary>Answers per URL; counts requests. A null entry throws, as a failed connection would.</summary>
    internal sealed class FakeCloudflare(Dictionary<string, (HttpStatusCode Status, string Body)?> responses) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            if (!responses.TryGetValue(url, out var response) || response is null)
                throw new HttpRequestException($"connection refused: {url}");
            return Task.FromResult(new HttpResponseMessage(response.Value.Status) { Content = new StringContent(response.Value.Body) });
        }
    }

    internal static FakeCloudflare Published() => new(new()
    {
        [ForwardedHeadersSettings.DefaultCloudflareIpsV4Url] = (HttpStatusCode.OK, CloudflareV4),
        [ForwardedHeadersSettings.DefaultCloudflareIpsV6Url] = (HttpStatusCode.OK, CloudflareV6),
    });
}
