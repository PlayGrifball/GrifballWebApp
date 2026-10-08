using System.Security.Claims;
using System.Text.Json;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Teams;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DiscordOnDeckMessagesTests
{
    private const ulong DraftChannel = 555_000_111;
    private GrifballContext _context = null!;
    private FakeRestRequestHandler _handler = null!;
    private DiscordOnDeckMessages _sut = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var rest = FakeRestRequestHandler.CreateClient(out _handler);
        var teamService = new TeamService(_context, Substitute.For<IPublisher>());
        _sut = new DiscordOnDeckMessages(teamService, rest, _context, Options.Create(new DiscordOptions { DraftChannel = DraftChannel }));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    internal static List<JsonElement> StringMenus(JsonElement body)
    {
        var result = new List<JsonElement>();
        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                if (e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Number && t.GetInt32() == 3)
                {
                    result.Add(e);
                    return;
                }
                foreach (var p in e.EnumerateObject()) Walk(p.Value);
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in e.EnumerateArray()) Walk(x);
            }
        }
        if (body.TryGetProperty("components", out var comps)) Walk(comps);
        return result;
    }

    internal static string? Content(JsonElement body) =>
        body.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

    [Test]
    public async Task SendMessage_Should_ReportFailure_When_NoTeams()
    {
        var season = await _context.AddSeason();

        await _sut.SendMessageAsync(season.SeasonID);

        var sent = _handler.SentMessages();
        Assert.That(sent, Has.Count.EqualTo(1));
        Assert.That(sent[0].ChannelId, Is.EqualTo(DraftChannel));
        Assert.That(Content(sent[0].Body), Is.EqualTo("Failed to find team"));
    }

    [Test]
    public async Task SendMessage_Should_ReportCompletion_When_PoolEmpty()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        await _context.AddSignup(season, cap);
        await _context.AddTeam(season, "T", cap, 1);

        await _sut.SendMessageAsync(season.SeasonID);

        var sent = _handler.SentMessages();
        Assert.That(sent, Has.Count.EqualTo(1));
        Assert.That(Content(sent[0].Body), Is.EqualTo("Draft has been completed"));
    }

    [Test]
    public async Task SendMessage_Should_MentionOnDeckCaptain_And_ListPoolSortedByName()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        _context.UserClaims.Add(new UserClaim { UserId = cap.Id, ClaimType = ClaimTypes.NameIdentifier, ClaimValue = "987654321" });
        await _context.SaveChangesAsync();
        await _context.AddTeam(season, "T", cap, 1);
        var zed = await _context.AddUser("zed", gamertag: "Zed");
        var amy = await _context.AddUser("amy", gamertag: "Amy");
        await _context.AddSignup(season, zed);
        await _context.AddSignup(season, amy);

        await _sut.SendMessageAsync(season.SeasonID);

        var sent = _handler.SentMessages();
        Assert.That(sent, Has.Count.EqualTo(1), _handler.Requests.FirstOrDefault()?.Body);
        Assert.That(sent[0].ChannelId, Is.EqualTo(DraftChannel));
        Assert.That(Content(sent[0].Body), Is.EqualTo("<@987654321> is on deck"));
        var menus = StringMenus(sent[0].Body);
        Assert.That(menus, Has.Count.EqualTo(1), sent[0].Body.ToString());
        var menu = menus[0];
        Assert.That(menu.GetProperty("custom_id").GetString(), Is.EqualTo($"draftpick:{season.SeasonID}:{cap.Id}:0"));
        Assert.That(menu.GetProperty("placeholder").GetString(), Is.EqualTo("Make pick - Amy - Zed"));
        var options = menu.GetProperty("options").EnumerateArray().Select(o => (o.GetProperty("label").GetString(), o.GetProperty("value").GetString())).ToList();
        Assert.That(options, Is.EqualTo(new[] { ("Amy", amy.Id.ToString()), ("Zed", zed.Id.ToString()) }));
    }

    [Test]
    public async Task SendMessage_Should_UseDisplayName_When_ClaimIsNotADiscordId()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Captain Display");
        _context.UserClaims.Add(new UserClaim { UserId = cap.Id, ClaimType = ClaimTypes.NameIdentifier, ClaimValue = "not-a-number" });
        await _context.SaveChangesAsync();
        await _context.AddTeam(season, "T", cap, 1);
        var p = await _context.AddUser("p", gamertag: "P");
        await _context.AddSignup(season, p);

        await _sut.SendMessageAsync(season.SeasonID);

        Assert.That(Content(_handler.SentMessages().Single().Body), Is.EqualTo("Captain Display is on deck"));
    }

    [Test]
    public async Task SendMessage_Should_FallBackToUserId_When_NoDisplayName()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap");
        await _context.AddTeam(season, "T", cap, 1);
        var p = await _context.AddUser("p", gamertag: "P");
        await _context.AddSignup(season, p);

        await _sut.SendMessageAsync(season.SeasonID);

        Assert.That(Content(_handler.SentMessages().Single().Body), Is.EqualTo($"{cap.Id} is on deck"));
    }

    [Test]
    public async Task SendMessage_Should_SplitLargePool_Into25OptionMenus_And5MenusPerMessage()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        await _context.AddTeam(season, "T", cap, 1);
        // 151 players -> 7 menus (6x25 + 1) -> 2 messages (5 menus + 2 menus)
        for (var i = 0; i < 151; i++)
        {
            _context.Users.Add(new User { UserName = $"user{i:000}", DisplayName = $"P{i:000}" });
        }
        await _context.SaveChangesAsync();
        var userIds = _context.Users.Where(u => u.UserName!.StartsWith("user")).Select(u => u.Id).ToList();
        _context.SeasonSignups.AddRange(userIds.Select(id => new SeasonSignup { SeasonID = season.SeasonID, UserID = id, Timestamp = new DateTime(2024, 12, 1) }));
        await _context.SaveChangesAsync();

        await _sut.SendMessageAsync(season.SeasonID);

        var sent = _handler.SentMessages();
        Assert.That(sent, Has.Count.EqualTo(2));
        Assert.That(sent.Select(s => s.ChannelId), Is.All.EqualTo(DraftChannel));
        Assert.That(Content(sent[0].Body), Is.EqualTo("Cap is on deck"));
        Assert.That(Content(sent[1].Body), Is.Null);

        var first = StringMenus(sent[0].Body);
        var second = StringMenus(sent[1].Body);
        Assert.That(first, Has.Count.EqualTo(5));
        Assert.That(second, Has.Count.EqualTo(2));
        var all = first.Concat(second).ToList();
        Assert.That(all.Select(m => m.GetProperty("options").GetArrayLength()), Is.EqualTo(new[] { 25, 25, 25, 25, 25, 25, 1 }));
        Assert.That(all.Select(m => m.GetProperty("custom_id").GetString()),
            Is.EqualTo(Enumerable.Range(0, 7).Select(i => $"draftpick:{season.SeasonID}:{cap.Id}:{i}")));
        Assert.That(all[0].GetProperty("placeholder").GetString(), Is.EqualTo("Make pick - P000 - P024"));
        Assert.That(all[6].GetProperty("placeholder").GetString(), Is.EqualTo("Make pick - P150 - P150"));
    }
}
