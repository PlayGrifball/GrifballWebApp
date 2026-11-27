using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ChatComponentsUnitTests
{
    private static string[] Ids(ActionRowProperties row) => [.. row.OfType<ButtonProperties>().Select(b => b.CustomId)];

    [Test]
    public void For_NothingToShow_IsNull()
        => Assert.That(ChatComponents.For([], null), Is.Null);

    [Test]
    public void For_AWaitingChange_HasConfirmAndCancel_ThenTheOfferedButtons()
    {
        var waiting = new ChatAction("a1", "ollama", "forfeit_season_match()", []);

        var rows = ChatComponents.For([new ChatButton(ChatButtonKind.JoinQueue, null)], waiting)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(Ids(rows[0]), Is.EqualTo(new[] { "chatconfirm:a1:yes", "chatconfirm:a1:no" }));
            Assert.That(Ids(rows[1]), Is.EqualTo(new[] { DiscordButtonContants.JoinQueue }));
        }
    }

    [TestCase(ChatButtonKind.SetGamertag, null, "set_gamertag")]
    [TestCase(ChatButtonKind.SignupDetails, 7, "signupdetails:7")]
    [TestCase(ChatButtonKind.Signup, 7, "signup:7")]
    [TestCase(ChatButtonKind.JoinQueue, null, "join_queue")]
    public void Describe_OpensTheBotsOwnFlow(ChatButtonKind kind, int? season, string customId)
        => Assert.That(ChatComponents.Describe(new ChatButton(kind, season)).CustomId, Is.EqualTo(customId));
}
