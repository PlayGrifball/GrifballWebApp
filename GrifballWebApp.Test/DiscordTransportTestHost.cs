using System.Text.Json;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Test;

/// <summary>
/// The real server (<see cref="ServerProgram"/>, normal mode) with only Discord's transport faked: NetCord's
/// <see cref="RestClient"/> sends through a <see cref="FakeRestRequestHandler"/>, and nothing connects to the gateway.
/// <see cref="SendAsync"/> hands an interaction to NetCord's own interaction handlers, as <see cref="GatewayClient"/> does on
/// INTERACTION_CREATE, so it runs through the app's NetCord registration (modules, <see cref="Server.DiscordInteractionPipeline"/>,
/// DI scopes) and the requests Discord would have received come back.
/// </summary>
/// <remarks>
/// One host (and database) per fixture, since startup applies the migrations. Program.Main swaps the static Serilog logger
/// (see <see cref="ProgramStartupTests"/>), so fixtures using this must not run in parallel with other startups.
/// </remarks>
internal sealed class DiscordTransportTestHost : IAsyncDisposable
{
    // Shaped like a bot token (base64 snowflake . timestamp . hmac) so NetCord can parse it; never sent anywhere.
    private const string DummyBotToken = "MTIzNDU2Nzg5MDEyMzQ1Njc4.GAAAAA.dummy-token-that-is-never-sent-anywhere";

    public const string BaseUrl = "https://grif.test";

    private readonly FakeRestRequestHandler _rest = new();
    private readonly string _connectionString;
    private readonly Factory _factory;
    private IInteractionCreateGatewayHandler[] _handlers = [];

    private DiscordTransportTestHost()
    {
        // Program.cs creates it (CreateDatabase).
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"DiscordTransport_{Guid.NewGuid():N}");
        _factory = new Factory(this);
    }

    public static DiscordTransportTestHost Start()
    {
        var host = new DiscordTransportTestHost();
        // Builds and starts the server, which runs Program.cs's app.AddModules
        var services = host._factory.Services;
        host._handlers = host._factory.InteractionHandlerTypes
            .Select(t => (IInteractionCreateGatewayHandler)services.GetRequiredService(t))
            .ToArray();
        return host;
    }

    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Delivers <paramref name="json"/> like the gateway would and returns the Discord requests it caused, in order.
    /// Everything the interaction does is awaited (temporary responses are deleted at once in tests, see <see cref="SetUpFixture"/>).
    /// </summary>
    public async Task<IReadOnlyList<DiscordRequest>> SendAsync(JsonInteraction json)
    {
        _rest.Clear();
        var gateway = Services.GetRequiredService<GatewayClient>();
        // What GatewayClient does with an INTERACTION_CREATE: responses go out through its RestClient
        var interaction = Interaction.CreateFromJson(json, gateway.Cache, gateway.Rest);
        foreach (var handler in _handlers)
            await handler.HandleAsync(interaction);
        return _rest.Requests.Select(DiscordRequest.From).ToList();
    }

    /// <summary>Runs <paramref name="action"/> with a context from a fresh scope, to seed or read the database.</summary>
    public async Task<T> WithContextAsync<T>(Func<GrifballContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<GrifballContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    private sealed class Factory(DiscordTransportTestHost host) : WebApplicationFactory<ServerProgram>
    {
        /// <summary>The gateway's interaction handlers NetCord registered for Program.cs: commands, autocomplete and each component type.</summary>
        public List<Type> InteractionHandlerTypes { get; } = [];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:Provider"] = TestDatabase.Provider.ToString(),
                ["ConnectionStrings:GrifballWebApp"] = host._connectionString,
                ["ApplyMigrations"] = "true",
                ["CreateDatabase"] = "true",
                ["BaseUrl"] = BaseUrl,
                ["Discord:ClientId"] = "123456789012345678",
                ["Discord:ClientSecret"] = "dummy-client-secret",
                ["Discord:Token"] = DummyBotToken,
                ["Discord:DraftChannel"] = "1",
                ["Discord:QueueChannel"] = "2",
                // Only the team notification handlers read it; none of the interactions here should post to the draft channel
                ["Discord:DisableGlobally"] = "true",
            };
            // UseSetting values reach WebApplication.CreateBuilder as command-line args, so Program.cs sees them
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);

            builder.ConfigureTestServices(services =>
            {
                InteractionHandlerTypes.AddRange(services
                    .Where(d => typeof(IInteractionCreateGatewayHandler).IsAssignableFrom(d.ServiceType))
                    .Select(d => d.ServiceType)
                    .Distinct());

                // Nothing connects to Discord (gateway, command registration) and the app's own loops stay off
                foreach (var descriptor in services.Where(IsDiscordOrBackgroundService).ToList())
                    services.Remove(descriptor);

                // Program.cs's RestClient (the gateway's, behind IDiscordRestClient too) sends to the fake
                services.PostConfigure<GatewayClientOptions>(options =>
                    options.RestClientConfiguration = new RestClientConfiguration { RequestHandler = host._rest });
            });
        }

        private static bool IsDiscordOrBackgroundService(ServiceDescriptor descriptor)
        {
            if (descriptor.ServiceType != typeof(IHostedService))
                return false;
            var type = descriptor.ImplementationType
                       ?? descriptor.ImplementationInstance?.GetType()
                       ?? descriptor.ImplementationFactory?.Method.DeclaringType;
            return type?.Assembly.GetName().Name?.StartsWith("NetCord") is true
                   || type == typeof(QueueBackgroundService)
                   || type == typeof(EventsBackgroundService);
        }
    }
}

/// <summary>A request the app sent to Discord: <see cref="Route"/> is the path after /api/v10, <see cref="Body"/> the JSON payload.</summary>
internal sealed record DiscordRequest(HttpMethod Method, string Route, JsonElement Body)
{
    // Discord interaction callback types
    public const int ChannelMessage = 4;
    public const int DeferredChannelMessage = 5;
    public const int AutocompleteResult = 8;
    public const int Modal = 9;

    public static DiscordRequest From(FakeRestRequestHandler.RecordedRequest request)
    {
        var path = request.Path;
        var version = path.IndexOf("/v", 1, StringComparison.Ordinal);
        var route = path.StartsWith("/api/", StringComparison.Ordinal) && version > 0 ? path[path.IndexOf('/', version + 1)..] : path;
        return new(request.Method, route, request.Json.RootElement.Clone());
    }

    /// <summary>Callback type of an interaction response (4 message, 5 deferred, 8 autocomplete, 9 modal).</summary>
    public int CallbackType => Body.GetProperty("type").GetInt32();

    /// <summary>The callback's data, or the body itself for message edits and follow-ups.</summary>
    public JsonElement Data => Body.TryGetProperty("data", out var data) ? data : Body;

    public string? Content => Data.TryGetProperty("content", out var content) ? content.GetString() : null;

    /// <summary>Message flags, 0 when not sent.</summary>
    public MessageFlags Flags => Data.TryGetProperty("flags", out var flags) ? (MessageFlags)flags.GetInt32() : 0;

    public override string ToString() => $"{Method} {Route} {Body}";
}
