using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Application.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ChatButtonsUnitTests
{
    private sealed class Bot : ISlashCommandCatalog
    {
        public IReadOnlyList<SlashCommandDto> Commands() => [new("/help", "Help")];
    }

    [TestCase(ChatButtonKind.SetGamertag, null, "[button:set_gamertag]")]
    [TestCase(ChatButtonKind.SignupDetails, 12, "[button:signup_details:12]")]
    [TestCase(ChatButtonKind.Signup, 3, "[button:signup:3]")]
    [TestCase(ChatButtonKind.JoinQueue, null, "[button:join_queue]")]
    public void Token_IsFoundAgainByIn(ChatButtonKind kind, int? season, string token)
    {
        var button = new ChatButton(kind, season);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatButtons.Token(button), Is.EqualTo(token));
            Assert.That(ChatButtons.In($"Here: {token}"), Is.EqualTo(new[] { button }));
        }
    }

    [Test]
    public void In_IgnoresWhatIsNoButton_TakesEachOnce_AndAtMostFive()
    {
        var text = "[button:nope] [button:signup] [button:set_gamertag:4] [button:join_queue] [button:join_queue] [button:set_gamertag] "
            + "[button:signup:1] [button:signup:2] [button:signup:3] [button:signup:4]";

        var found = ChatButtons.In(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Has.Count.EqualTo(ChatButtons.MaxButtons));
            Assert.That(found[0], Is.EqualTo(new ChatButton(ChatButtonKind.JoinQueue, null)));
        }
    }

    [TestCase("Tap [button:join_queue] now.", "Tap now.")]
    [TestCase("Tap it.\n[button:join_queue]", "Tap it.")]
    [TestCase("Tap [button:join_queue].", "Tap.")]
    [TestCase("[button:join_queue] Tap it.", "Tap it.")]
    [TestCase("Tap [button:join_queue]\nnow.", "Tap\nnow.")]
    [TestCase("Tap it [button:join_queue]", "Tap it")]
    [TestCase("Not [button:nope] a button", "Not [button:nope] a button")]
    [TestCase("No token", "No token")]
    public void Without_LeavesTheTokensOut_AndTheRestAsItWas(string text, string expected)
        => Assert.That(ChatButtons.Without(text), Is.EqualTo(expected));

    [Test]
    public void OfferButton_GivesTheToken()
    {
        var tools = new ButtonTools(new Bot());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.OfferButton(ChatButtonKind.Signup, 5), Is.EqualTo("[button:signup:5]"));
            Assert.That(tools.OfferButton(ChatButtonKind.JoinQueue), Is.EqualTo("[button:join_queue]"));
        }
    }

    [Test]
    public void OfferButton_RefusesAMissingOrNeedlessSeason_AndAHostWithoutTheBot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ToolException>(() => new ButtonTools(new Bot()).OfferButton(ChatButtonKind.Signup));
            Assert.Throws<ToolException>(() => new ButtonTools(new Bot()).OfferButton(ChatButtonKind.JoinQueue, 5));
            Assert.Throws<ToolException>(() => new ButtonTools(new NoSlashCommands()).OfferButton(ChatButtonKind.JoinQueue));
        }
    }
}
