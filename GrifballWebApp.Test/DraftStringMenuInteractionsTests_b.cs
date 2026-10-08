using System.Reflection;
using System.Text.Json;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Test.CovB;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using NSubstitute;
using User = GrifballWebApp.Database.Models.User;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using TeamsStringMenuInteractions = GrifballWebApp.Server.Teams.StringMenuInteractions;

namespace GrifballWebApp.Test;

/// <summary>Tests for the draft-pick string menu (GrifballWebApp.Server.Teams.StringMenuInteractions).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DraftStringMenuInteractionsTests_b
{
    private const ulong DiscordId = 4242;
    private GrifballContext _context = null!;
    private FakeRestRequestHandler_b _handler = null!;
    private RestClient _rest = null!;
    private readonly List<InteractionCallback> _callbacks = [];

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _rest = FakeRestRequestHandler_b.CreateClient(out _handler);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private TeamsStringMenuInteractions CreateModule(TeamService teamService, string selectedValue, ulong discordId = DiscordId)
    {
        var json = $$"""
        {"id":"1","application_id":"2","type":3,"data":{"custom_id":"draftpick:1:2:0","component_type":3,"values":["{{selectedValue}}"]},
         "channel_id":"3","channel":{"id":"3","type":0},
         "user":{"id":"{{discordId}}","username":"u","discriminator":"0"},"token":"tok","version":1,
         "message":{"id":"9","channel_id":"3","author":{"id":"2","username":"bot","discriminator":"0"},"content":"","timestamp":"2024-01-01T00:00:00+00:00","tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"type":0},
         "locale":"en-US","entitlements":[],"authorizing_integration_owners":{},"context":1}
        """;
        var model = JsonSerializer.Deserialize<JsonInteraction>(json,
            new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString })!;
        var interaction = new StringMenuInteraction(model, null!, (_, cb, _, _) => { lock (_callbacks) _callbacks.Add(cb); return Task.CompletedTask; }, _rest);
        var context = new StringMenuInteractionContext(interaction, null!);

        var module = new TeamsStringMenuInteractions(_context, teamService);
        typeof(BaseComponentInteractionModule<StringMenuInteractionContext>)
            .GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(module, context);
        return module;
    }

    private List<string?> ModifiedContents() => _handler.Requests
        .Where(r => r.Method == HttpMethod.Patch && r.Path.EndsWith("/messages/@original"))
        .Select(r => JsonDocument.Parse(r.Body!).RootElement.GetProperty("content").GetString())
        .ToList();

    private async Task<(Season season, User captain, User player)> SeedDraft()
    {
        var season = await _context.AddSeason();
        var captain = await _context.AddUser("cap", "Cap", gamertag: "CapGT");
        captain.DiscordUser = new DiscordUser { DiscordUserID = (long)DiscordId, DiscordUsername = "capdiscord" };
        await _context.SaveChangesAsync();
        await _context.AddTeam(season, "Cap Team", captain, 1);
        var player = await _context.AddUser("player", "Player");
        return (season, captain, player);
    }

    [Test]
    public async Task DraftPick_Should_RejectUsersWithoutGamertag()
    {
        // User exists with this discord id but has no Xbox account linked
        var user = await _context.AddUser("nogt", "No GT", discordName: "nogt");
        var discordId = (ulong)user.DiscordUserID!.Value;
        var module = CreateModule(new TeamService(_context, Substitute.For<IPublisher>()), "1", discordId);

        await module.DraftPick(1, 2, 0); // includes the app's 5s temp-response delay

        var cb = _callbacks.Single();
        Assert.That(cb.Type, Is.EqualTo(InteractionCallbackType.Message));
        var data = (InteractionMessageProperties)cb.GetType().GetProperty("Data")!.GetValue(cb)!;
        Assert.That(data.Content, Is.EqualTo("You must set your gamertag first"));
        Assert.That(_handler.Requests.Any(r => r.Method == HttpMethod.Delete), Is.True, "temp response is deleted afterwards");
        Assert.That(ModifiedContents(), Is.Empty);
    }

    [Test]
    public async Task DraftPick_Should_AddPlayer_And_ConfirmPick()
    {
        var (season, captain, player) = await SeedDraft();
        await _context.AddSignup(season, player);
        var publisher = Substitute.For<IPublisher>();
        var module = CreateModule(new TeamService(_context, publisher), player.Id.ToString());

        await module.DraftPick(season.SeasonID, captain.Id, 0);

        Assert.That(_callbacks.Single().Type, Is.EqualTo(InteractionCallbackType.DeferredMessage));
        Assert.That(ModifiedContents(), Is.EqualTo(new[] { $"<@{DiscordId}>, your pick was made successfully" }));
        await using var ctx = _context.NewContextLike();
        var tp = await ctx.TeamPlayers.SingleAsync(x => x.UserID == player.Id);
        Assert.That(tp.DraftRound, Is.EqualTo(1));
        Assert.That(publisher.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task DraftPick_Should_ReportTeamServiceErrors()
    {
        var (season, captain, player) = await SeedDraft(); // player not signed up
        var module = CreateModule(new TeamService(_context, Substitute.For<IPublisher>()), player.Id.ToString());

        await module.DraftPick(season.SeasonID, captain.Id, 0);

        Assert.That(ModifiedContents(), Is.EqualTo(new[] { $"Hey <@{DiscordId}>, This player has not signed up" }));
        Assert.That(await _context.NewContextLike().TeamPlayers.AnyAsync(x => x.UserID == player.Id), Is.False);
    }

    [Test]
    public async Task DraftPick_Should_ReportGenericErrors()
    {
        var (season, captain, player) = await SeedDraft();
        await _context.AddSignup(season, player);
        var broken = _context.NewContextLike();
        await broken.DisposeAsync();
        var module = CreateModule(new TeamService(broken, Substitute.For<IPublisher>()), player.Id.ToString());

        await module.DraftPick(season.SeasonID, captain.Id, 0);

        Assert.That(ModifiedContents(), Is.EqualTo(new[] { $"Hey <@{DiscordId}>, something bad happened. Please try again and contact sysadmin if problem persists" }));
    }
}
