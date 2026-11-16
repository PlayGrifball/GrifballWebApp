using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// A change the model asks for waits for the person (<see cref="GrifChat.ConfirmAsync"/>): a tool that changes the league's data is
/// never run on the model's word alone. What waits is kept on the server, so only the person's yes or no comes back.
/// </summary>
public partial class GrifChatUnitTests
{
    private static readonly ToolCaller Commissioner = new(7, [SiteRoles.Commissioner, SiteRoles.Sysadmin]);

    private static ChatResponseUpdate Calls(string name, Dictionary<string, object?> arguments, string callId = "call-1")
        => new(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]);

    private static readonly Dictionary<string, object?> Forfeit = new() { ["seasonMatchID"] = 12, ["side"] = "home" };

    /// <summary>The model's requests' function results, by call ID.</summary>
    private static List<FunctionResultContent> Results(FakeModel model, int request)
        => model.Requests[request].SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();

    [Test]
    public void AIFunctions_ToolsThatChangeData_WaitForApproval_TheOthersDont()
    {
        var tools = GrifTools.AIFunctions(Substitute.For<IServiceScopeFactory>(), Commissioner, GrifTools.Every);
        var changing = GrifTools.ChangingData.SelectMany(GrifTools.Methods).Select(GrifTools.Name).ToHashSet();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Where(t => changing.Contains(t.Name)), Is.Not.Empty.And.All.InstanceOf<ApprovalRequiredAIFunction>());
            Assert.That(tools.Where(t => !changing.Contains(t.Name)), Is.Not.Empty.And.None.InstanceOf<ApprovalRequiredAIFunction>(),
                "Reads and Halo pulls run when the model asks");
        }
    }

    [Test]
    public async Task StreamAsync_AChangeTheModelAsksFor_WaitsForTheirConfirmation_AndSaysWhat()
    {
        var model = new FakeModel([Calls("forfeit_season_match", Forfeit)]);
        var chat = ChatWithModels(model);
        _clients.Models.Returns([Free]);

        List<string> texts;
        ChatAction? waiting;
        using (var reply = await chat.StartAsync([], "Forfeit match 12 for the home team", Commissioner, ChatHost.Site, Asker))
        {
            texts = await All(reply.StreamAsync());
            waiting = reply.Waiting;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Requests, Has.Count.EqualTo(1), "Not run: nothing went back to the model");
            Assert.That(waiting, Is.Not.Null);
            Assert.That(waiting!.Description, Does.Contain("forfeit_season_match").And.Contain("seasonMatchID: 12").And.Contain("side: home"));
            Assert.That(string.Concat(texts), Does.Contain(waiting.Description).And.Contain(GrifChat.ConfirmSite));
            Assert.That(await chat.WaitingAsync(Asker), Is.EqualTo(waiting), "Kept for their answer");
        }
    }

    [Test]
    public async Task ConfirmAsync_Yes_RunsTheChangeTheyWereShown_AndTheModelAnswers()
    {
        var model = new FakeModel([Calls("forfeit_season_match", Forfeit)], [Text("Done.")]);
        var chat = ChatWithModels(model);
        _clients.Models.Returns([Free]);
        ChatAction waiting;
        using (var reply = await chat.StartAsync([], "Forfeit match 12 for the home team", Commissioner, ChatHost.Site, Asker))
        {
            await All(reply.StreamAsync());
            waiting = reply.Waiting!;
        }

        using (var confirmed = await chat.ConfirmAsync([new ChatMessage(ChatRole.User, "Forfeit match 12 for the home team")], waiting.Id, true,
            Commissioner, ChatHost.Site, Asker))
            Assert.That(await All(confirmed.StreamAsync()), Does.Contain("Done."));

        var result = Results(model, 1).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CallId, Is.EqualTo("call-1"));
            Assert.That(result.Result?.ToString(), Does.Not.Contain("rejected"), "Run: its result went back to the model");
            Assert.That(await chat.WaitingAsync(Asker), Is.Null, "Answered: nothing waits");
        }
    }

    [Test]
    public async Task ConfirmAsync_No_RunsNothing_AndTellsTheModel()
    {
        var model = new FakeModel([Calls("forfeit_season_match", Forfeit)], [Text("Left as it was.")]);
        var chat = ChatWithModels(model);
        _clients.Models.Returns([Free]);
        ChatAction waiting;
        using (var reply = await chat.StartAsync([], "Forfeit match 12", Commissioner, ChatHost.Site, Asker))
        {
            await All(reply.StreamAsync());
            waiting = reply.Waiting!;
        }

        using (var refused = await chat.ConfirmAsync([], waiting.Id, false, Commissioner, ChatHost.Site, Asker))
            await All(refused.StreamAsync());

        Assert.That(Results(model, 1).Single().Result?.ToString(), Does.Contain("rejected"));
    }

    [Test]
    public async Task ConfirmAsync_AnActionThatIsntWaiting_IsRefused_NotRun()
    {
        var model = new FakeModel([Calls("forfeit_season_match", Forfeit)]);
        var chat = ChatWithModels(model);
        _clients.Models.Returns([Free]);
        using (var reply = await chat.StartAsync([], "Forfeit match 12", Commissioner, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await chat.ConfirmAsync([], "another", true, Commissioner, ChatHost.Site, Asker),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NothingWaiting));
            Assert.That(async () => await chat.ConfirmAsync([], (await chat.WaitingAsync(Asker))!.Id, true, Commissioner, ChatHost.Site, "user:2"),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NothingWaiting), "Another asker's is not theirs");
            Assert.That(GrifChat.IsRefusal(GrifChat.NothingWaiting), Is.True);
        }
    }

    [Test]
    public async Task StartAsync_ANewQuestion_DropsTheChangeWaiting()
    {
        var chat = ChatWithModels(new FakeModel([Calls("forfeit_season_match", Forfeit)], [Text("Season 3.")]));
        _clients.Models.Returns([Free]);
        using (var reply = await chat.StartAsync([], "Forfeit match 12", Commissioner, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        using (var other = await chat.StartAsync([], "Which season is on?", Commissioner, ChatHost.Site, Asker))
            await All(other.StreamAsync());

        Assert.That(await chat.WaitingAsync(Asker), Is.Null, "A later yes isn't for a change they moved on from");
    }

    [Test]
    public async Task WaitingAsync_Expires()
    {
        var chat = ChatWithModels(new FakeModel([Calls("forfeit_season_match", Forfeit)]));
        _clients.Models.Returns([Free]);
        using (var reply = await chat.StartAsync([], "Forfeit match 12", Commissioner, ChatHost.Site, Asker))
            await All(reply.StreamAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _services.GetRequiredService<IDistributedCache>().GetAsync(ChatAccounts.WaitingKey(Asker)), Is.Not.Null);
            Assert.That(ChatAccounts.WaitingFor, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }
    }

    [Test]
    public void Instructions_SayToolResultsAreData_NotInstructions_AndChangesWaitForThePerson()
    {
        var site = GrifChat.Instructions(ChatHost.Site);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(site, Does.Contain(GrifChat.UntrustedLine));
            Assert.That(GrifChat.Instructions(ChatHost.Discord), Does.Contain(GrifChat.UntrustedLine), "In public too: tools return what people wrote");
            Assert.That(site, Does.Contain(GrifChat.PrivateLine));
        }
    }
}
