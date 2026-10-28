using DiscordInterfaces;
using System.Text.Json;
using GrifballWebApp.Database;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using NSubstitute;
using Role = GrifballWebApp.Database.Models.Role;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ModalInteractionsTests
{
    private GrifballContext _context;
    private IServiceScope _scope;
    private ISetGamertagService _setGamertagService;
    private RestClient _rest;
    private FakeRestRequestHandler _handler;
    private readonly List<InteractionCallbackProperties> _callbacks = [];

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _rest = FakeRestRequestHandler.CreateClient(out _handler);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _context);
        services.AddIdentity<User, Role>()
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<GrifballContext>();
        _scope = services.BuildServiceProvider().CreateScope();

        _setGamertagService = Substitute.For<ISetGamertagService>();
    }

    [TearDown]
    public async Task TearDown()
    {
        var connectionString = _context.Database.GetConnectionString()!;
        _scope.Dispose(); // also disposes _context
        await TestDatabase.DropDatabase(connectionString);
    }

    private (DiscordSetGamertag Handler, IDiscordModalInteractionContext Context) CreateHandler(string gamertag)
    {
        // The text input arrives wrapped in a Label (type 18), as the Set Gamertag modal now sends it
        var json = $$$"""
        {"id":"1","application_id":"2","type":5,"data":{"custom_id":"{{{DiscordModalsContants.SetGamertag}}}",
           "components":[{"type":18,"id":1,"component":{"type":4,"id":2,"custom_id":"gamertag","value":"{{{gamertag}}}"}}]},
         "channel_id":"3","channel":{"id":"3","type":0},
         "user":{"id":"1234567890","username":"discorduser","discriminator":"0"},"token":"tok","version":1,
         "locale":"en-US","entitlements":[],"authorizing_integration_owners":{},"context":1}
        """;
        var model = JsonSerializer.Deserialize<JsonInteraction>(json,
            new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString })!;
        var interaction = new ModalInteraction(model, null!, (_, cb, _, _, _) => { lock (_callbacks) _callbacks.Add(cb); return Task.FromResult<InteractionCallbackResponse?>(null); }, _rest);
        var context = new ModalInteractionContext(interaction, null!);

        var discordSetGamertag = new DiscordSetGamertag(
            _context,
            Substitute.For<ILogger<DiscordSetGamertag>>(),
            _scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            _setGamertagService);
        return (discordSetGamertag, context.ToDiscordContext());
    }

    [Test]
    public async Task SetGamertag_ReadsTextInputInsideLabel()
    {
        var (handler, context) = CreateHandler("Grunt Padre");
        await handler.SetGamertag(context);

        await _setGamertagService.Received(1).SetGamertag(Arg.Any<int>(), "Grunt Padre", Arg.Any<CancellationToken>());
        Assert.That(_callbacks.Single().Type, Is.EqualTo(InteractionCallbackType.DeferredMessage));
    }
}
