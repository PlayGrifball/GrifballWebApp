using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>The chat model a user chose (<see cref="ChatChoice"/>): on the site and in a Discord DM, until they choose another.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ChatChoiceAggregateTests
{
    [Test]
    public void Make_KeepsTheUsersModel_AndChooseChangesIt()
    {
        var choice = ChatChoice.Make(7, "claude-haiku");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((choice.UserID, choice.Model), Is.EqualTo((7, "claude-haiku")));
            choice.Choose("claude-opus");
            Assert.That(choice.Model, Is.EqualTo("claude-opus"));
        }
    }

    [Test]
    public void ABlankOrTooLongModel_IsRefused()
    {
        var choice = ChatChoice.Make(7, "claude-haiku");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => ChatChoice.Make(7, " "), Throws.ArgumentException);
            Assert.That(() => choice.Choose(""), Throws.ArgumentException);
            Assert.That(() => choice.Choose(new string('m', ChatChoice.ModelMaxLength + 1)), Throws.ArgumentException);
            Assert.That(choice.Model, Is.EqualTo("claude-haiku"), "Refused: unchanged");
        }
    }
}
