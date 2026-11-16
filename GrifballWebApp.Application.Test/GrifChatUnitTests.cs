using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The chat the Discord bot and the site share, on a model that answers as it is told (<see cref="FakeModel"/>): its
/// instructions, the conversation it sends, the tools it gives and calls, its limits and its time limit, and its telemetry (the
/// spans, metrics and logs of its model and tool calls, without what people wrote unless that is switched on).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public partial class GrifChatUnitTests
{
    private static readonly string[] Answer = ["It is ", "noon."];
    private static readonly string[] Ok = ["ok"];
    private static readonly string[] Canceled = ["canceled"];
    private const string Asker = "user:1";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly ChatLimits _limits = new(maxReplies: 3);
    /// <summary>A model anyone may use, free: the league's own.</summary>
    private static readonly ChatModelOption Free = new("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b");

    private readonly IChatClientFactory _clients = Substitute.For<IChatClientFactory>();
    private ServiceProvider _services = null!;

    [TearDown]
    public async Task TearDown()
    {
        _limits.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    /// <summary>The chat on <paramref name="model"/>, its tools reading <paramref name="clock"/> (else the test's), its logs collected.</summary>
    private GrifChat Chat(FakeModel model, bool sensitiveData = false, TimeProvider? clock = null)
    {
        _services = new ServiceCollection().AddSingleton(clock ?? _time).AddFakeLogging().AddSingleton<IChatWaiting, MemoryChatWaiting>().BuildServiceProvider();
        _clients.Available.Returns(true);
        _clients.Models.Returns([Free]);
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(model);
        var traced = new ChatModel(_clients, _services.GetRequiredService<ILoggerFactory>(), sensitiveData);
        var scopes = _services.GetRequiredService<IServiceScopeFactory>();
        return new GrifChat(traced, scopes, _limits, new ChatAccounts(scopes, _time, NullLogger<ChatAccounts>.Instance));
    }

    private static async Task<List<string>> All(IAsyncEnumerable<string> reply)
    {
        var texts = new List<string>();
        await foreach (var text in reply)
            texts.Add(text);
        return texts;
    }

    /// <summary>The whole reply to <paramref name="question"/>, started and streamed as a host does.</summary>
    private static async Task<List<string>> Ask(GrifChat chat, string question, ToolCaller caller, ChatHost host, string asker = Asker)
    {
        using var reply = await chat.StartAsync([], question, caller, host, asker);
        return await All(reply.StreamAsync());
    }

    [Test]
    public async Task Start_StreamsTheModelsReply_WithTheHostsInstructionsAndTheChatToolsTheCallerMayUse()
    {
        var model = new FakeModel([Text("It is "), Text("noon.")]);

        var reply = await Ask(Chat(model), "What time is it?", ToolCaller.Anonymous, ChatHost.Site);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply, Is.EqualTo(Answer));
            Assert.That(model.Requests.Single().Select(m => (m.Role, m.Text)), Is.EqualTo(new[] { (ChatRole.User, "What time is it?") }));
            Assert.That(model.Options.Single()!.Instructions, Is.EqualTo(GrifChat.Instructions(ChatHost.Site)));
            // No account: every chat tool but the pull tools, which only some roles may use
            Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat.Where(t => !t.StartsWith("pull_", StringComparison.Ordinal))));
        }
    }

    [Test]
    public async Task Start_ACallerWithAPlayersRole_GetsThePullToolsToo()
    {
        var model = new FakeModel([Text("Hi")]);

        await Ask(Chat(model), "Hi", new ToolCaller(7, ["Player"]), ChatHost.Discord);

        Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
    }

    /// <summary>The staff tools (<see cref="GrifTools.Staff"/>) <paramref name="roles"/> allow.</summary>
    private static List<string> StaffToolsFor(params string[] roles)
        => GrifTools.Staff.SelectMany(GrifTools.Methods)
            .Where(m => SiteRoles.IsInAnyRole(roles, GrifTools.Roles(m)))
            .Select(GrifTools.Name)
            .ToList();

    [TestCase(SiteRoles.Commissioner)]
    [TestCase(SiteRoles.Sysadmin)]
    public async Task Start_OnTheSite_AStaffCallerGetsTheirStaffToolsToo(string role)
    {
        var model = new FakeModel([Text("Hi")]);

        await Ask(Chat(model), "Hi", new ToolCaller(7, [SiteRoles.Player, role]), ChatHost.Site with { AutoApprove = false });

        var expected = StaffToolsFor(SiteRoles.Player, role);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(expected, Is.Not.Empty);
            Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat.Concat(expected)));
        }
    }

    [Test]
    public async Task Start_OnTheSite_APlayerGetsNoStaffTools()
    {
        var model = new FakeModel([Text("Hi")]);

        await Ask(Chat(model), "Hi", new ToolCaller(7, [SiteRoles.Player]), ChatHost.Site);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StaffToolsFor(SiteRoles.Player), Is.Empty);
            Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
        }
    }

    [Test]
    public async Task Start_OnDiscord_StaffGetNoStaffTools()
    {
        var model = new FakeModel([Text("Hi")]);

        await Ask(Chat(model), "Hi", new ToolCaller(7, [SiteRoles.Commissioner, SiteRoles.Sysadmin]), ChatHost.Discord);

        Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
    }

    [Test]
    public void Staff_IsEveryClassOfToolsThatChangeData_AndThePrivateChatHasAllTheirTools()
    {
        var changingData = GrifTools.ChangingData.SelectMany(GrifTools.Methods).Select(GrifTools.Name).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifTools.Staff, Is.SupersetOf(GrifTools.ChangingData));
            Assert.That(changingData, Is.Not.Empty);
            Assert.That(GrifTools.PrivateChat, Is.SupersetOf(changingData));
            Assert.That(GrifTools.Chat.Intersect(changingData), Is.Empty);
        }
    }

    /// <summary>The function a reply's tool wraps (DelegatingAIFunction's InnerFunction).</summary>
    private static object? Inner(AITool tool)
        => typeof(DelegatingAIFunction).GetProperty("InnerFunction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(tool);

    [Test]
    public void AIFunctions_AreMadeOnce_OnlyTheCallerBoundPerReply()
    {
        var scopes = Substitute.For<IServiceScopeFactory>();

        var first = GrifTools.AIFunctions(scopes, ToolCaller.Anonymous, GrifTools.Chat).Single(t => t.Name == "get_current_time");
        var second = GrifTools.AIFunctions(scopes, new ToolCaller(7, ["Player"]), GrifTools.Chat).Single(t => t.Name == "get_current_time");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Not.SameAs(second), "each reply's own, binding its caller");
            Assert.That(Inner(first), Is.Not.Null.And.SameAs(Inner(second)), "the function itself made once");
        }
    }

    [Test]
    public async Task Start_CallsTheToolTheModelAsksFor_AndSendsItTheResult_WhateverTheProvider()
    {
        // The factory's model calls no tools itself: GrifChat does it for any provider
        var model = new FakeModel(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_current_time")])],
            [Text("It is "), Text("noon.")]);

        var reply = await Ask(Chat(model), "What time is it?", ToolCaller.Anonymous, ChatHost.Site);

        var result = model.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        var json = result.Result is JsonElement element ? element : JsonSerializer.SerializeToElement(result.Result);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Requests, Has.Count.EqualTo(2), "Asked again with the tool's result");
            Assert.That(result.CallId, Is.EqualTo("call-1"));
            Assert.That(json.GetProperty("unixSeconds").GetInt64(), Is.EqualTo(_time.GetUtcNow().ToUnixTimeSeconds()));
            Assert.That(string.Concat(reply), Is.EqualTo("It is noon."));
        }
    }

    [Test]
    public void Start_ModelSlowerThanTheHostsTimeout_IsTookTooLong()
    {
        var model = new FakeModel { Hang = true };
        var host = ChatHost.Site with { Timeout = TimeSpan.FromMilliseconds(20) };
        var chat = Chat(model);

        Assert.That(async () => await Ask(chat, "Hi", ToolCaller.Anonymous, host),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.TookTooLong));
    }

    [Test]
    public void Start_AProviderThatEndsQuietlyWhenTimedOut_IsTookTooLongStill()
    {
        var model = new FakeModel { Quiet = true };
        var host = ChatHost.Site with { Timeout = TimeSpan.FromMilliseconds(20) };
        var chat = Chat(model);

        Assert.That(async () => await Ask(chat, "Hi", ToolCaller.Anonymous, host),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.TookTooLong));
    }

    [Test]
    public void Start_TheAskerStoppingWaiting_IsNoTimeout()
    {
        var chat = Chat(new FakeModel { Hang = true });
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        Assert.That(async () =>
        {
            using var reply = await chat.StartAsync([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker);
            await All(reply.StreamAsync(stop.Token));
        }, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Start_NoModel_IsUnavailable()
    {
        var chat = Chat(new FakeModel());
        _clients.Available.Returns(false);

        Assert.That(async () => await chat.StartAsync([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.Unavailable));
        _clients.DidNotReceive().Create(Arg.Any<ChatModelOption>());
    }

    [Test]
    public void Start_AQuestionOverTheLimit_IsRefusedOnEveryHost([Values] bool discord)
    {
        var chat = Chat(new FakeModel());

        Assert.That(async () => await chat.StartAsync([], new string('a', GrifChat.MaxQuestionLength + 1), ToolCaller.Anonymous, discord ? ChatHost.DiscordDirect : ChatHost.Site, Asker),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo("Ask in 2000 characters or fewer."));
    }

    [Test]
    public async Task Start_TrimsTheHistoryAndCutsLongMessages_WhateverTheHost()
    {
        var history = Enumerable.Range(1, GrifChat.HistoryLimit + 5).Select(i => new ChatMessage(ChatRole.User, i == 30 ? new string('a', 5000) : $"m{i}"));

        using var reply = await Chat(new FakeModel()).StartAsync(history, "Next?", ToolCaller.Anonymous, ChatHost.Discord, Asker);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply.Conversation, Has.Count.EqualTo(GrifChat.HistoryLimit + 1));
            Assert.That(reply.Conversation[^2].Text, Has.Length.EqualTo(GrifChat.MaxHistoryMessageLength));
            Assert.That(reply.Conversation[^1].Text, Is.EqualTo("Next?"));
        }
    }

    [Test]
    public async Task Start_WhileTheAskersLastReplyIsUnderWay_IsBusy_UntilItIsDone()
    {
        var chat = Chat(new FakeModel([Text("Hi")]));
        var first = await chat.StartAsync([], "Hi", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:5");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await chat.StartAsync([], "Again", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:5"),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(ChatLimits.Busy));
            Assert.That(async () => (await chat.StartAsync([], "Hi", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:6")).Dispose(), Throws.Nothing, "another asker");
        }
        first.Dispose();
        Assert.That(async () => (await chat.StartAsync([], "Again", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:5")).Dispose(), Throws.Nothing);
    }

    [Test]
    public async Task StreamAsync_TheAskerStopping_FreesTheirSlotAtOnce_NotWhenTheReplyHasWoundDown()
    {
        var blocked = new TaskCompletionSource();
        var model = new FakeModel([Text("Late")]) { Blocked = blocked };
        var chat = Chat(model);
        using var stop = new CancellationTokenSource();
        using var first = await chat.StartAsync([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker);
        var streaming = All(first.StreamAsync(stop.Token));
        await model.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await stop.CancelAsync();

        // The model hasn't noticed: the first reply is still under way, yet the asker may ask again
        Assert.That(async () => (await chat.StartAsync([], "Again", ToolCaller.Anonymous, ChatHost.Site, Asker)).Dispose(), Throws.Nothing);
        blocked.SetResult();
        try
        {
            await streaming;
        }
        catch (OperationCanceledException)
        {
            // Canceled, as asked
        }
    }

    [Test]
    public async Task StreamAsync_Done_FreesTheSlot_EvenUndisposed()
    {
        var chat = Chat(new FakeModel([Text("Hi")]));
        var reply = await chat.StartAsync([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker);

        await All(reply.StreamAsync());

        Assert.That(async () => (await chat.StartAsync([], "Again", ToolCaller.Anonymous, ChatHost.Site, Asker)).Dispose(), Throws.Nothing);
        reply.Dispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Available_WhenTheFactoryHasAModel(bool configured)
    {
        var clients = Substitute.For<IChatClientFactory>();
        clients.Available.Returns(configured);

        Assert.That(new GrifChat(new ChatModel(clients, NullLoggerFactory.Instance), Substitute.For<IServiceScopeFactory>(), _limits, new ChatAccounts(Substitute.For<IServiceScopeFactory>(), TimeProvider.System, NullLogger<ChatAccounts>.Instance)).Available, Is.EqualTo(configured));
    }

    [Test]
    public void Instructions_TheHostsFirstAndLastLines_AroundWhatEveryHostSays()
    {
        var discord = GrifChat.Instructions(ChatHost.Discord).Split('\n');
        var site = GrifChat.Instructions(ChatHost.Site).Split('\n');
        var direct = GrifChat.Instructions(ChatHost.DiscordDirect).Split('\n');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord[0], Is.EqualTo("You are a Discord bot, and all messages must stay under the 2000-character limit."));
            Assert.That(discord[^1], Is.EqualTo("The bot also has slash commands; /help lists them."));
            Assert.That(site[0], Does.StartWith("You are the chat on the Grifball league's website"));
            // In private, the line about the tools that change data too
            Assert.That(site[1..^1], Is.EqualTo(discord[1..^1].Append(GrifChat.PrivateLine)));
            Assert.That(direct, Is.EqualTo(discord[..^1].Append(GrifChat.PrivateLine).Append(discord[^1])));
            Assert.That(discord, Has.None.EqualTo(GrifChat.PrivateLine));
            Assert.That(site, Has.Some.Contains("in UTC, as the tools give them"));
            Assert.That(string.Join('\n', site), Does.Not.Contain("Discord").And.Not.Contain("/help"));
        }
    }

    [Test]
    public void Hosts_ADiscordChannelIsPublic_ADmAndTheSitePrivate_AllTimed()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatHost.DiscordDirect.InPublic, Is.False);
            Assert.That(ChatHost.DiscordDirect with { Name = "discord", InPublic = true, Confirm = "" }, Is.EqualTo(ChatHost.Discord));
            Assert.That(ChatHost.Discord.ToolsFor(Free), Is.SameAs(GrifTools.Chat));
            Assert.That(ChatHost.DiscordDirect.ToolsFor(Free), Is.SameAs(GrifTools.PrivateChat));
            Assert.That(ChatHost.Site.ToolsFor(Free), Is.SameAs(GrifTools.PrivateChat));
            Assert.That(ChatHost.Discord.InPublic, Is.True);
            // One model, one reply at a time: no host may hold it forever
            Assert.That(ChatHost.Discord.Timeout, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(ChatHost.DiscordDirect.Timeout, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(ChatHost.Site.InPublic, Is.False);
            Assert.That(ChatHost.Site.Timeout, Is.EqualTo(TimeSpan.FromMinutes(2)));
        }
    }

    [Test]
    public void Conversation_KeepsTheLastMessages_CutsLongOnes_AndAsksTheQuestionLast()
    {
        var history = Enumerable.Range(1, GrifChat.HistoryLimit + 5)
            .Select(i => new ChatMessage(i % 2 == 0 ? ChatRole.Assistant : ChatRole.User, i == 30 ? new string('a', 5000) : $"m{i}"));

        var conversation = GrifChat.Conversation(history, "Next?");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation, Has.Count.EqualTo(GrifChat.HistoryLimit + 1));
            Assert.That(conversation[0].Text, Is.EqualTo("m6"));
            Assert.That(conversation[^2].Text, Is.EqualTo(new string('a', GrifChat.MaxHistoryMessageLength)));
            Assert.That(conversation[^2].Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That((conversation[^1].Role, conversation[^1].Text), Is.EqualTo((ChatRole.User, "Next?")));
        }
    }

    [Test]
    public void Conversation_LeavesOutTheChatsRefusals_AndTheQuestionsItRefused_ButKeepsAnAnswerCutShort()
    {
        ChatMessage User(string text) => new(ChatRole.User, text);
        ChatMessage Bot(string text) => new(ChatRole.Assistant, text);
        var history = new[]
        {
            User("Who won?"), Bot("The Grifs."),
            User(new string('a', 4000)), Bot(GrifChat.TooLong),
            User("Again?"), Bot(ChatLimits.TooMany),
            User("And now?"), Bot(ChatLimits.Full),
            User("Slow one"), Bot("Part of it"), Bot(GrifChat.TookTooLong),
            // Only what the chat says counts: a person may say anything
            User(ChatLimits.Busy),
        };

        var conversation = GrifChat.Conversation(history, "Next?");

        Assert.That(conversation.Select(m => (m.Role, m.Text)), Is.EqualTo(new[]
        {
            (ChatRole.User, "Who won?"), (ChatRole.Assistant, "The Grifs."),
            (ChatRole.User, "Slow one"), (ChatRole.Assistant, "Part of it"),
            (ChatRole.User, ChatLimits.Busy), (ChatRole.User, "Next?"),
        }));
    }

    [Test]
    public void Refusal_NoRequestAtAll_IsUnreadable()
        => Assert.That(GrifChat.Refusal((ChatRequestDto?)null), Is.EqualTo(GrifChat.Unreadable));

    [Test]
    public void History_OfARequest_TheBrowsersHistoryInItsRoles()
    {
        var request = new ChatRequestDto
        {
            Question = "And now?",
            History = [new ChatTurnDto { FromBot = false, Text = "Hi" }, new ChatTurnDto { FromBot = true, Text = "Hello" }],
        };

        Assert.That(GrifChat.History(request).Select(m => (m.Role, m.Text)), Is.EqualTo(new[] { (ChatRole.User, "Hi"), (ChatRole.Assistant, "Hello") }));
    }

    [TestCase("", "Ask a question.")]
    [TestCase("   ", "Ask a question.")]
    [TestCase(null, "Ask a question.")]
    [TestCase("Who won?", null)]
    public void Refusal_OfAQuestion(string? question, string? refusal)
        => Assert.That(GrifChat.Refusal(new ChatRequestDto { Question = question!, History = [] }), Is.EqualTo(refusal));

    [Test]
    public void Refusal_QuestionOverDiscordsLimit_IsRefused_AtTheLimitIsNot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifChat.Refusal(new string('a', GrifChat.MaxQuestionLength + 1)), Is.EqualTo("Ask in 2000 characters or fewer."));
            Assert.That(GrifChat.Refusal(new string('a', GrifChat.MaxQuestionLength)), Is.Null);
        }
    }

    [Test]
    public void Refusal_AHistoryTheBrowserSentAsNull_OrWithANullMessage_IsUnreadable_NotAFault()
    {
        // JSON's null passes for a required property: what a client sends, not what the types say
        var noHistory = JsonSerializer.Deserialize<ChatRequestDto>("""{"question":"Hi","history":null}""", JsonSerializerOptions.Web)!;
        var nullTurn = JsonSerializer.Deserialize<ChatRequestDto>("""{"question":"Hi","history":[null]}""", JsonSerializerOptions.Web)!;
        var nullText = JsonSerializer.Deserialize<ChatRequestDto>("""{"question":"Hi","history":[{"fromBot":true,"text":null}]}""", JsonSerializerOptions.Web)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifChat.Refusal(noHistory), Is.EqualTo(GrifChat.Unreadable));
            Assert.That(GrifChat.Refusal(nullTurn), Is.EqualTo(GrifChat.Unreadable));
            Assert.That(GrifChat.Refusal(nullText), Is.EqualTo(GrifChat.Unreadable));
        }
    }

    /// <summary>
    /// The whole reply, streamed on its own once the host's span has ended, as the site's hub streams it (a task of its own, after
    /// the hub call returns): the host's trace.
    /// </summary>
    private static async Task<ActivityTraceId> AskUnderAHostsSpan(GrifChat chat, string question)
    {
        ChatReply reply;
        ActivityTraceId trace;
        Task<List<string>> streaming;
        using (var host = Spans.Test.StartActivity("host call")!)
        {
            trace = host.TraceId;
            reply = await chat.StartAsync([], question, ToolCaller.Anonymous, ChatHost.Site, Asker);
            streaming = Task.Run(() => All(reply.StreamAsync()));
        }
        using (reply)
            await streaming;
        return trace;
    }

    [Test]
    public async Task StreamAsync_TracesASpanPerModelCallAndPerToolCall_InTheHostsTrace()
    {
        using var spans = new Spans();
        var model = new FakeModel([Call("get_current_time")], [Text("It is noon."), Usage(12, 5)]);

        var trace = await AskUnderAHostsSpan(Chat(model), "What time is it?");

        var traced = spans.Of(trace);
        var host = traced.Single(a => a.Source == Spans.Test);
        var orchestrate = traced.Single(a => a.OperationName == "orchestrate_tools");
        var calls = traced.Where(a => Operation(a) == "chat").OrderBy(a => a.StartTimeUtc).ToList();
        var tool = traced.Single(a => Operation(a) == "execute_tool");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(orchestrate.ParentSpanId, Is.EqualTo(host.SpanId), "the reply's spans are in the host's trace, though it streamed after the host's span ended");
            Assert.That(orchestrate.Source.Name, Is.EqualTo(ChatModel.SourceName));
            Assert.That(calls, Has.Count.EqualTo(2), "asked, then asked again with the tool's result");
            Assert.That(calls.Select(a => a.ParentSpanId), Is.All.EqualTo(orchestrate.SpanId));
            Assert.That(calls.Select(a => a.GetTagItem("gen_ai.request.model")), Is.All.EqualTo(model.ModelId));
            Assert.That(calls[1].GetTagItem("gen_ai.usage.input_tokens"), Is.EqualTo(12));
            Assert.That(calls[1].GetTagItem("gen_ai.usage.output_tokens"), Is.EqualTo(5));
            Assert.That(tool.ParentSpanId, Is.EqualTo(orchestrate.SpanId));
            Assert.That(tool.GetTagItem("gen_ai.tool.name"), Is.EqualTo("get_current_time"));
            Assert.That(tool.GetTagItem(GrifTools.OutcomeTag), Is.EqualTo("ok"));
            Assert.That(tool.Status, Is.Not.EqualTo(ActivityStatusCode.Error));
        }
    }

    [Test]
    public async Task StreamAsync_TheChatUntraced_TagsNoOtherSpanWithAToolsOutcome()
    {
        using var spans = new Spans(chat: false);
        var model = new FakeModel([Call("get_current_time")], [Text("Done.")]);

        var trace = await AskUnderAHostsSpan(Chat(model), "What time is it?");

        // The tool ran under the host's span, the current one then: not its own
        Assert.That(spans.Of(trace).Single().GetTagItem(GrifTools.OutcomeTag), Is.Null);
    }

    [Test]
    public async Task StreamAsync_MeasuresTheModelsTokensAndTime_PerCall()
    {
        using var measured = new Measurements();
        var model = new FakeModel([Text("Hi"), Usage(12, 5)]);

        await Ask(Chat(model), "Hi", ToolCaller.Anonymous, ChatHost.Site);

        var ours = measured.Of(model.ModelId);
        var tokens = ours.Where(m => m.Instrument == "gen_ai.client.token.usage").Select(m => (m.Tags["gen_ai.token.type"], m.Value));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens, Is.EquivalentTo(new[] { ((object?)"input", 12.0), ((object?)"output", 5.0) }));
            Assert.That(ours.Count(m => m.Instrument == "gen_ai.client.operation.duration"), Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StreamAsync_WhatPeopleWroteAndWhatToolsGave_IsInTheTelemetry_OnlyWhenSwitchedOn(bool sensitiveData)
    {
        using var spans = new Spans();
        var model = new FakeModel([Call("get_current_time")], [Text("The secret answer.")]);
        var chat = Chat(model, sensitiveData);

        var trace = await AskUnderAHostsSpan(chat, "The secret question?");

        var told = string.Join('\n', spans.Of(trace)
                .SelectMany(a => a.TagObjects.Select(t => t.Value?.ToString()).Concat(a.Events.SelectMany(e => e.Tags.Select(t => t.Value?.ToString())))))
            + string.Join('\n', _services.GetFakeLogCollector().GetSnapshot().Select(r => r.ToString()));
        var toolResult = _time.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(told.Contains("The secret question?", StringComparison.Ordinal), Is.EqualTo(sensitiveData));
            Assert.That(told.Contains("The secret answer.", StringComparison.Ordinal), Is.EqualTo(sensitiveData));
            Assert.That(told.Contains(toolResult, StringComparison.Ordinal), Is.EqualTo(sensitiveData));
        }
    }

    [Test]
    public void ChatModel_SensitiveData_IsOffUnlessSwitchedOn_WhateverTheEnvironmentSays()
    {
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(new FakeModel());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new ChatModel(_clients, NullLoggerFactory.Instance).Create(Free).GetService<OpenTelemetryChatClient>()!.EnableSensitiveData, Is.False);
            Assert.That(new ChatModel(_clients, NullLoggerFactory.Instance, sensitiveData: true).Create(Free).GetService<OpenTelemetryChatClient>()!.EnableSensitiveData, Is.True);
        }
    }

    [Test]
    public void ChatModel_IsEachModelsWholePipeline_BuiltOnce_WhateverTheFactoryGivesLater()
    {
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(new FakeModel(), new FakeModel(), new FakeModel());
        var model = new ChatModel(_clients, NullLoggerFactory.Instance);
        var other = Free with { Id = "other" };

        var first = model.Create(Free);

        using (Assert.EnterMultipleScope())
        {
            // Each wrapper makes a Meter and an ActivitySource of its own: one, never replaced, so none is left undisposed
            Assert.That(model.Create(Free), Is.SameAs(first));
            _clients.Received(1).Create(Free);
            var second = model.Create(other);
            Assert.That(second, Is.Not.SameAs(first), "Each model its own");
            Assert.That(model.Create(other), Is.SameAs(second));
            // The tools called over the traced model: an execute_tool span per call, on its source
            Assert.That(first, Is.InstanceOf<FunctionInvokingChatClient>());
            Assert.That(first.GetService<OpenTelemetryChatClient>(), Is.Not.Null);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StreamAsync_AFailingToolsMessage_IsInTheTelemetry_OnlyWhenSwitchedOn(bool sensitiveData)
    {
        using var spans = new Spans();
        var model = new FakeModel([Call("get_current_time")], [Text("Done.")]);
        // A failure's message may quote what the model passed, which is what someone asked
        var chat = Chat(model, sensitiveData, new ThrowingClock(new InvalidOperationException("No player SecretGamertag")));

        var trace = await AskUnderAHostsSpan(chat, "What time is it?");

        var traced = spans.Of(trace);
        var errors = _services.GetFakeLogCollector().GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
        var told = string.Join('\n', traced.SelectMany(a => a.TagObjects.Select(t => t.Value?.ToString()).Append(a.StatusDescription)))
            + string.Join('\n', _services.GetFakeLogCollector().GetSnapshot().Select(r => r + "\n" + r.Exception));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(told.Contains("SecretGamertag", StringComparison.Ordinal), Is.EqualTo(sensitiveData));
            // Its type and where it was thrown stay, whatever the switch says
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Exception?.ToString(), Does.Contain("System.InvalidOperationException").And.Contain(nameof(ThrowingClock)));
            Assert.That(traced.Single(a => Operation(a) == "execute_tool").Status, Is.EqualTo(ActivityStatusCode.Error));
        }
    }

    [Test]
    public async Task AIFunctions_LogEachCall_ToTheLoggerTheyAreGiven_NotOneFromTheCallsScope()
    {
        // No logging in the tools' services: the log is the chat's, not the scope's
        await using var services = new ServiceCollection().AddSingleton<TimeProvider>(_time).BuildServiceProvider();
        var log = new FakeLogger();
        var tool = (AIFunction)GrifTools.AIFunctions(services.GetRequiredService<IServiceScopeFactory>(), ToolCaller.Anonymous, GrifTools.Chat, log)
            .Single(t => t.Name == "get_current_time");

        await tool.InvokeAsync(new AIFunctionArguments());

        Assert.That(log.Collector.GetSnapshot().Select(r => r.GetStructuredStateValue("Outcome")), Is.EqualTo(Ok));
    }

    [TestCase(null, "ok")]
    [TestCase(typeof(ToolException), "refused")]
    [TestCase(typeof(InvalidOperationException), "failed")]
    // A tool's own timeout (an HTTP client's), the reply still under way: a failure, its message left out as any other's
    [TestCase(typeof(TaskCanceledException), "failed")]
    public async Task StreamAsync_LogsEachToolCall_ItsNameOutcomeAndTime_AndTagsItsSpan(Type? thrown, string outcome)
    {
        using var spans = new Spans();
        var model = new FakeModel([Call("get_current_time")], [Text("Done.")]);
        TimeProvider clock = thrown is null ? _time : new ThrowingClock((Exception)Activator.CreateInstance(thrown, "The clock broke")!);
        var chat = Chat(model, clock: clock);

        var trace = await AskUnderAHostsSpan(chat, "What time is it?");

        var logged = _services.GetFakeLogCollector().GetSnapshot().Where(r => r.Category == typeof(GrifTools).FullName).ToList();
        var tool = spans.Of(trace).Single(a => Operation(a) == "execute_tool");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logged, Has.Count.EqualTo(1));
            Assert.That(logged[0].Level, Is.EqualTo(LogLevel.Information));
            Assert.That(logged[0].GetStructuredStateValue("Tool"), Is.EqualTo("get_current_time"));
            Assert.That(logged[0].GetStructuredStateValue("Outcome"), Is.EqualTo(outcome));
            Assert.That(double.Parse(logged[0].GetStructuredStateValue("ElapsedMs")!, CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(0));
            Assert.That(logged[0].Message, Does.Not.Contain("The clock broke"), "a failure's details are in the function invocation's error log");
            Assert.That(tool.GetTagItem(GrifTools.OutcomeTag), Is.EqualTo(outcome));
            Assert.That(tool.Status == ActivityStatusCode.Error, Is.EqualTo(outcome == "failed"), "a refusal is the tool's answer, not an error");
            Assert.That(tool.StatusDescription ?? "", Does.Not.Contain("The clock broke"));
            // The failure's own type, which error.type (the redacted failure's) no longer tells
            Assert.That(tool.GetTagItem(GrifTools.ErrorTypeTag), Is.EqualTo(outcome == "failed" ? thrown!.FullName : null));
        }
    }

    [Test]
    public async Task StreamAsync_TheAskerStoppingWaitingDuringATool_IsCanceled_NotAFailure()
    {
        using var spans = new Spans();
        using var stop = new CancellationTokenSource();
        var model = new FakeModel([Call("get_current_time")], [Text("Done.")]);
        var chat = Chat(model, clock: new StoppingClock(stop));
        ActivityTraceId trace;

        using (var host = Spans.Test.StartActivity("host call")!)
        {
            trace = host.TraceId;
            using var reply = await chat.StartAsync([], "What time is it?", ToolCaller.Anonymous, ChatHost.Site, Asker);
            Assert.That(async () => await All(reply.StreamAsync(stop.Token)), Throws.InstanceOf<OperationCanceledException>());
        }

        var logged = _services.GetFakeLogCollector().GetSnapshot().Where(r => r.Category == typeof(GrifTools).FullName).ToList();
        var tool = spans.Of(trace).Single(a => Operation(a) == "execute_tool");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logged.Select(r => r.GetStructuredStateValue("Outcome")), Is.EqualTo(Canceled));
            Assert.That(tool.GetTagItem(GrifTools.OutcomeTag), Is.EqualTo("canceled"));
            Assert.That(tool.GetTagItem(GrifTools.ErrorTypeTag), Is.Null);
        }
    }

    [Test]
    public void ChatLog_IsTheChatsOwn_NotTheTools()
        => Assert.That(typeof(ChatLog).Namespace, Is.EqualTo(typeof(ChatModel).Namespace));

    private static string? Operation(Activity activity) => activity.GetTagItem("gen_ai.operation.name") as string;

    private static ChatResponseUpdate Call(string tool) => new(ChatRole.Assistant, [new FunctionCallContent("call-1", tool)]);

    private static ChatResponseUpdate Usage(int input, int output)
        => new(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = input, OutputTokenCount = output, TotalTokenCount = input + output })]);

    /// <summary>A clock that throws <paramref name="error"/>: a tool that refuses (a <see cref="ToolException"/>) or fails.</summary>
    private sealed class ThrowingClock(Exception error) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw error;
    }

    /// <summary>A clock read as the asker stops waiting (<paramref name="stop"/>): the tool is canceled as it runs.</summary>
    private sealed class StoppingClock(CancellationTokenSource stop) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            stop.Cancel();
            stop.Token.ThrowIfCancellationRequested();
            return default;
        }
    }

    /// <summary>The spans the chat (<see cref="ChatModel.SourceName"/>) and the test's host (<see cref="Test"/>) end while it listens.</summary>
    private sealed class Spans : IDisposable
    {
        private const string TestName = "GrifballWebApp.Application.Test.ChatHost";

        public static readonly ActivitySource Test = new(TestName);

        private readonly ConcurrentQueue<Activity> _ended = new();
        private readonly ActivityListener _listener;

        /// <param name="chat">Whether the chat is traced too, else only the host.</param>
        public Spans(bool chat = true)
        {
            _listener = new ActivityListener
            {
                // By name: the static source may be made only as the listener is added, and so be missed
                ShouldListenTo = source => source.Name == TestName || (chat && source.Name == ChatModel.SourceName),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = _ended.Enqueue,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>The spans of one trace: other tests' are in others.</summary>
        public List<Activity> Of(ActivityTraceId trace) => _ended.Where(a => a.TraceId == trace).ToList();

        public void Dispose() => _listener.Dispose();
    }

    /// <summary>What the chat's meter (<see cref="ChatModel.SourceName"/>) records while it listens.</summary>
    private sealed class Measurements : IDisposable
    {
        private readonly ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object?> Tags)> _recorded = new();
        private readonly MeterListener _listener = new();

        public Measurements()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ChatModel.SourceName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.Start();
        }

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            => _recorded.Enqueue((instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));

        /// <summary>The measurements of the calls to <paramref name="model"/>: other tests' are others'.</summary>
        public List<(string Instrument, double Value, Dictionary<string, object?> Tags)> Of(string model)
            => _recorded.Where(m => m.Tags.TryGetValue("gen_ai.request.model", out var asked) && Equals(asked, model)).ToList();

        public void Dispose() => _listener.Dispose();
    }

    private static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    /// <summary>
    /// A model that streams each reply it is given in turn, one per request, and records each request; with <see cref="Hang"/>,
    /// never answers until it is canceled; with <see cref="Blocked"/>, waits for it whatever is canceled.
    /// </summary>
    private sealed class FakeModel(params ChatResponseUpdate[][] replies) : IChatClient
    {
        private readonly Queue<ChatResponseUpdate[]> _replies = new(replies);

        public List<List<ChatMessage>> Requests { get; } = [];

        public List<ChatOptions?> Options { get; } = [];

        public bool Hang { get; init; }

        /// <summary>Streams its reply, then waits until canceled: a reply cut short before the provider says what it used.</summary>
        public bool ThenHang { get; init; }

        /// <summary>The model it says it is (its <see cref="ChatClientMetadata"/>): its own, so its telemetry is told from other tests'.</summary>
        public string ModelId { get; } = $"fake-{Guid.NewGuid():N}";

        public TaskCompletionSource? Blocked { get; init; }

        /// <summary>Waits until canceled, then ends the stream quietly, as a provider may.</summary>
        public bool Quiet { get; init; }

        /// <summary>Set when the model is first asked.</summary>
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The chat only streams");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            Options.Add(options);
            Asked.TrySetResult();
            if (Hang)
                await Task.Delay(Timeout.Infinite, cancellationToken);
            if (Quiet)
            {
                await Task.WhenAny(Task.Delay(Timeout.Infinite, cancellationToken));
                yield break;
            }
            if (Blocked is not null)
                await Blocked.Task;
            foreach (var update in _replies.Dequeue())
                yield return update;
            if (ThenHang)
                await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("fake", null, ModelId) : null;

        public void Dispose()
        {
            // Nothing held
        }
    }
}
