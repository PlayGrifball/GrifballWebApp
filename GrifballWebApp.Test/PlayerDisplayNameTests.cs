using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;

namespace GrifballWebApp.Test;

[TestFixture]
public class PlayerDisplayNameTests
{
    [Test]
    public void ToDisplayName_MatchedAndQueuedPlayer_DelegateToUser()
    {
        var withGt = User.Seed(displayName: "Disp", xboxUser: XboxUser.FromXbox(0, "GT"));
        var discordOnly = User.Seed(displayName: "Disp", discordUser: DiscordUser.FromDiscord(0, "disc"));
        var bare = User.Seed(displayName: "Disp");

        Assert.Multiple(() =>
        {
            Assert.That(new MatchedPlayer(withGt).ToDisplayName(), Is.EqualTo("GT"));
            Assert.That(new QueuedPlayer { User = discordOnly }.ToDisplayName(), Is.EqualTo("disc"));
            Assert.That(new QueuedPlayer { User = bare }.ToDisplayName(), Is.EqualTo("Disp"));
            Assert.That(new MatchedPlayer(User.Seed()).ToDisplayName(), Is.Null);
        });
    }
}
