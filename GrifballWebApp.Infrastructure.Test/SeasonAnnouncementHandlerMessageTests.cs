using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Infrastructure.Events;
using NetCord.Rest;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>SeasonAnnouncementHandler.Message: pure rendering of a SeasonAnnouncement, no database, host or network.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SeasonAnnouncementHandlerMessageTests
{
    private static ButtonProperties[] Buttons(MessageProperties message) =>
        message.Components!.OfType<ActionRowProperties>().Single().Components.OfType<ButtonProperties>().ToArray();

    [Test]
    public void Message_SignupsClosed_HasOnlyTheGamertagButton()
    {
        var message = SeasonAnnouncementHandler.Message(new SeasonAnnouncement(7, "Season 7", "description", SignupsOpen: false));

        var buttons = Buttons(message);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo("Season 7"));
            Assert.That(buttons, Has.Length.EqualTo(1));
            Assert.That(buttons[0].CustomId, Is.EqualTo(DiscordButtonContants.SetGamertag));
        }
    }

    [Test]
    public void Message_SignupsOpen_AddsASignupButtonForTheSeason()
    {
        var message = SeasonAnnouncementHandler.Message(new SeasonAnnouncement(7, "Season 7", "description", SignupsOpen: true));

        var buttons = Buttons(message);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(buttons, Has.Length.EqualTo(2));
            Assert.That(buttons[1].CustomId, Is.EqualTo(DiscordButtonContants.SignupWithParams(7)));
        }
    }

    [Test]
    public void Message_EmbedsTheSeasonsNameAndDescription()
    {
        var message = SeasonAnnouncementHandler.Message(new SeasonAnnouncement(7, "Season 7", "the description", SignupsOpen: false));

        var embed = message.Embeds!.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Season 7"));
            Assert.That(embed.Description, Is.EqualTo("the description"));
        }
    }
}
