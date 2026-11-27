using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Application.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ChatButtonsUnitTests
{
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
}
