using System.Net;
using System.Security.Claims;
using System.Threading.Channels;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server.Chat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The site's chat hub on a scripted model (<see cref="ScriptedChatClient"/>): who it asks as, what it streams back, what it
/// refuses and how a failure reaches the browser. No host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ChatHubUnitTests
{
    private static readonly string[] HiThere = ["Hi ", "there"];
    private static readonly string[] Hi = ["Hi"];
    private static readonly string[] PlayerAndCommissioner = ["Player", "Commissioner"];

    private readonly IChatClientFactory _clients = Substitute.For<IChatClientFactory>();
    private readonly IAmbientCurrentUser _ambient = Substitute.For<IAmbientCurrentUser>();
    private readonly RecordingLogger<ChatHub> _logger = new();
    private readonly ChatLimits _limits = new();

    [TearDown]
    public void TearDown() => _limits.Dispose();

    private ChatHub Hub(IChatClient model, ClaimsPrincipal? user = null, string connectionId = "connection-1", string address = "203.0.113.9")
    {
        _clients.Create().Returns(model);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(address);
        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new HttpContextHolder { HttpContext = http });
        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(user ?? new ClaimsPrincipal(new ClaimsIdentity()));
        context.ConnectionId.Returns(connectionId);
        context.Features.Returns(features);
        return new ChatHub(new GrifChat(_clients, Substitute.For<IServiceScopeFactory>()), _limits, _ambient, _logger) { Context = context };
    }

    private static ClaimsPrincipal SignedIn(int userId, params string[] roles)
        => new(new ClaimsIdentity(
            roles.Select(r => new Claim(ClaimTypes.Role, r)).Prepend(new Claim(ClaimTypes.NameIdentifier, userId.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            "Cookies"));

    private static ChatRequestDto Question(string question = "Who won?", params ChatTurnDto[] history) => new() { Question = question, History = [.. history] };

    /// <summary>Everything the reply streamed, then how it ended: null, or the error the browser is shown.</summary>
    private static async Task<(List<string> Texts, string? Error)> Read(ChannelReader<string> reply)
    {
        var texts = new List<string>();
        try
        {
            await foreach (var text in reply.ReadAllAsync())
                texts.Add(text);
            return (texts, null);
        }
        catch (HubException e)
        {
            return (texts, e.Message);
        }
    }

    [Test]
    public async Task Ask_SignedIn_AsksAsTheUserWithTheirRoles_InPrivate_AndStreamsTheReply()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi "), (TimeSpan.Zero, ""), (TimeSpan.Zero, "there"));
        var hub = Hub(model, SignedIn(7, "Player"));

        var (texts, error) = await Read(await hub.Ask(Question("And now?", new ChatTurnDto { FromBot = false, Text = "Hi" }, new ChatTurnDto { FromBot = true, Text = "Hello" }), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(texts, Is.EqualTo(HiThere), "Empty chunks aren't sent");
            Assert.That(model.Messages.Select(m => (m.Role, m.Text)), Is.EqualTo(new[] { (ChatRole.User, "Hi"), (ChatRole.Assistant, "Hello"), (ChatRole.User, "And now?") }));
            Assert.That(model.Options!.Instructions, Is.EqualTo(GrifChat.Instructions(ChatHost.Site)));
            // A player may pull: every chat tool
            Assert.That(model.Options!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
        }
        Received.InOrder(() =>
        {
            _ambient.Begin();
            _ambient.UserId = 7;
            _ambient.End();
        });
    }

    [Test]
    public async Task Ask_SignedInStaff_GetTheirStaffTools_AsTheirRolesAllow()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        var (_, error) = await Read(await Hub(model, SignedIn(7, "Player", "Commissioner")).Ask(Question(), CancellationToken.None));

        var tools = model.Options!.Tools!.Select(t => t.Name).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            // A commissioner's dashboard and a match's result, but not a sysadmin's claims
            Assert.That(tools, Does.Contain("get_commissioner_dashboard").And.Contain("report_season_match_game"));
            Assert.That(tools, Does.Not.Contain("list_pending_gamertag_claims").And.Not.Contain("decide_gamertag_claim"));
        }
    }

    [Test]
    public async Task Ask_SignedOut_AsksWithNoAccount_AsAnUnlinkedDiscordUser()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        var (_, error) = await Read(await Hub(model).Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(model.Options!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat.Where(t => !t.StartsWith("pull_", StringComparison.Ordinal))));
        }
        _ambient.Received(1).UserId = null;
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Ask_NoQuestion_IsRefused_WithoutAskingTheModel(string question)
    {
        var (_, error) = await Read(await Hub(new ScriptedChatClient()).Ask(Question(question), CancellationToken.None));

        Assert.That(error, Is.EqualTo("Ask a question."));
        _clients.DidNotReceive().Create();
    }

    [Test]
    public async Task Ask_WhileAReplyIsUnderWayOnTheConnection_IsRefused_AndAllowedOnceItIsDone()
    {
        var release = new TaskCompletionSource();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi")) { BeforeReply = _ => release.Task };
        var hub = Hub(model);

        var first = await hub.Ask(Question(), CancellationToken.None);
        var (_, refused) = await Read(await hub.Ask(Question(), CancellationToken.None));
        release.SetResult();
        var (firstTexts, _) = await Read(first);
        var (_, third) = await Read(await hub.Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(ChatLimits.Busy));
            Assert.That(firstTexts, Is.EqualTo(Hi));
            Assert.That(third, Is.Null);
        }
    }

    [Test]
    public async Task Ask_SignedOutAddressOverItsQuestionsAMinute_IsRefused()
    {
        var errors = new List<string?>();
        for (var i = 0; i <= ChatLimits.SignedOutPerMinute; i++)
            errors.Add((await Read(await Hub(new ScriptedChatClient((TimeSpan.Zero, "Hi")), connectionId: $"connection-{i}").Ask(Question(), CancellationToken.None))).Error);

        Assert.That(errors, Is.EqualTo(Enumerable.Repeat<string?>(null, ChatLimits.SignedOutPerMinute).Append(ChatLimits.TooMany)));
    }

    [Test]
    public async Task Ask_ModelFails_TellsTheBrowserItCouldNotAnswer_AndLogsWhy()
    {
        var model = new ScriptedChatClient { BeforeReply = _ => throw new HttpRequestException("Ollama is down") };

        var (_, error) = await Read(await Hub(model, SignedIn(7)).Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.EqualTo(ChatHub.Failed));
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<HttpRequestException>());
            Assert.That(_logger.Entries.Single().Message, Does.Contain("user 7"));
        }
        _ambient.Received(1).End();
    }

    [Test]
    public async Task Ask_ReplyCanceledButNotByTheBrowser_IsTheSitesTimeout()
    {
        var model = new ScriptedChatClient { BeforeReply = _ => throw new OperationCanceledException() };

        var (_, error) = await Read(await Hub(model).Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.EqualTo(ChatHub.TookTooLong));
            Assert.That(_logger.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task Ask_BrowserStopsReading_EndsTheReplyQuietly()
    {
        using var browser = new CancellationTokenSource();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"))
        {
            BeforeReply = async _ => await browser.CancelAsync(),
        };

        var (texts, error) = await Read(await Hub(model).Ask(Question(), browser.Token));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(texts, Is.Empty);
            Assert.That(_logger.Entries, Is.Empty);
        }
    }

    [Test]
    public void Caller_SignedIn_IsTheUserWithTheirRoles_SignedOutHasNoAccount()
    {
        var signedIn = ChatHub.Caller(SignedIn(7, "Player", "Commissioner"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signedIn.UserID, Is.EqualTo(7));
            Assert.That(signedIn.Roles, Is.EqualTo(PlayerAndCommissioner));
            Assert.That(signedIn.InPublic, Is.False);
            Assert.That(ChatHub.Caller(new ClaimsPrincipal(new ClaimsIdentity())), Is.SameAs(ToolCaller.Anonymous));
            Assert.That(ChatHub.Caller(null), Is.SameAs(ToolCaller.Anonymous));
        }
    }

    private sealed class HttpContextHolder : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    [Test]
    public void Limits_SignedInUser_HasTheirOwnLargerAllowance_ByUserNotAddress()
    {
        var partition = ChatLimits.Partition(new ToolCaller(7, []), "203.0.113.9");
        var refusals = Enumerable.Range(0, ChatLimits.SignedInPerMinute + 1).Select(i =>
        {
            var refusal = _limits.TryStart($"connection-{i}", partition);
            _limits.Finish($"connection-{i}");
            return refusal;
        }).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partition, Is.EqualTo("user:7"));
            Assert.That(ChatLimits.Partition(ToolCaller.Anonymous, "203.0.113.9"), Is.EqualTo("address:203.0.113.9"));
            Assert.That(refusals.Count(r => r is null), Is.EqualTo(ChatLimits.SignedInPerMinute));
            Assert.That(refusals[^1], Is.EqualTo(ChatLimits.TooMany));
        }
    }
}
