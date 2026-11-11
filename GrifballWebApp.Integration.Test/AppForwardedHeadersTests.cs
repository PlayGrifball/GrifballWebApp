using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using System.Net;
using static GrifballWebApp.Server.Test.AppForwardedHeadersTestData;

namespace GrifballWebApp.Integration.Test;

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

    private static async Task<(string? RemoteIp, string Scheme, string Host)> Send(Dictionary<string, string?> settings, string peer, string? clientSuppliedXff = null, HttpClient? cloudflare = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        if (cloudflare is null)
#pragma warning disable VSTHRD103 // The sync overload is under test (the async one in the else)
            builder.Services.ConfigureAppForwardedHeaders(builder.Configuration);
#pragma warning restore VSTHRD103
        else
            await builder.Services.ConfigureAppForwardedHeadersAsync(builder.Configuration, cloudflare);

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(remoteIp, Is.EqualTo(Client));
            Assert.That(scheme, Is.EqualTo("http"), "nginx sends X-Forwarded-Proto: $scheme, which is http");
            Assert.That(host, Is.EqualTo("grifball.xyz"));
        }
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
        // Without Cloudflare's ranges (FetchCloudflare off) the walk stops at the Cloudflare edge.
        var (remoteIp, _, _) = await Send(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:ForwardLimit"] = "3",
        }, NginxPod);
        Assert.That(remoteIp, Is.EqualTo(CloudflareEdge));
    }

    [Test]
    public async Task WalkingXForwardedFor_WithFetchedCloudflareRanges_RecoversTheClient()
    {
        // The documented switch away from X-Real-IP: client, Cloudflare edge, Traefik pod; nginx is the peer.
        var settings = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:ForwardedForHeaderName"] = "X-Forwarded-For",
            ["ForwardedHeaders:ForwardLimit"] = "3",
            ["ForwardedHeaders:FetchCloudflare"] = "true",
        };

        var (remoteIp, _, _) = await Send(settings, NginxPod, cloudflare: new HttpClient(Published()));
        var (spoofed, _, _) = await Send(settings, NginxPod, clientSuppliedXff: "127.0.0.1", cloudflare: new HttpClient(Published()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(remoteIp, Is.EqualTo(Client));
            Assert.That(spoofed, Is.EqualTo(Client), "an X-Forwarded-For entry the client typed sits left of the limit");
        }
    }
}
