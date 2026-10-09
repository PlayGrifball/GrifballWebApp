using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace GrifballWebApp.Test;

[TestFixture]
public class AppForwardedHeadersTests
{
    // The headers grif-backend receives in production, from the 2026-10-08 Tempo trace of a request:
    // Cloudflare -> Traefik (trusts Cloudflare, sets X-Real-Ip) -> nginx (appends Traefik) -> backend.
    private const string Client = "68.41.224.97";
    private const string CloudflareEdge = "172.70.80.242";
    private const string TraefikPod = "10.42.0.163";
    private const string NginxPod = "10.42.1.245";

    private static readonly Dictionary<string, string?> ProductionSettings = new()
    {
        ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
        ["ForwardedHeaders:ForwardedForHeaderName"] = "X-Real-IP",
    };

    private static async Task<(string? RemoteIp, string Scheme, string Host)> Send(Dictionary<string, string?> settings, string peer, string? clientSuppliedXff = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.ConfigureAppForwardedHeaders(builder.Configuration);

        await using var app = builder.Build();
        app.UseForwardedHeaders();
        app.Run(context => context.Response.WriteAsync($"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host}"));
        await app.StartAsync();

        var xff = string.Join(", ", new[] { clientSuppliedXff, Client, CloudflareEdge, TraefikPod }.Where(x => x is not null));
        var context = await app.GetTestServer().SendAsync(c =>
        {
            c.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            c.Request.Host = new HostString("grifball.xyz");
            c.Request.Headers["X-Forwarded-For"] = xff;
            c.Request.Headers["X-Real-IP"] = Client;
            c.Request.Headers["CF-Connecting-IP"] = Client;
            c.Request.Headers["X-Forwarded-Proto"] = "http"; // nginx's $scheme
            c.Request.Headers["X-Forwarded-Host"] = "grifball.xyz";
        });

        using var reader = new StreamReader(context.Response.Body);
        var parts = (await reader.ReadToEndAsync()).Split('|');
        return (parts[0], parts[1], parts[2]);
    }

    [Test]
    public async Task WithoutConfiguration_NothingIsApplied_TheCurrentProductionBehaviour()
    {
        var (remoteIp, _, _) = await Send(new(), NginxPod);
        Assert.That(remoteIp, Is.EqualTo(NginxPod), "only loopback is trusted by default, so the nginx pod is reported as the client");
    }

    [Test]
    public async Task TrustingThePodNetwork_UsesTheClientTraefikResolved()
    {
        var (remoteIp, scheme, host) = await Send(ProductionSettings, NginxPod);

        Assert.Multiple(() =>
        {
            Assert.That(remoteIp, Is.EqualTo(Client));
            Assert.That(scheme, Is.EqualTo("http"), "nginx sends X-Forwarded-Proto: $scheme, which is http");
            Assert.That(host, Is.EqualTo("grifball.xyz"));
        });
    }

    [Test]
    public async Task ClientSuppliedXForwardedFor_CannotSpoofTheAddress()
    {
        var (remoteIp, _, _) = await Send(ProductionSettings, NginxPod, clientSuppliedXff: "127.0.0.1");
        Assert.That(remoteIp, Is.EqualTo(Client));
    }

    [Test]
    public async Task UntrustedPeer_IsLeftAlone()
    {
        var (remoteIp, _, _) = await Send(ProductionSettings, "192.0.2.9");
        Assert.That(remoteIp, Is.EqualTo("192.0.2.9"));
    }

    [Test]
    public async Task XForwardedFor_WithoutCloudflareRanges_StopsAtTheCloudflareEdge()
    {
        // Why X-Real-IP: walking X-Forwarded-For would need every Cloudflare range trusted here as well.
        var (remoteIp, _, _) = await Send(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:ForwardLimit"] = "3",
        }, NginxPod);
        Assert.That(remoteIp, Is.EqualTo(CloudflareEdge));
    }

    [Test]
    public void Parse_SkipsInvalidEntries_AndReportsThem()
    {
        var (networks, proxies, invalid) = AppForwardedHeaders.Parse(new ForwardedHeadersSettings
        {
            KnownIPNetworks = ["10.42.0.0/16", "not-a-cidr", " ", "fd00::/8"],
            KnownProxies = ["10.43.0.10", "nope"],
        });

        Assert.Multiple(() =>
        {
            Assert.That(networks.Select(n => n.ToString()), Is.EqualTo(new[] { "10.42.0.0/16", "fd00::/8" }));
            Assert.That(proxies.Select(p => p.ToString()), Is.EqualTo(new[] { "10.43.0.10" }));
            Assert.That(invalid, Is.EqualTo(new[] { "not-a-cidr", "nope" }));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.EqualTo(new[] { "bogus" }));
            Assert.That(options.ForwardedHeaders, Is.EqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost));
            Assert.That(options.KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(new[] { "10.42.0.0/16" }));
            Assert.That(options.KnownProxies, Is.EqualTo(new[] { IPAddress.Parse("10.43.0.10") }));
            Assert.That(options.ForwardedForHeaderName, Is.EqualTo("X-Real-IP"));
            Assert.That(options.ForwardLimit, Is.EqualTo(2));
        });
    }

    [Test]
    public void ConfigureAppForwardedHeaders_NothingConfigured_KeepsLoopbackDefaults()
    {
        var services = new ServiceCollection();
        var invalid = services.ConfigureAppForwardedHeaders(new ConfigurationBuilder().Build());
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.Empty);
            Assert.That(options.KnownProxies, Does.Contain(IPAddress.IPv6Loopback));
            Assert.That(options.ForwardedForHeaderName, Is.EqualTo(ForwardedHeadersDefaults.XForwardedForHeaderName));
            Assert.That(options.ForwardLimit, Is.EqualTo(1));
        });
    }
}
