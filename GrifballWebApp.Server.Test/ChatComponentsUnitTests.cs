using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ChatComponentsUnitTests
{
    private static readonly string[] ConfirmAndCancel = ["chatconfirm:a1:yes", "chatconfirm:a1:no"];
    private static readonly string[] JoinQueue = [DiscordButtonContants.JoinQueue];

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
            Assert.That(Ids(rows[0]), Is.EqualTo(ConfirmAndCancel));
            Assert.That(Ids(rows[1]), Is.EqualTo(JoinQueue));
        }
    }

    private static readonly string[] OfferedOnly = [DiscordButtonContants.JoinQueue];
    private static readonly string[] NewChangeAndOffered = ["chatconfirm:new:yes,chatconfirm:new:no", DiscordButtonContants.JoinQueue];

    /// <summary>A message's rows as Discord gives them: each a JSON object.</summary>
    private static IReadOnlyList<NetCord.IMessageComponent> Rows(params object[] rows) => GatewayMessages.Build(1, direct: true, components: rows).Components;

    /// <summary>A row of the bot's buttons, by their custom IDs (comma-separated).</summary>
    private static object Buttons(string ids) => new { type = 1, components = ids.Split(',').Select(id => new { type = 2, style = 1, label = id, custom_id = id }).ToArray() };

    /// <summary>A row with only a link button: none of the bot's own.</summary>
    private static readonly object LinkRow = new { type = 1, components = new[] { new { type = 2, style = 5, label = "Site", url = "https://grif.test" } } };

    [Test]
    public void WithoutConfirm_TakesOffAnsweredConfirmRows_KeepsAStillWaitingChanges_AndDropsRowsLeftEmpty()
    {
        var rows = Rows(Buttons("chatconfirm:old:yes,chatconfirm:old:no"), Buttons("chatconfirm:new:yes,chatconfirm:new:no"), LinkRow, Buttons(DiscordButtonContants.JoinQueue));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatComponents.WithoutConfirm(rows).Select(row => string.Join(",", Ids(row))), Is.EqualTo(OfferedOnly));
            Assert.That(ChatComponents.WithoutConfirm(rows, keep: "new").Select(row => string.Join(",", Ids(row))), Is.EqualTo(NewChangeAndOffered));
            Assert.That(ChatComponents.HasConfirm(rows, keep: "new"), Is.True, "the old change's");
            Assert.That(ChatComponents.HasConfirm(Rows(Buttons("chatconfirm:new:yes,chatconfirm:new:no")), keep: "new"), Is.False);
            Assert.That(ChatComponents.HasConfirm(Rows(LinkRow, Buttons(DiscordButtonContants.JoinQueue))), Is.False);
        }
    }

    [TestCase(ChatButtonKind.SetGamertag, null, "set_gamertag")]
    [TestCase(ChatButtonKind.SignupDetails, 7, "signupdetails:7")]
    [TestCase(ChatButtonKind.Signup, 7, "signup:7")]
    [TestCase(ChatButtonKind.JoinQueue, null, "join_queue")]
    public void Describe_OpensTheBotsOwnFlow(ChatButtonKind kind, int? season, string customId)
        => Assert.That(ChatComponents.Describe(new ChatButton(kind, season)).CustomId, Is.EqualTo(customId));
}
