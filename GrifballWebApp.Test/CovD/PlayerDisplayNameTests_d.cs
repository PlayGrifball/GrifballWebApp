using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;

namespace GrifballWebApp.Test.CovD;

[TestFixture]
public class PlayerDisplayNameTests_d
{
    [Test]
    public void ToDisplayName_MatchedAndQueuedPlayer_DelegateToUser()
    {
        var withGt = new User { DisplayName = "Disp", XboxUser = new XboxUser { Gamertag = "GT" } };
        var discordOnly = new User { DisplayName = "Disp", DiscordUser = new DiscordUser { DiscordUsername = "disc" } };
        var bare = new User { DisplayName = "Disp" };

        Assert.Multiple(() =>
        {
            Assert.That(new MatchedPlayer { User = withGt }.ToDisplayName(), Is.EqualTo("GT"));
            Assert.That(new QueuedPlayer { User = discordOnly }.ToDisplayName(), Is.EqualTo("disc"));
            Assert.That(new QueuedPlayer { User = bare }.ToDisplayName(), Is.EqualTo("Disp"));
            Assert.That(new MatchedPlayer { User = new User() }.ToDisplayName(), Is.Null);
        });
    }
}
