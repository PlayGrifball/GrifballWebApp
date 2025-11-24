using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

[TestFixture]
public class StringExtensionsTests
{
    [Test]
    public void RemoveXUIDWrapper_Should_RemoveXuidPrefix_When_StringHasXuidWrapper()
    {
        // Arrange
        const string input = "xuid(12345)";

        // Act
        var result = input.RemoveXUIDWrapper();

        // Assert
        Assert.That(result, Is.EqualTo("12345"));
    }

    [Test]
    public void RemoveXUIDWrapper_Should_ReturnUnchanged_When_StringHasNoXuidWrapper()
    {
        // Arrange
        const string input = "12345";

        // Act
        var result = input.RemoveXUIDWrapper();

        // Assert
        Assert.That(result, Is.EqualTo("12345"));
    }

    [Test]
    public void AddXUIDWrapper_Should_AddXuidWrapper_When_StringProvided()
    {
        // Arrange
        const string input = "12345";

        // Act
        var result = input.AddXUIDWrapper();

        // Assert
        Assert.That(result, Is.EqualTo("xuid(12345)"));
    }

    [Test]
    public void LinkMarkdown_Should_CreateMarkdownLink_When_UrlProvided()
    {
        // Arrange
        const string display = "Click Here";
        const string url = "https://example.com";

        // Act
        var result = display.LinkMarkdown(url);

        // Assert
        Assert.That(result, Is.EqualTo("[Click Here](https://example.com)"));
    }

    [Test]
    public void LinkMarkdown_Should_ReturnDisplay_When_UrlIsNull()
    {
        // Arrange
        const string display = "Click Here";
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        string? url = null;
#pragma warning restore CS8625

        // Act
        var result = display.LinkMarkdown(url!);

        // Assert
        Assert.That(result, Is.EqualTo("Click Here"));
    }

    [Test]
    public void ToDisplayName_Should_ReturnGamertag_When_UserHasXboxUser()
    {
        // Arrange
        var xboxUser = new XboxUser { Gamertag = "TestGamer", XboxUserID = 12345 };
        var user = new User { UserName = "test", DisplayName = "Display" };
        user.XboxUser = xboxUser;

        // Act
        var result = user.ToDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo("TestGamer"));
    }

    [Test]
    public void ToDisplayName_Should_ReturnDiscordUsername_When_UserHasNoXboxUserButHasDiscordUser()
    {
        // Arrange
        var discordUser = new DiscordUser { DiscordUserID = 12345, DiscordUsername = "DiscordUser" };
        var user = new User { UserName = "test", DisplayName = "Display" };
        user.DiscordUser = discordUser;

        // Act
        var result = user.ToDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo("DiscordUser"));
    }

    [Test]
    public void ToDisplayName_Should_ReturnDisplayName_When_UserHasNoXboxOrDiscordUser()
    {
        // Arrange
        var user = new User { UserName = "test", DisplayName = "Display" };

        // Act
        var result = user.ToDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo("Display"));
    }

    [Test]
    public void ToDisplayName_Should_PreferGamertag_When_UserHasBothXboxAndDiscordUser()
    {
        // Arrange
        var xboxUser = new XboxUser { Gamertag = "TestGamer", XboxUserID = 12345 };
        var discordUser = new DiscordUser { DiscordUserID = 67890, DiscordUsername = "DiscordUser" };
        var user = new User { UserName = "test", DisplayName = "Display" };
        user.XboxUser = xboxUser;
        user.DiscordUser = discordUser;

        // Act
        var result = user.ToDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo("TestGamer"));
    }
}
