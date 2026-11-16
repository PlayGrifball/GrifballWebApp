using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The chat's models (<see cref="GrifChat.Allowed"/>): which a caller may use where, the one their question goes to, the one they
/// chose, and a paid model's cost, added to their month and refused past its limit.
/// </summary>
public partial class GrifChatUnitTests
{
    private static readonly ChatModelOption Haiku = new("claude-haiku", "Claude Haiku", ChatProvider.Anthropic, "claude-haiku-5-5")
        { Roles = [SiteRoles.Sysadmin], Price = new(Input: 1m, Output: 5m, CacheWrite: 1.25m, CacheRead: 0.10m) };

    private static readonly ChatModelOption HaikuCapped = Haiku with { MaxOutputTokens = 8192 };

    private static readonly ChatModelOption Opus = new("claude-opus", "Claude Opus", ChatProvider.Anthropic, "claude-opus-5-5")
        { Roles = [SiteRoles.Sysadmin], Price = new(Input: 4m, Output: 20m, CacheWrite: 5m, CacheRead: 0.20m) };

    private static readonly ToolCaller Sysadmin = new(7, [SiteRoles.Sysadmin]);
    private static readonly ToolCaller Player = new(8, [SiteRoles.Player]);

    /// <summary>The answer's texts, the usage updates' empty ones among them (the hosts skip those).</summary>
    private static readonly string[] NoonWithUsage = ["It is ", "", "noon.", ""];
    private static readonly string[] FreeAnswer = ["Free"];
    private static readonly string[] AgainAnswer = ["Again"];
    private static readonly string[] OnlyFree = ["ollama"];

    private FakeApplicationDbContext _db = null!;
    private readonly FakeLogger<ChatAccounts> _accountsLog = new();

    [TearDown]
    public void DisposeDatabase() => _db?.Dispose();

    /// <summary>A chat over Haiku, Opus and the free model, in that order, each answered by <paramref name="model"/>, its accounts in a database.</summary>
    private GrifChat ChatWithModels(FakeModel model, ChatLimits? limits = null, bool refuseSaves = false)
    {
        _db = new FakeApplicationDbContext();
        _services = new ServiceCollection()
            .AddSingleton<TimeProvider>(_time)
            .AddScoped<IApplicationDbContext>(_ => refuseSaves ? _db.NewContextLike(new RefusingSaves(_ => true)) : _db.NewContextLike())
            .BuildServiceProvider();
        _clients.Available.Returns(true);
        _clients.Models.Returns([Haiku, Opus, Free]);
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(model);
        var scopes = _services.GetRequiredService<IServiceScopeFactory>();
        return new GrifChat(new ChatModel(_clients, NullLoggerFactory.Instance), scopes, limits ?? _limits, new ChatAccounts(scopes, _time, _accountsLog));
    }

    private static ChatResponseUpdate Used(long input, long output) => new(ChatRole.Assistant, [new UsageContent(new UsageDetails
    {
        InputTokenCount = input,
        OutputTokenCount = output,
    })]);

