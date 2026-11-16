using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server.Chat;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The site's chat hub on a scripted model (<see cref="ScriptedChatClient"/>): who it asks as (the user as HubSessionFilter found
/// them, <see cref="HubUser"/>), what it streams back, what it refuses and how a failure reaches the browser. No host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ChatHubUnitTests
{
    private static readonly string[] HiThere = ["Hi ", "there"];
    private static readonly string[] Hi = ["Hi"];
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly IChatClientFactory _clients = Substitute.For<IChatClientFactory>();
    private readonly IAmbientCurrentUser _ambient = Substitute.For<IAmbientCurrentUser>();
    private readonly RecordingLogger<ChatHub> _logger = new();
    private readonly ChatLimits _limits = new();
    // Where a change the model asks for waits (ChatAccounts), in memory
    private readonly ServiceProvider _cache = new ServiceCollection().AddSingleton<IChatWaiting>(new MemoryChatWaiting(TimeProvider.System)).BuildServiceProvider();

    [TearDown]
    public async Task TearDown()
    {
        _limits.Dispose();
        await _cache.DisposeAsync();
    }

    /// <summary>
    /// The hub for <paramref name="user"/> as the database has them now (signed out by default), on a connection whose claims are
    /// <paramref name="claims"/> (signed out by default), from <paramref name="address"/>.
    /// </summary>
    private ChatHub Hub(IChatClient model, HubUserState? user = null, ClaimsPrincipal? claims = null, string address = "203.0.113.9")
    {
        _clients.Models.Returns([new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b")]);
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(model);
        _clients.Available.Returns(true);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(address);
        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new HttpContextHolder { HttpContext = http });
        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(claims ?? new ClaimsPrincipal(new ClaimsIdentity()));
        context.ConnectionId.Returns("connection-1");
        context.Features.Returns(features);
        var now = new HubUser();
        now.Set(user ?? HubUserState.Anonymous);
        return new ChatHub(new GrifChat(new ChatModel(_clients, NullLoggerFactory.Instance), Substitute.For<IServiceScopeFactory>(), _limits, new ChatAccounts(_cache.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<ChatAccounts>.Instance)), now, _ambient, _logger) { Context = context };
    }

    // With the page's own choice of whether changes wait (false): these tests have no database for the user's setting (ChatHubModelsUnitTests)
    private static ChatRequestDto Question(string question = "Who won?", params ChatTurnDto[] history)
        => new() { Question = question, History = [.. history], AutoApprove = false };

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
        var hub = Hub(model, new HubUserState(7, ["Player"]));

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
    public async Task Ask_TheRepliesSpans_AreInTheHubCallsTrace_ThoughItStreamsAfterTheCallReturns()
    {
        using var spans = new ChatSpans();
        Activity? modelCall = null;
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi")) { BeforeReply = _ => { modelCall = Activity.Current; return Task.CompletedTask; } };
        var hub = Hub(model);
        ChannelReader<string> reply;
        Activity hubCall;

        // SignalR's span for the hub call ends when Ask returns; the reply streams on after it
        using (hubCall = ChatSpans.Host.StartActivity("ChatHub/Ask")!)
            reply = await hub.Ask(Question(), CancellationToken.None);
        var (_, error) = await Read(reply);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(modelCall?.Source.Name, Is.EqualTo(ChatModel.SourceName), "the model's call is traced");
            Assert.That(modelCall?.TraceId, Is.EqualTo(hubCall.TraceId));
            Assert.That(ChatSpans.Ancestors(modelCall!), Does.Contain(hubCall));
        }
    }

    [Test]
    public async Task Ask_SignedInStaff_GetTheirStaffTools_AsTheirRolesAllow()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        var (_, error) = await Read(await Hub(model, new HubUserState(7, ["Player", "Commissioner"])).Ask(Question(), CancellationToken.None));

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
    public async Task Ask_ARoleTheClaimsStillHaveButTheUserLost_GivesNoneOfItsTools()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));
        var claims = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "Sysadmin")], "Cookies"));

        await Read(await Hub(model, new HubUserState(7, ["Player"]), claims).Ask(Question(), CancellationToken.None));

        Assert.That(model.Options!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat), "the user's roles now, not the connection's claims");
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

    [Test]
    public async Task Models_SignedOut_TheModelAnyoneMayUse_WithNoSpend()
    {
        var models = await Hub(new ScriptedChatClient()).Models();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(models.Models.Select(m => (m.Id, m.Name)), Is.EqualTo(new[] { ("ollama", "llama3.2:3b") }));
            Assert.That((models.Selected, models.Spent, models.Limit), Is.EqualTo(("ollama", (decimal?)null, (decimal?)null)));
        }
    }

    [Test]
    public void Choose_SignedOut_IsRefused_ToTheBrowser()
    {
        var hub = Hub(new ScriptedChatClient());

        Assert.That(async () => await hub.Choose("ollama"), Throws.TypeOf<HubException>().With.Message.EqualTo(GrifChat.NotYours));
    }

    [Test]
    public async Task Ask_AModelNotTheirs_IsRefused_ToTheBrowser()
    {
        var (_, error) = await Read(await Hub(new ScriptedChatClient()).Ask(Question() with { Model = "claude-opus" }, CancellationToken.None));

        Assert.That(error, Is.EqualTo(GrifChat.NotYours));
        _clients.DidNotReceive().Create(Arg.Any<ChatModelOption>());
    }

    [Test]
    public async Task Ask_NoModelConfigured_SaysTheChatIsUnavailable()
    {
        var hub = Hub(new ScriptedChatClient((TimeSpan.Zero, "Never")));
        _clients.Available.Returns(false);

        var (texts, error) = await Read(await hub.Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.EqualTo(GrifChat.Unavailable));
            Assert.That(texts, Is.Empty);
        }
        _clients.DidNotReceive().Create(Arg.Any<ChatModelOption>());
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Ask_NoQuestion_IsRefused_WithoutAskingTheModel(string question)
    {
        var (_, error) = await Read(await Hub(new ScriptedChatClient()).Ask(Question(question), CancellationToken.None));

        Assert.That(error, Is.EqualTo("Ask a question."));
        _clients.DidNotReceive().Create(Arg.Any<ChatModelOption>());
    }

    [Test]
    public async Task Ask_AHistoryOfNulls_IsRefusedAsUnreadable_NotThrown()
    {
        var hub = Hub(new ScriptedChatClient());

        var (_, noHistory) = await Read(await hub.Ask(new ChatRequestDto { Question = "Hi", History = null! }, CancellationToken.None));
        var (_, noRequest) = await Read(await hub.Ask(null!, CancellationToken.None));
        var (_, nullTurn) = await Read(await hub.Ask(new ChatRequestDto { Question = "Hi", History = [null!] }, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noHistory, Is.EqualTo(GrifChat.Unreadable));
            Assert.That(noRequest, Is.EqualTo(GrifChat.Unreadable));
            Assert.That(nullTurn, Is.EqualTo(GrifChat.Unreadable));
            Assert.That(_logger.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task Ask_WhileTheirLastReplyIsUnderWay_IsRefused_AndAllowedOnceItIsDone()
    {
        var release = new TaskCompletionSource();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi")) { BeforeReply = _ => release.Task };
        var hub = Hub(model, new HubUserState(7, []));

        var first = await hub.Ask(Question(), CancellationToken.None);
        var (_, refused) = await Read(await hub.Ask(Question(), CancellationToken.None));
        release.SetResult();
        await Read(first);
        var (_, third) = await Read(await hub.Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(ChatLimits.Busy));
            Assert.That(third, Is.Null);
        }
    }

    [Test]
    public async Task Ask_RightAfterTheBrowserStopsTheLastReply_IsAnswered_ThoughThatReplyIsStillWindingDown()
    {
        var neverEnds = new TaskCompletionSource();
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hub = Hub(new ScriptedChatClient((TimeSpan.Zero, "Late"))
        {
            BeforeReply = async _ =>
            {
                asked.SetResult();
                await neverEnds.Task;
            },
        });
        using var stop = new CancellationTokenSource();
        await hub.Ask(Question(), stop.Token);
        await asked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The page's Stop: SignalR cancels the stream's token
        await stop.CancelAsync();
        var (texts, error) = await Read(await Hub(new ScriptedChatClient((TimeSpan.Zero, "Hi"))).Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(texts, Is.EqualTo(Hi));
        }
        neverEnds.SetResult();
    }

    [Test]
    public async Task Ask_SignedOutAddressOverItsQuestionsAMinute_IsRefused_ItsWholeIPv6Slash64Alike()
    {
        var errors = new List<string?>();
        for (var i = 0; i <= ChatLimits.OthersPerMinute; i++)
            errors.Add((await Read(await Hub(new ScriptedChatClient((TimeSpan.Zero, "Hi")), address: $"2001:db8:1:2::{i + 1:x}").Ask(Question(), CancellationToken.None))).Error);

        Assert.That(errors, Is.EqualTo(Enumerable.Repeat<string?>(null, ChatLimits.OthersPerMinute).Append(ChatLimits.TooMany)));
    }

    [Test]
    public async Task Ask_ModelFails_TellsTheBrowserItCouldNotAnswer_AndLogsWhy()
    {
        var model = new ScriptedChatClient { BeforeReply = _ => throw new HttpRequestException("Ollama is down") };

        var (_, error) = await Read(await Hub(model, new HubUserState(7, [])).Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.EqualTo(ChatHub.Failed));
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<HttpRequestException>());
            Assert.That(_logger.Entries.Single().Message, Does.Contain("user 7"));
        }
        _ambient.Received(1).End();
    }

    [Test]
    public async Task Ask_TheChatEndingTheReply_TellsTheBrowserWhy_AndLogsNothing()
    {
        var model = new ScriptedChatClient { BeforeReply = _ => throw new ChatRefusedException(GrifChat.TookTooLong) };

        var (_, error) = await Read(await Hub(model).Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.EqualTo(GrifChat.TookTooLong));
            Assert.That(_logger.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task Ask_StartingTheReplyFails_TellsTheBrowserItCouldNotAnswer_AndLogsWhy()
    {
        var hub = Hub(new ScriptedChatClient((TimeSpan.Zero, "Never")), new HubUserState(7, []));
        // The models read as the reply starts (as the user's choice and month are, from the database): it fails
        _clients.Models.Returns(_ => throw new InvalidOperationException("The database is down"));

        var (texts, error) = await Read(await hub.Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.EqualTo(ChatHub.Failed));
            Assert.That(texts, Is.Empty);
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_logger.Entries.Single().Message, Does.Contain("user 7"));
        }
    }

    [Test]
    public async Task Ask_StoppedWhileTheReplyStarts_EndsTheStreamQuietly()
    {
        using var browser = new CancellationTokenSource();
        var hub = Hub(new ScriptedChatClient((TimeSpan.Zero, "Never")));
        // The user presses Stop while the reply's start reads the database
        _clients.Models.Returns(_ =>
        {
            browser.Cancel();
            throw new OperationCanceledException(browser.Token);
        });

        var (texts, error) = await Read(await hub.Ask(Question(), browser.Token));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null, "No error for a question the user stopped");
            Assert.That(texts, Is.Empty);
            Assert.That(_logger.Entries, Is.Empty, "A stop is no failure");
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
    public async Task Ask_WithNoHttpContext_CountsTheSignedOutAsker_UnderNoAddress()
    {
        var hub = Hub(new ScriptedChatClient((TimeSpan.Zero, "Hi")));
        hub.Context.Features.Returns(new FeatureCollection());

        var (texts, error) = await Read(await hub.Ask(Question(), CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(texts, Is.EqualTo(Hi));
        }
    }

    [TestCase('a')]
    [TestCase('\u0001')]
    [TestCase('一')]
    public void MaximumReceiveMessageSize_TakesAQuestionAndTheHistoryTheServerReads_InAnyCharacters(char character)
    {
        // The page's largest: the history the server reads, every message and the question at their limits, every character the
        // costliest JSON has (a control character or, escaped by default, any non-ASCII one: 6 bytes)
        var request = new ChatRequestDto
        {
            Question = new string(character, GrifChat.MaxQuestionLength),
            History = [.. Enumerable.Range(0, GrifChat.HistoryLimit).Select(i => new ChatTurnDto { FromBot = i % 2 == 1, Text = new string(character, GrifChat.MaxHistoryMessageLength) })],
        };
        var invocation = JsonSerializer.Serialize(new { type = 4, invocationId = "1", target = "Ask", arguments = new object[] { request }, streamIds = Array.Empty<string>() },
            WebJson) + "\u001e";

        Assert.That(Encoding.UTF8.GetByteCount(invocation), Is.LessThanOrEqualTo(ChatHub.MaximumReceiveMessageSize));
    }

    private sealed class HttpContextHolder : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
