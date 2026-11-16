using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// Changes approved automatically (<see cref="ChatHost.AutoApprove"/>): staff's choice, kept for them (<see cref="GrifChat.SetAutoApproveAsync"/>)
/// and the page's per conversation. With it, a change the model asks for runs at once, as a read does; without it (the default), it waits.
/// </summary>
public partial class GrifChatUnitTests
{
    [Test]
    public void AIFunctions_ApprovedAutomatically_NoneWait()
    {
        var tools = GrifTools.AIFunctions(Substitute.For<IServiceScopeFactory>(), Commissioner, GrifTools.Every, approveChanges: false);

        Assert.That(tools, Is.Not.Empty.And.None.InstanceOf<ApprovalRequiredAIFunction>());
    }

    [Test]
    public void MayChangeData_StaffWithAToolThatChangesData_NotAPlayer()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifTools.MayChangeData(Commissioner), Is.True);
            Assert.That(GrifTools.MayChangeData(Player), Is.False);
            Assert.That(GrifTools.MayChangeData(ToolCaller.Anonymous), Is.False);
        }
    }

    [Test]
    public async Task AutoApprove_IsOffUntilStaffTurnItOn_AndKeptWithTheirModel()
    {
        var chat = ChatWithModels(new FakeModel());

        var before = await chat.AutoApproveAsync(Sysadmin);
        var models = await chat.SetAutoApproveAsync(Sysadmin, true);
        await chat.ChooseAsync(Sysadmin, Opus.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.False);
            Assert.That((models.AutoApprove, models.Selected), Is.EqualTo(((bool?)true, Haiku.Id)), "Set with no model chosen: their default still");
            Assert.That(await chat.AutoApproveAsync(Sysadmin), Is.True, "Choosing a model leaves it");
            Assert.That((await chat.ModelsAsync(Sysadmin)).Selected, Is.EqualTo(Opus.Id));
            Assert.That((await chat.SetAutoApproveAsync(Sysadmin, false)).AutoApprove, Is.False);
        }
    }

    [Test]
    public async Task AutoApprove_ForSomeoneWithNoChangeToMake_IsNone_AndRefused()
    {
        var chat = ChatWithModels(new FakeModel());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await chat.ModelsAsync(Player)).AutoApprove, Is.Null, "Nothing to approve: no setting shown");
            Assert.That(await chat.AutoApproveAsync(Player), Is.False);
            Assert.That(await chat.AutoApproveAsync(ToolCaller.Anonymous), Is.False);
            Assert.That(async () => await chat.SetAutoApproveAsync(Player, true), Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours));
            Assert.That(async () => await chat.SetAutoApproveAsync(new ToolCaller(null, [Identity.SiteRoles.Sysadmin]), true),
                Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.NotYours), "Not signed in: nowhere to keep it");
        }
    }

    [Test]
    public async Task StreamAsync_ApprovedAutomatically_AChangeRunsAtOnce_NothingWaits()
    {
        var model = new FakeModel([Calls("forfeit_season_match", Forfeit)], [Text("Done.")]);
        var chat = ChatWithModels(model);
        _clients.Models.Returns([Free]);

        ChatAction? waiting;
        using (var reply = await chat.StartAsync([], "Forfeit match 12 for the home team", Commissioner, ChatHost.Site with { AutoApprove = true }, Asker))
        {
            Assert.That(await All(reply.StreamAsync()), Does.Contain("Done."));
            waiting = reply.Waiting;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(waiting, Is.Null);
            Assert.That(Results(model, 1).Single().CallId, Is.EqualTo("call-1"), "Run: its result went back to the model");
            Assert.That(await chat.WaitingAsync(ChatHost.Site, Asker), Is.Null);
        }
    }

    [Test]
    public void Instructions_ApprovedAutomatically_DontSayAChangeWaits()
    {
        var auto = GrifChat.Instructions(ChatHost.Site with { AutoApprove = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(auto, Does.Contain(GrifChat.AutoApprovedLine).And.Not.Contain(GrifChat.PrivateLine));
            Assert.That(GrifChat.Instructions(ChatHost.Site), Does.Contain(GrifChat.PrivateLine).And.Not.Contain(GrifChat.AutoApprovedLine));
            Assert.That(GrifChat.Instructions(ChatHost.Discord with { AutoApprove = true }), Does.Not.Contain(GrifChat.AutoApprovedLine), "In public: no changes at all");
        }
    }
}
