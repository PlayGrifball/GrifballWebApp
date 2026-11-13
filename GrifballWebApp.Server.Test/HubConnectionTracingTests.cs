using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Http;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A hub connection's own requests last as long as it does (hours), which no span should: they aren't traced; its
/// negotiate and the sends of the transports that aren't WebSockets are.
/// </summary>
[TestFixture]
public class HubConnectionTracingTests
{
    [TestCase("GET", "/TeamsHub", true)] // the WebSocket, the server-sent events, or a long poll's wait
    [TestCase("GET", "/QueueHub", true)]
    [TestCase("GET", "/teamshub", true)]
    [TestCase("POST", "/TeamsHub/negotiate", false)]
    [TestCase("POST", "/TeamsHub", false)] // a send over long polling or server-sent events
    [TestCase("DELETE", "/TeamsHub", false)] // long polling's close
    [TestCase("GET", "/TeamsHub/negotiate", false)]
    [TestCase("GET", "/Teams", false)]
    [TestCase("GET", "/TeamsHubs", false)]
    public void IsHubConnection(string method, string path, bool expected)
        => Assert.That(HubConnectionTracing.IsHubConnection(method, path), Is.EqualTo(expected));

    [Test]
    public void IsTraced_NotAHubConnectionNorAHealthProbeNorTheBrowsersBatches()
    {
        static HttpRequest Request(string method, string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            return context.Request;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MetricsAndTracingExtensions.IsTraced(Request("GET", "/TeamsHub"), HealthCheckPaths.Default), Is.False);
            Assert.That(MetricsAndTracingExtensions.IsTraced(Request("POST", "/TeamsHub/negotiate"), HealthCheckPaths.Default), Is.True);
            Assert.That(MetricsAndTracingExtensions.IsTraced(Request("GET", "/health/live"), HealthCheckPaths.Default), Is.False);
            Assert.That(MetricsAndTracingExtensions.IsTraced(Request("POST", "/client-telemetry"), HealthCheckPaths.Default), Is.False);
            Assert.That(MetricsAndTracingExtensions.IsTraced(Request("GET", "/Season/Get"), HealthCheckPaths.Default), Is.True);
        }
    }

    [Test]
    public void TheHubsPaths_AreTheMappedHubs()
    {
        Assert.That(HubConnectionTracing.HubPaths, Is.EquivalentTo(MappedHubs));
    }

    private static readonly string[] MappedHubs = ["/" + HubRoutes.Teams, "/" + HubRoutes.Queue];
}
