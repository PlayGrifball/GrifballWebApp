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
/// instructions, the conversation it sends, the tools it gives and calls, and its time limit.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GrifChatUnitTests
{
    private static readonly string[] Answer = ["It is ", "noon."];

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private ServiceProvider _services = null!;

    [TearDown]
    public async Task TearDown()
    {
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private GrifChat Chat(FakeModel model)
    {
        _services = new ServiceCollection().AddSingleton<TimeProvider>(_time).BuildServiceProvider();
        var clients = Substitute.For<IChatClientFactory>();
        clients.Create().Returns(model);
        return new GrifChat(clients, _services.GetRequiredService<IServiceScopeFactory>());
    }

    private static async Task<List<string>> All(IAsyncEnumerable<string> reply)
    {
        var texts = new List<string>();
        await foreach (var text in reply)
            texts.Add(text);
        return texts;
    }

    private static List<ChatMessage> Asked(string question) => GrifChat.Conversation([], question);

    [Test]
    public async Task ReplyAsync_StreamsTheModelsReply_WithTheHostsInstructionsAndTheChatToolsTheCallerMayUse()
    {
        var model = new FakeModel([Text("It is "), Text("noon.")]);

        var reply = await All(Chat(model).ReplyAsync(Asked("What time is it?"), ToolCaller.Anonymous, ChatHost.Site));

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
    public async Task ReplyAsync_ACallerWithAPlayersRole_GetsThePullToolsToo()
    {
        var model = new FakeModel([Text("Hi")]);

        await All(Chat(model).ReplyAsync(Asked("Hi"), new ToolCaller(7, ["Player"]), ChatHost.Discord));

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
    public async Task ReplyAsync_OnTheSite_AStaffCallerGetsTheirStaffToolsToo(string role)
    {
        var model = new FakeModel([Text("Hi")]);

        await All(Chat(model).ReplyAsync(Asked("Hi"), new ToolCaller(7, [SiteRoles.Player, role]), ChatHost.Site));

        var expected = StaffToolsFor(SiteRoles.Player, role);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(expected, Is.Not.Empty);
            Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat.Concat(expected)));
        }
    }

    [Test]
    public async Task ReplyAsync_OnTheSite_APlayerGetsNoStaffTools()
    {
        var model = new FakeModel([Text("Hi")]);

        await All(Chat(model).ReplyAsync(Asked("Hi"), new ToolCaller(7, [SiteRoles.Player]), ChatHost.Site));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StaffToolsFor(SiteRoles.Player), Is.Empty);
            Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
        }
    }

    [Test]
    public async Task ReplyAsync_OnDiscord_StaffGetNoStaffTools()
    {
        var model = new FakeModel([Text("Hi")]);

        await All(Chat(model).ReplyAsync(Asked("Hi"), new ToolCaller(7, [SiteRoles.Commissioner, SiteRoles.Sysadmin]), ChatHost.Discord));

        Assert.That(model.Options.Single()!.Tools!.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
    }

    [Test]
    public async Task ReplyAsync_CallsTheToolTheModelAsksFor_AndSendsItTheResult_WhateverTheProvider()
    {
        // The factory's model calls no tools itself: GrifChat does it for any provider
        var model = new FakeModel(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_current_time")])],
            [Text("It is "), Text("noon.")]);

        var reply = await All(Chat(model).ReplyAsync(Asked("What time is it?"), ToolCaller.Anonymous, ChatHost.Site));

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
    public void ReplyAsync_ModelSlowerThanTheHostsTimeout_IsCanceled()
    {
        var model = new FakeModel { Hang = true };
        var host = ChatHost.Site with { Timeout = TimeSpan.FromMilliseconds(20) };

        Assert.That(async () => await All(Chat(model).ReplyAsync(Asked("Hi"), ToolCaller.Anonymous, host)), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Instructions_TheHostsFirstAndLastLines_AroundWhatEveryHostSays()
    {
        var discord = GrifChat.Instructions(ChatHost.Discord).Split('\n');
        var site = GrifChat.Instructions(ChatHost.Site).Split('\n');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord[0], Is.EqualTo("You are a Discord bot, and all messages must stay under the 2000-character limit."));
            Assert.That(discord[^1], Is.EqualTo("The bot also has slash commands; /help lists them."));
            Assert.That(site[0], Does.StartWith("You are the chat on the Grifball league's website"));
            Assert.That(site[1..^1], Is.EqualTo(discord[1..^1]));
            Assert.That(site, Has.Some.Contains("in UTC, as the tools give them"));
            Assert.That(string.Join('\n', site), Does.Not.Contain("Discord").And.Not.Contain("/help"));
        }
    }

    [Test]
    public void Hosts_DiscordAnswersInPublicAndIsNeverCutShort_TheSiteIsPrivateAndTimed()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatHost.Discord.InPublic, Is.True);
            Assert.That(ChatHost.Discord.Timeout, Is.EqualTo(Timeout.InfiniteTimeSpan));
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
    public void Conversation_OfARequest_TheBrowsersHistoryInItsRoles()
    {
        var request = new ChatRequestDto
        {
            Question = "And now?",
            History = [new ChatTurnDto { FromBot = false, Text = "Hi" }, new ChatTurnDto { FromBot = true, Text = "Hello" }],
        };

        var conversation = GrifChat.Conversation(request);

        Assert.That(conversation.Select(m => (m.Role, m.Text)), Is.EqualTo(new[] { (ChatRole.User, "Hi"), (ChatRole.Assistant, "Hello"), (ChatRole.User, "And now?") }));
    }

    [TestCase("", "Ask a question.")]
    [TestCase("   ", "Ask a question.")]
    [TestCase("Who won?", null)]
    public void Refusal_OfAQuestion(string question, string? refusal)
        => Assert.That(GrifChat.Refusal(new ChatRequestDto { Question = question, History = [] }), Is.EqualTo(refusal));

    [Test]
    public void Refusal_QuestionOverDiscordsLimit_IsRefused_AtTheLimitIsNot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifChat.Refusal(new ChatRequestDto { Question = new string('a', GrifChat.MaxQuestionLength + 1), History = [] }), Is.EqualTo("Ask in 2000 characters or fewer."));
            Assert.That(GrifChat.Refusal(new ChatRequestDto { Question = new string('a', GrifChat.MaxQuestionLength), History = [] }), Is.Null);
        }
    }

    private static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    /// <summary>
    /// A model that streams each reply it is given in turn, one per request, and records each request; or, with
    /// <see cref="Hang"/>, never answers until it is canceled.
    /// </summary>
    private sealed class FakeModel(params ChatResponseUpdate[][] replies) : IChatClient
    {
        private readonly Queue<ChatResponseUpdate[]> _replies = new(replies);

        public List<List<ChatMessage>> Requests { get; } = [];

        public List<ChatOptions?> Options { get; } = [];

        public bool Hang { get; init; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The chat only streams");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            Options.Add(options);
            if (Hang)
                await Task.Delay(Timeout.Infinite, cancellationToken);
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