    [Test]
    public void Allowed_InPrivate_EachModelTheRolesAllow_InTheirOrder_InPublic_OnlyTheFirstForAnyone()
    {
        var chat = ChatWithModels(new FakeModel());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chat.Allowed(Sysadmin, ChatHost.Site), Is.EqualTo(new[] { Haiku, Opus, Free }));
            Assert.That(chat.Allowed(Sysadmin, ChatHost.DiscordDirect), Is.EqualTo(new[] { Haiku, Opus, Free }));
            Assert.That(chat.Allowed(Player, ChatHost.Site), Is.EqualTo(new[] { Free }));
            Assert.That(chat.Allowed(ToolCaller.Anonymous, ChatHost.Site), Is.EqualTo(new[] { Free }));
            Assert.That(chat.Allowed(Sysadmin, ChatHost.Discord), Is.EqualTo(new[] { Free }), "A channel's is the one anyone may use, whoever asks");
        }
    }

    [Test]
    public async Task ModelForAsync_TheDefaultIsTheFirstAllowed_TheirChoiceWhileTheyMayUseIt_AndAskedForOnlyWhenTheirs()
    {
        var chat = ChatWithModels(new FakeModel());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await chat.ModelForAsync(Sysadmin, ChatHost.Site), Is.EqualTo(Haiku), "A sysadmin's default: Haiku");
            Assert.That(await chat.ModelForAsync(Player, ChatHost.Site), Is.EqualTo(Free));
            Assert.That(await chat.ModelForAsync(Sysadmin, ChatHost.Site, "ollama"), Is.EqualTo(Free));
            Assert.That(async () => await chat.ModelForAsync(Player, ChatHost.Site, "claude-haiku"),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours));
            Assert.That(async () => await chat.ModelForAsync(Sysadmin, ChatHost.Discord, "claude-haiku"),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours), "Not in a channel");
            Assert.That(async () => await chat.ModelForAsync(Sysadmin, ChatHost.Site, "gone"),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours));
        }

        await chat.ChooseAsync(Sysadmin, "claude-opus");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await chat.ModelForAsync(Sysadmin, ChatHost.Site), Is.EqualTo(Opus));
            Assert.That(await chat.ModelForAsync(Sysadmin, ChatHost.DiscordDirect), Is.EqualTo(Opus), "Their choice in a DM too");
            Assert.That(await chat.ModelForAsync(Sysadmin, ChatHost.Discord), Is.EqualTo(Free), "Not in a channel");
            Assert.That(await chat.ModelForAsync(Sysadmin with { Roles = [] }, ChatHost.Site), Is.EqualTo(Free),
                "No longer a sysadmin: their choice is no longer theirs");
        }
    }

    [Test]
    public async Task ChooseAsync_KeepsOneChoiceAUser_AndRefusesAModelNotTheirs_OrNoAccount()
    {
        var chat = ChatWithModels(new FakeModel());

        await chat.ChooseAsync(Sysadmin, "claude-opus");
        await chat.ChooseAsync(Sysadmin, "ollama");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _db.ChatChoices.Select(c => new { c.UserID, c.Model }).ToListAsync(), Is.EqualTo(new[] { new { UserID = 7, Model = "ollama" } }));
            Assert.That(async () => await chat.ChooseAsync(Player, "claude-opus"), Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours));
            Assert.That(async () => await chat.ChooseAsync(ToolCaller.Anonymous, "ollama"), Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours));
        }
    }

    [Test]
    public async Task StartAsync_APaidReply_AddsWhatEveryModelCallCostToTheCallersMonth()
    {
        // Usage reported more than once (as a tool round's and the answer's are): all of it counts
        var chat = ChatWithModels(new FakeModel([Text("It is "), Used(input: 1000, output: 100), Text("noon."), Used(input: 2000, output: 200)]));

        using (var reply = await chat.StartAsync([], "What time is it?", Sysadmin, ChatHost.Site, Asker))
            Assert.That(await All(reply.StreamAsync()), Is.EqualTo(NoonWithUsage));

        var month = await _db.ChatMonths.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((month.UserID, month.Month), Is.EqualTo((7, new DateOnly(2026, 11, 1))));
            // 3000 input tokens at $1 a million, 300 output at $5: $0.0045
            Assert.That((month.CostMicros, month.InputTokens, month.OutputTokens, month.Replies), Is.EqualTo((4_500L, 3000L, 300L, 1)));
        }
    }

    [Test]
    public async Task StartAsync_AFreeReply_CountsNoSpend()
    {
        var chat = ChatWithModels(new FakeModel([Text("Hi"), Used(input: 1000, output: 100)]));

        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker, model: "ollama"))
            await All(reply.StreamAsync());

        Assert.That(await _db.ChatMonths.AnyAsync(), Is.False);
    }

    [Test]
    public async Task StartAsync_ThePaidModelsMonthlyLimitReached_IsRefused_TheFreeModelStillAnswers()
    {
        using var limits = new ChatLimits(maxReplies: 3, monthlyLimit: 0.01m);
        var chat = ChatWithModels(new FakeModel([Text("Hi"), Used(input: 0, output: 2000)], [Text("Free")]), limits);

        // $0.01: 2000 output tokens at $5 a million
        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await chat.StartAsync([], "Again", Sysadmin, ChatHost.Site, Asker),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(ChatLimits.MonthlyLimitReached));
            Assert.That(GrifChat.IsRefusal(ChatLimits.MonthlyLimitReached), Is.True, "Left out of the conversation the model is sent");
        }
        using var free = await chat.StartAsync([], "Again", Sysadmin, ChatHost.Site, Asker, model: "ollama");
        Assert.That(await All(free.StreamAsync()), Is.EqualTo(FreeAnswer));
    }

    [Test]
    public async Task StartAsync_NextMonth_ThePaidModelsAnswerAgain()
    {
        using var limits = new ChatLimits(maxReplies: 3, monthlyLimit: 0.01m);
        var chat = ChatWithModels(new FakeModel([Text("Hi"), Used(input: 0, output: 2000)], [Text("Again")]), limits);
        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        _time.Advance(TimeSpan.FromDays(31));

        using var next = await chat.StartAsync([], "Again", Sysadmin, ChatHost.Site, Asker);
        Assert.That(await All(next.StreamAsync()), Is.EqualTo(AgainAnswer));
    }

    [Test]
    public async Task ModelsAsync_TheCallersModels_TheSelectedOne_AndTheirSpend_WhenAnyIsPaid()
    {
        var chat = ChatWithModels(new FakeModel([Text("Hi"), Used(input: 1_000_000, output: 0)]));
        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        var sysadmin = await chat.ModelsAsync(Sysadmin);
        var player = await chat.ModelsAsync(Player);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sysadmin.Models.Select(m => (m.Id, m.Name)), Is.EqualTo(new[]
                { ("claude-haiku", "Claude Haiku"), ("claude-opus", "Claude Opus"), ("ollama", "llama3.2:3b") }));
            Assert.That((sysadmin.Selected, sysadmin.Spent, sysadmin.Limit), Is.EqualTo(("claude-haiku", (decimal?)1m, (decimal?)ChatLimits.DefaultMonthlyLimit)));
            Assert.That(player.Models.Select(m => m.Id), Is.EqualTo(OnlyFree));
            Assert.That((player.Selected, player.Spent, player.Limit), Is.EqualTo(("ollama", (decimal?)null, (decimal?)null)), "Nothing paid: no spend shown");
        }
    }

    [Test]
    public void StartAsync_APaidModel_IsForASignedInCaller()
    {
        var anonymousSysadmin = new ToolCaller(null, [SiteRoles.Sysadmin]);
        var chat = ChatWithModels(new FakeModel());

        Assert.That(async () => await chat.StartAsync([], "Hi", anonymousSysadmin, ChatHost.Site, Asker),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours));
    }

    [Test]
    public void ChatPrice_CostMicros_PricesPlainInput_CacheReadsAndWrites_AndOutput_RoundedUp()
    {
        var price = new ChatPrice(Input: 2m, Output: 10m, CacheWrite: 2.5m, CacheRead: 0.1m);
        var usage = new UsageDetails
        {
            // Anthropic's client counts the cache's reads and writes in the input
            InputTokenCount = 1_000 + 4_000 + 2_000,
            CachedInputTokenCount = 4_000,
            AdditionalCounts = new() { [ChatPrice.CacheWriteCount] = 2_000 },
            OutputTokenCount = 333,
        };

        using (Assert.EnterMultipleScope())
        {
            // 1000 x 2 + 4000 x 0.1 + 2000 x 2.5 + 333 x 10 micro-dollars
            Assert.That(price.CostMicros(usage), Is.EqualTo(2_000 + 400 + 5_000 + 3_330));
            Assert.That(new ChatPrice(0.01m, 0m, 0m, 0m).CostMicros(new UsageDetails { InputTokenCount = 1 }), Is.EqualTo(1), "A part of a micro-dollar: rounded up");
        }
    }

    [Test]
    public async Task ACallerWithNoModelTheyMayUse_IsUnavailable_AndSeesNone()
    {
        var chat = ChatWithModels(new FakeModel());
        _clients.Models.Returns([Haiku]);

        var models = await chat.ModelsAsync(Player);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await chat.ModelForAsync(Player, ChatHost.Site),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.Unavailable));
            Assert.That(models.Models, Is.Empty);
            Assert.That((models.Selected, models.Spent, models.Limit), Is.EqualTo(((string?)null, (decimal?)null, (decimal?)null)));
        }
    }

    [Test]
    public void ChatLimits_MonthlyLimit_NeverBelowNothing()
    {
        using var limits = new ChatLimits(monthlyLimit: -5m);
        using var unset = new ChatLimits();

        Assert.That((limits.MonthlyLimit, unset.MonthlyLimit), Is.EqualTo((0m, ChatLimits.DefaultMonthlyLimit)));
    }

    [Test]
    public void ChatPrice_CostMicros_OtherAdditionalCounts_AreNoCacheWrites()
    {
        var usage = new UsageDetails { InputTokenCount = 10, AdditionalCounts = new() { ["WebFetchRequests"] = 3 } };

        Assert.That(new ChatPrice(1m, 0m, 100m, 0m).CostMicros(usage), Is.EqualTo(10));
    }

    [Test]
    public async Task StreamAsync_AsksForTheModelsOutputLimit()
    {
        var model = new FakeModel([Text("Hi")]);
        var chat = ChatWithModels(model);
        _clients.Models.Returns([HaikuCapped, Free]);

        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        Assert.That(model.Options.Single()!.MaxOutputTokens, Is.EqualTo(8192), "Not the provider's own default (Anthropic's client: 1024)");
    }

    [Test]
    public async Task StreamAsync_APaidReplyStoppedBeforeItsUsage_IsChargedAnEstimate_NotNothing()
    {
        var chat = ChatWithModels(new FakeModel([Text(new string('a', 3000))]) { ThenHang = true });
        using var stop = new CancellationTokenSource();

        using (var reply = await chat.StartAsync([], new string('q', 1500), Sysadmin, ChatHost.Site, Asker))
        {
            var streaming = All(reply.StreamAsync(stop.Token));
            await stop.CancelAsync();
            Assert.That(async () => await streaming, Throws.InstanceOf<OperationCanceledException>());
        }

        var month = await _db.ChatMonths.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            // The model never said what it used: at least the question's tokens in, and the answer's so far out
            Assert.That(month.InputTokens, Is.GreaterThanOrEqualTo(1500 / ChatReply.CharactersPerToken));
            Assert.That(month.OutputTokens, Is.EqualTo(3000 / ChatReply.CharactersPerToken));
            Assert.That(month.CostMicros, Is.Positive);
        }
    }

    [Test]
    public async Task StreamAsync_ASpendThatFailsToSave_IsLogged_AndTheWholeAnswerStands()
    {
        var chat = ChatWithModels(new FakeModel([Text("Noon."), Used(input: 1000, output: 100)]), refuseSaves: true);

        List<string> answer;
        using (var reply = await chat.StartAsync([], "What time is it?", Sysadmin, ChatHost.Site, Asker))
            answer = await All(reply.StreamAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer, Does.Contain("Noon."));
            Assert.That(_accountsLog.Collector.GetSnapshot().Select(r => r.Level), Does.Contain(LogLevel.Error));
            Assert.That(async () => (await chat.StartAsync([], "Again", Sysadmin, ChatHost.Site, Asker)).Dispose(), Throws.Nothing,
                "Its slot freed all the same");
        }
    }

    [Test]
    public async Task StartAsync_AnAskerAlreadyAnswered_IsRefusedBeforeAnyDatabaseRead()
    {
        var chat = ChatWithModels(new FakeModel([Text("Hi")]));
        using var first = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker);
        // Any read now fails: the refusal must come first
        await _db.DisposeAsync();

        Assert.That(async () => await chat.StartAsync([], "Again", Sysadmin, ChatHost.Site, Asker),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(ChatLimits.Busy));
    }

    [Test]
    public async Task StreamAsync_APaidCallThatNeverStreamed_ChargesNothing()
    {
        // The provider failed or was stopped before its first update: it bills nothing
        var chat = ChatWithModels(new FakeModel { Hang = true });
        using var stop = new CancellationTokenSource();

        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
        {
            var streaming = All(reply.StreamAsync(stop.Token));
            await stop.CancelAsync();
            Assert.That(async () => await streaming, Throws.InstanceOf<OperationCanceledException>());
        }

        Assert.That(await _db.ChatMonths.AnyAsync(), Is.False);
    }

    [Test]
    public async Task StreamAsync_APaidReplyThatReachesTheMonthlyLimit_StopsThere()
    {
        using var limits = new ChatLimits(maxReplies: 3, monthlyLimit: 0.01m);
        // Its first call's usage ($0.0125: 2500 output tokens at $5 a million) reaches the limit: no next call's words
        var chat = ChatWithModels(new FakeModel([Text("First "), Used(input: 0, output: 2500), Text("second")]), limits);
        var texts = new List<string>();

        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
        {
            Assert.That(async () =>
            {
                await foreach (var text in reply.StreamAsync())
                    texts.Add(text);
            }, Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(ChatLimits.MonthlyLimitReached));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts, Does.Not.Contain("second"));
            Assert.That((await _db.ChatMonths.AsNoTracking().SingleAsync()).CostMicros, Is.GreaterThan(12_500),
                "The call it stopped had begun: charged an estimate too");
        }
    }

    [Test]
    public async Task StreamAsync_AFurtherCallTheMonthlyLimitStops_IsChargedAnEstimate_ForWhatItWasSentAndWrote()
    {
        using var limits = new ChatLimits(maxReplies: 3, monthlyLimit: 0.01m);
        // The first call's usage reaches the limit; the next call (already sent, and answering) is stopped at its first words
        var chat = ChatWithModels(new FakeModel([Text("First "), Used(input: 0, output: 2500), Text(new string('a', 300))]), limits);

        using (var reply = await chat.StartAsync([], new string('q', 1500), Sysadmin, ChatHost.Site, Asker))
        {
            Assert.That(async () => await All(reply.StreamAsync()),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(ChatLimits.MonthlyLimitReached));
        }

        var month = await _db.ChatMonths.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            // The first call's 2500 out, and the stopped call's 300 characters: 100 more
            Assert.That(month.OutputTokens, Is.EqualTo(2500 + 300 / ChatReply.CharactersPerToken));
            Assert.That(month.InputTokens, Is.GreaterThanOrEqualTo(1500 / ChatReply.CharactersPerToken), "What the stopped call was sent");
        }
    }

    [Test]
    public async Task StreamAsync_ACutShortCallsEstimate_CountsItsThinkingAndToolCalls_NotOnlyItsText()
    {
        var thinking = new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(new string('r', 3000))]);
        var chat = ChatWithModels(new FakeModel([thinking]) { ThenHang = true });
        using var stop = new CancellationTokenSource();

        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
        {
            var streaming = All(reply.StreamAsync(stop.Token));
            await stop.CancelAsync();
            Assert.That(async () => await streaming, Throws.InstanceOf<OperationCanceledException>());
        }

        Assert.That((await _db.ChatMonths.AsNoTracking().SingleAsync()).OutputTokens, Is.EqualTo(3000 / ChatReply.CharactersPerToken));
    }

    [Test]
    public void ChatModel_LetsAPaidModelsReplyCallTheModelAtMostMaxModelCallsTimes()
    {
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(new FakeModel());

        var client = new ChatModel(_clients, NullLoggerFactory.Instance).Create(Haiku);

        Assert.That(client.GetService<FunctionInvokingChatClient>()!.MaximumIterationsPerRequest, Is.EqualTo(ChatModel.MaxModelCalls));
    }

    [Test]
    public void ChatModel_AFreeModelsReply_KeepsTheLibrarysDefaultCalls()
    {
        _clients.Create(Arg.Any<ChatModelOption>()).Returns(new FakeModel());
        using var library = new FunctionInvokingChatClient(new FakeModel());

        var client = new ChatModel(_clients, NullLoggerFactory.Instance).Create(Free);

        Assert.That(client.GetService<FunctionInvokingChatClient>()!.MaximumIterationsPerRequest,
            Is.EqualTo(library.MaximumIterationsPerRequest).And.GreaterThan(ChatModel.MaxModelCalls), "A free reply's tool rounds cost nothing: not cut short");
    }

    [Test]
    public async Task StreamAsync_AUsageWithNoInputCount_KeepsTheLastCallsInput_ForTheEstimate()
    {
        var noInput = new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { OutputTokenCount = 0 })]);
        var chat = ChatWithModels(new FakeModel([noInput, Text(new string('a', 30))]) { ThenHang = true });
        using var stop = new CancellationTokenSource();

        using (var reply = await chat.StartAsync([], "Hi", Sysadmin, ChatHost.Site, Asker))
        {
            var streaming = All(reply.StreamAsync(stop.Token));
            await stop.CancelAsync();
            Assert.That(async () => await streaming, Throws.InstanceOf<OperationCanceledException>());
        }

        Assert.That((await _db.ChatMonths.AsNoTracking().SingleAsync()).OutputTokens, Is.EqualTo(30 / ChatReply.CharactersPerToken));
    }

    [Test]
    public void Written_CountsText_Thinking_AndAToolCallsNameAndArguments_AndNothingElse()
    {
        AIContent[] contents =
        [
            new TextContent("ab"),
            new TextReasoningContent("cde"),
            // 6 + 1 + 4 + 1: the tool's name, each argument's name and value (none for a null one)
            new FunctionCallContent("c1", "lookup", new Dictionary<string, object?> { ["a"] = "abcd", ["b"] = null }),
            new FunctionCallContent("c2", "now"),
            new ErrorContent("not the model's writing"),
        ];

        Assert.That(ChatReply.Written(contents), Is.EqualTo(2 + 3 + 12 + 3));
    }

    [Test]
    public void IsRefusal_KnowsTheProvidersLimit_SoItIsLeftOutOfTheConversation()
        => Assert.That(GrifChat.IsRefusal(GrifChat.ProviderLimitReached), Is.True);
}
