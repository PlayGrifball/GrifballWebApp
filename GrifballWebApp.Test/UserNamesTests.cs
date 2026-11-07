using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Historical;

namespace GrifballWebApp.Test;

[TestFixture]
public class UserNamesTests
{
    [Test]
    public void Pick_TakesTheGamertag_ThenDiscordName_ThenDisplayName_NeverTheLoginName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UserNames.Pick("Tag", "disc", "Display", "login"), Is.EqualTo("Tag"));
            Assert.That(UserNames.Pick(null, "disc", "Display", "login"), Is.EqualTo("disc"));
            Assert.That(UserNames.Pick(null, null, "Display", "login"), Is.EqualTo("Display"));
            Assert.That(UserNames.Pick(null, null, null, "login"), Is.Empty, "A login name is private");
            Assert.That(UserNames.Pick(null, null, null, null), Is.Empty);
        });
    }

    [Test]
    public void Pick_ShowsAnArchivePlaceholder_ByTheGamertagInItsName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UserNames.Pick(null, null, null, HistoricalKeys.DummyUserName("Old Tag")), Is.EqualTo("Old Tag"));
            Assert.That(UserNames.PlaceholderName(HistoricalKeys.DummyUserName("Old Tag")), Is.EqualTo("Old Tag"));
            Assert.That(UserNames.PlaceholderName("login"), Is.Null);
            Assert.That(UserNames.PlaceholderName(null), Is.Null);
        });
    }
}
