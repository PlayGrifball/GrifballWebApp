using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Application.Extensions;

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
            Assert.That(QueuedPlayer.Seed(discordOnly, DateTime.UtcNow).ToDisplayName(), Is.EqualTo("disc"));
            Assert.That(QueuedPlayer.Seed(bare, DateTime.UtcNow).ToDisplayName(), Is.EqualTo("Disp"));
            Assert.That(new MatchedPlayer(User.Seed()).ToDisplayName(), Is.Null);
        });
    }
}
