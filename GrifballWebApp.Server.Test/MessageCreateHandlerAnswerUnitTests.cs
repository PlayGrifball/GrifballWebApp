namespace GrifballWebApp.Server.Test;

/// <summary>A DM's answer to a change waiting for its author (<see cref="MessageCreateHandler.Answer"/>): a plain yes or no, nothing else.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MessageCreateHandlerAnswerUnitTests
{
    [TestCase("yes", true)]
    [TestCase(" Yes! ", true)]
    [TestCase("confirm", true)]
    [TestCase("no", false)]
    [TestCase("Cancel.", false)]
    [TestCase("yes, and also forfeit match 13", null)]
    [TestCase("who won?", null)]
    public void Answer_IsAPlainYesOrNo(string content, bool? answer)
        => Assert.That(MessageCreateHandler.Answer(content), Is.EqualTo(answer));
}
