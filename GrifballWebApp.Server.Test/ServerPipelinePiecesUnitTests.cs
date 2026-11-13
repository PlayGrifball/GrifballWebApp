using GrifballWebApp.Server.Telemetry;
using GrifballWebApp.Server.Telemetry.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Pieces Program.cs wires in, each on its own: the API's Location prefix as the response starts, the telemetry
/// query's options and HTTP client, and the health endpoints' paths. An empty WebApplication where routing is needed.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ServerPipelinePiecesUnitTests
{
    /// <summary>A response that runs its OnStarting callbacks when told to, as the server does before the headers go.</summary>
    private sealed class StartingResponse : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _starting)
                await callback(state);
        }
    }

    [TestCase(StatusCodes.Status201Created, "/seasons/12/settings", "/api/seasons/12/settings")]
    [TestCase(StatusCodes.Status302Found, "/signin-discord", "/signin-discord")]
    public async Task UsePublicPrefix_PrefixesTheLocation_WhenTheResponseStarts(int status, string location, string expected)
    {
        var response = new StartingResponse();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Host = new HostString("grifball.example");
        var reached = false;

        await ApiLocations.UsePublicPrefix(context, c =>
        {
            reached = true;
            c.Response.StatusCode = status;
            c.Response.Headers.Location = location;
            return Task.CompletedTask;
        });
        var before = context.Response.Headers.Location.ToString();
        await response.StartAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reached, Is.True);
            Assert.That(before, Is.EqualTo(location), "Not before the response starts: the action may still change it");
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(expected));
        }
    }

    [Test]
    public void AddTelemetryQuery_BindsItsSection_NamesTheService_AndGivesItsClientALongerTimeoutAndBuffer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TelemetryQuery:LokiUrl"] = "http://loki:3100/",
            ["TelemetryQuery:TempoUrl"] = "http://tempo:3200/",
        }).Build();
        using var services = new ServiceCollection().AddTelemetryQuery(configuration, "grif-test").BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<TelemetryQueryOptions>>().Value;
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(TelemetryQueryService));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((options.ServiceName, options.Loki, options.Tempo, options.Prometheus),
                Is.EqualTo(("grif-test", (Uri?)new Uri("http://loki:3100/"), (Uri?)new Uri("http://tempo:3200/"), (Uri?)null)));
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(client.MaxResponseContentBufferSize, Is.EqualTo(32 * 1024 * 1024));
            Assert.That(services.GetRequiredService<TimeProvider>(), Is.SameAs(TimeProvider.System));
        }
    }

    [TestCase(null, "/health", "/health/live", "/health/ready")]
    [TestCase("probes", "/probes", "/probes/alive", "/probes/serving")]
    public void MapAppHealthChecks_MapsTheConfiguredPaths(string? health, string expectedHealth, string expectedLive, string expectedReady)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        if (health is not null)
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthChecks:Path"] = health,
                ["HealthChecks:LivePath"] = "probes/alive",
                ["HealthChecks:ReadyPath"] = "/probes/serving/",
            });
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        using var app = builder.Build();

        app.MapAppHealthChecks();

        var paths = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'));
        Assert.That(paths, Is.EquivalentTo(new[] { expectedHealth, expectedLive, expectedReady }));
    }
}
