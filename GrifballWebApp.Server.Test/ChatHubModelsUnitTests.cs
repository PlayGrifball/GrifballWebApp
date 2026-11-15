using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server.Chat;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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

    private ChatHub Hub(HubUserState user)
    {
        var clients = Substitute.For<IChatClientFactory>();
        clients.Available.Returns(true);
        clients.Models.Returns([Free, Other]);
        _services ??= new ServiceCollection().AddSingleton<IApplicationDbContext>(Db).BuildServiceProvider();
        var scopes = _services.GetRequiredService<IServiceScopeFactory>();
        var now = new HubUser();
        now.Set(user);
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionAborted.Returns(CancellationToken.None);
        return new ChatHub(new GrifChat(new ChatModel(clients, NullLoggerFactory.Instance), scopes, _limits, new ChatAccounts(scopes, new FakeTimeProvider())), now,
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
}
