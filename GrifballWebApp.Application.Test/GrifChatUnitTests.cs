using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The chat the Discord bot and the site share, on a model that answers as it is told (<see cref="FakeModel"/>): its
/// instructions, the conversation it sends, the tools it gives and calls, its limits and its time limit.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GrifChatUnitTests
{
    private static readonly string[] Answer = ["It is ", "noon."];
    private const string Asker = "user:1";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly ChatLimits _limits = new(maxReplies: 3);
    private readonly IChatClientFactory _clients = Substitute.For<IChatClientFactory>();
    private ServiceProvider _services = null!;

    [TearDown]
    public async Task TearDown()
    {
        _limits.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private GrifChat Chat(FakeModel model)
    {
        _services = new ServiceCollection().AddSingleton<TimeProvider>(_time).BuildServiceProvider();
        _clients.Available.Returns(true);
        _clients.Create().Returns(model);
        return new GrifChat(_clients, _services.GetRequiredService<IServiceScopeFactory>(), _limits);
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
        using var reply = chat.Start([], question, caller, host, asker);
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

        await Ask(Chat(model), "Hi", new ToolCaller(7, [SiteRoles.Player, role]), ChatHost.Site);

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
            using var reply = chat.Start([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker);
            await All(reply.StreamAsync(stop.Token));
        }, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Start_NoModel_IsUnavailable()
    {
        var chat = Chat(new FakeModel());
        _clients.Available.Returns(false);

        Assert.That(() => chat.Start([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.Unavailable));
        _clients.DidNotReceive().Create();
    }

    [Test]
    public void Start_AQuestionOverTheLimit_IsRefusedOnEveryHost([Values] bool discord)
    {
        var chat = Chat(new FakeModel());

        Assert.That(() => chat.Start([], new string('a', GrifChat.MaxQuestionLength + 1), ToolCaller.Anonymous, discord ? ChatHost.DiscordDirect : ChatHost.Site, Asker),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo("Ask in 2000 characters or fewer."));
    }

    [Test]
    public void Start_TrimsTheHistoryAndCutsLongMessages_WhateverTheHost()
    {
        var history = Enumerable.Range(1, GrifChat.HistoryLimit + 5).Select(i => new ChatMessage(ChatRole.User, i == 30 ? new string('a', 5000) : $"m{i}"));

        using var reply = Chat(new FakeModel()).Start(history, "Next?", ToolCaller.Anonymous, ChatHost.Discord, Asker);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply.Conversation, Has.Count.EqualTo(GrifChat.HistoryLimit + 1));
            Assert.That(reply.Conversation[^2].Text, Has.Length.EqualTo(GrifChat.MaxHistoryMessageLength));
            Assert.That(reply.Conversation[^1].Text, Is.EqualTo("Next?"));
        }
    }

    [Test]
    public void Start_WhileTheAskersLastReplyIsUnderWay_IsBusy_UntilItIsDone()
    {
        var chat = Chat(new FakeModel([Text("Hi")]));
        var first = chat.Start([], "Hi", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:5");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => chat.Start([], "Again", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:5"),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(ChatLimits.Busy));
            Assert.That(() => chat.Start([], "Hi", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:6").Dispose(), Throws.Nothing, "another asker");
        }
        first.Dispose();
        Assert.That(() => chat.Start([], "Again", ToolCaller.Anonymous, ChatHost.DiscordDirect, "discord:5").Dispose(), Throws.Nothing);
    }

    [Test]
    public async Task StreamAsync_TheAskerStopping_FreesTheirSlotAtOnce_NotWhenTheReplyHasWoundDown()
    {
        var blocked = new TaskCompletionSource();
        var model = new FakeModel([Text("Late")]) { Blocked = blocked };
        var chat = Chat(model);
        using var stop = new CancellationTokenSource();
        using var first = chat.Start([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker);
        var streaming = All(first.StreamAsync(stop.Token));
        await model.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await stop.CancelAsync();

        // The model hasn't noticed: the first reply is still under way, yet the asker may ask again
        Assert.That(() => chat.Start([], "Again", ToolCaller.Anonymous, ChatHost.Site, Asker).Dispose(), Throws.Nothing);
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
        var reply = chat.Start([], "Hi", ToolCaller.Anonymous, ChatHost.Site, Asker);

        await All(reply.StreamAsync());

        Assert.That(() => chat.Start([], "Again", ToolCaller.Anonymous, ChatHost.Site, Asker).Dispose(), Throws.Nothing);
        reply.Dispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Available_WhenTheFactoryHasAModel(bool configured)
    {
        var clients = Substitute.For<IChatClientFactory>();
        clients.Available.Returns(configured);

        Assert.That(new GrifChat(clients, Substitute.For<IServiceScopeFactory>(), _limits).Available, Is.EqualTo(configured));
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
            Assert.That(ChatHost.DiscordDirect with { InPublic = true }, Is.EqualTo(ChatHost.Discord));
            Assert.That(ChatHost.Discord.Tools, Is.SameAs(GrifTools.Chat));
            Assert.That(ChatHost.DiscordDirect.Tools, Is.SameAs(GrifTools.PrivateChat));
            Assert.That(ChatHost.Site.Tools, Is.SameAs(GrifTools.PrivateChat));
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
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing held
        }
    }
}
