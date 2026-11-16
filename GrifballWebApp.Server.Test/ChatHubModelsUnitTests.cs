using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server.Chat;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>The site's chat hub's models (<see cref="ChatHub.Models"/>, <see cref="ChatHub.Choose"/>), the user's choice kept in the database.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ChatHubModelsUnitTests : SqliteGrifballContextFixture
{
    private static readonly ChatModelOption Free = new("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b");
    private static readonly ChatModelOption Other = new("qwen", "qwen3:4b", ChatProvider.Ollama, "qwen3:4b");
    private static readonly string[] BothIds = ["ollama", "qwen"];

    private readonly ChatLimits _limits = new();
    private ServiceProvider _services = null!;

    [TearDown]
    public async Task TearDown()
    {
        _limits.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private ChatHub Hub(HubUserState user, IChatClient? model = null)
    {
        var clients = Substitute.For<IChatClientFactory>();
        clients.Available.Returns(true);
        clients.Models.Returns([Free, Other]);
        if (model is not null)
            clients.Create(Arg.Any<ChatModelOption>()).Returns(model);
        _services ??= new ServiceCollection().AddSingleton<IApplicationDbContext>(Db).AddSingleton<IChatWaiting>(new MemoryChatWaiting(TimeProvider.System)).BuildServiceProvider();
        var scopes = _services.GetRequiredService<IServiceScopeFactory>();
        var now = new HubUser();
        now.Set(user);
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionAborted.Returns(CancellationToken.None);
        return new ChatHub(new GrifChat(new ChatModel(clients, NullLoggerFactory.Instance), scopes, _limits, new ChatAccounts(scopes, new FakeTimeProvider(), NullLogger<ChatAccounts>.Instance)), now,
            Substitute.For<IAmbientCurrentUser>(), new RecordingLogger<ChatHub>()) { Context = context };
    }

    [Test]
    public async Task Choose_KeepsTheUsersChoice_AndGivesTheirModelsWithIt()
    {
        var models = await Hub(new HubUserState(7, ["Player"])).Choose("qwen");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(models.Selected, Is.EqualTo("qwen"));
            Assert.That(models.Models.Select(m => m.Id), Is.EqualTo(BothIds));
            Assert.That(await Db.ChatChoices.AsNoTracking().Select(c => c.Model).SingleAsync(), Is.EqualTo("qwen"));
            Assert.That((await Hub(new HubUserState(7, ["Player"])).Models()).Selected, Is.EqualTo("qwen"), "Kept for the next connection");
        }
    }

    [Test]
    public async Task Waiting_NoChangeAskedFor_IsNone_AndAnAnswerToIt_IsRefused()
    {
        var hub = Hub(new HubUserState(7, ["Commissioner"]));

        var waiting = await hub.Waiting();
        var reply = await hub.Confirm(new ChatConfirmDto { ActionId = "nothing", Approved = true, History = [] }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(waiting, Is.Null);
            Assert.That(async () => await reply.ReadAllAsync().ToListAsync(),
                Throws.InstanceOf<HubException>().With.Message.EqualTo(GrifChat.NothingWaiting));
        }
    }

    private static readonly ChatConfirmDto?[] Unreadable =
    [
        null,
        new() { ActionId = null!, Approved = true, History = [] },
        new() { ActionId = "a", Approved = true, History = null! },
        new() { ActionId = "a", Approved = true, History = [null!] },
        new() { ActionId = "a", Approved = true, History = [new ChatTurnDto { FromBot = false, Text = null! }] },
    ];

    [TestCaseSource(nameof(Unreadable))]
    public async Task Confirm_AnUnreadableAnswer_IsRefused(ChatConfirmDto? answer)
    {
        var reply = await Hub(new HubUserState(7, ["Commissioner"])).Confirm(answer!, CancellationToken.None);

        Assert.That(async () => await reply.ReadAllAsync().ToListAsync(), Throws.InstanceOf<HubException>().With.Message.EqualTo(GrifChat.Unreadable));
    }

    [Test]
    public async Task Waiting_TheChangeWaitingForTheUser_ItsIDAndWhatItDoes()
    {
        var hub = Hub(new HubUserState(7, ["Commissioner"]));
        var scopes = _services!.GetRequiredService<IServiceScopeFactory>();
        await new ChatAccounts(scopes, new FakeTimeProvider(), NullLogger<ChatAccounts>.Instance).SaveWaitingAsync(ChatHost.Site,
            ChatLimits.Asker(new GrifballWebApp.Application.Tools.ToolCaller(7, []), ""), new ChatAction("a1", "ollama", "forfeit_season_match(seasonMatchID: 12)", []));

        var waiting = await hub.Waiting();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(waiting?.Id, Is.EqualTo("a1"));
            Assert.That(waiting?.Description, Is.EqualTo("forfeit_season_match(seasonMatchID: 12)"));
        }
    }

    [Test]
    public async Task Forget_DropsTheChangeWaitingForTheUser()
    {
        var hub = Hub(new HubUserState(7, ["Commissioner"]));
        await new ChatAccounts(_services!.GetRequiredService<IServiceScopeFactory>(), new FakeTimeProvider(), NullLogger<ChatAccounts>.Instance).SaveWaitingAsync(ChatHost.Site,
            ChatLimits.Asker(new GrifballWebApp.Application.Tools.ToolCaller(7, []), ""), new ChatAction("a1", "ollama", "run_job()", []));

        await hub.Forget();

        Assert.That(await hub.Waiting(), Is.Null);
    }

    [Test]
    public async Task SetAutoApprove_KeepsStaffsSetting_AndGivesTheirModelsWithIt()
    {
        var models = await Hub(new HubUserState(7, ["Commissioner"])).SetAutoApprove(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(models.AutoApprove, Is.True);
            Assert.That((await Hub(new HubUserState(7, ["Commissioner"])).Models()).AutoApprove, Is.True, "Kept for the next connection");
            Assert.That((await Hub(new HubUserState(8, ["Player"])).Models()).AutoApprove, Is.Null, "A player has no change to approve");
        }
    }

    [Test]
    public async Task Ask_WithNoChoiceOfItsOwn_TheirSettingDecides_TheConversationsChoiceOverIt()
    {
        await Hub(new HubUserState(7, ["Commissioner"])).SetAutoApprove(true);

        var saved = new ScriptedChatClient((TimeSpan.Zero, "Hi"));
        await (await Hub(new HubUserState(7, ["Commissioner"]), saved).Ask(new ChatRequestDto { Question = "Hi", History = [] }, CancellationToken.None))
            .ReadAllAsync().ToListAsync();
        var overridden = new ScriptedChatClient((TimeSpan.Zero, "Hi"));
        await (await Hub(new HubUserState(7, ["Commissioner"]), overridden)
            .Ask(new ChatRequestDto { Question = "Hi", History = [], AutoApprove = false }, CancellationToken.None)).ReadAllAsync().ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Options!.Instructions, Does.Contain(GrifChat.AutoApprovedLine));
            Assert.That(saved.Options.Tools, Has.None.InstanceOf<ApprovalRequiredAIFunction>());
            Assert.That(overridden.Options!.Instructions, Does.Contain(GrifChat.PrivateLine), "This conversation: changes wait");
            Assert.That(overridden.Options.Tools, Has.Some.InstanceOf<ApprovalRequiredAIFunction>());
        }
    }

    [Test]
    public void SetAutoApprove_ForSomeoneWithNoChangeToMake_IsRefused_WithWhy()
        => Assert.That(async () => await Hub(new HubUserState(8, ["Player"])).SetAutoApprove(true),
            Throws.InstanceOf<HubException>().With.Message.EqualTo(GrifChat.NoChangesToApprove));
}
