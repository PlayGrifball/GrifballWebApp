using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A Discord account as an aggregate (#351): it begins as Discord gives it, always with a username to show, and only
/// <see cref="DiscordUser.Rename"/> changes that username.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DiscordUserAggregateTests
{
    [Test]
    public void FromDiscord_keeps_the_ID_and_the_username()
    {
        var account = DiscordUser.FromDiscord(42, "alice");

        Assert.Multiple(() =>
        {
            Assert.That(account.DiscordUserID, Is.EqualTo(42));
            Assert.That(account.DiscordUsername, Is.EqualTo("alice"));
            Assert.That(account.User, Is.Null);
            Assert.That(account.SentMessages, Is.Empty);
            Assert.That(account.ReceivedMessages, Is.Empty);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void FromDiscord_without_a_username_goes_by_its_ID(string? username)
        => Assert.That(DiscordUser.FromDiscord(42, username).DiscordUsername, Is.EqualTo("42"));

    [Test]
    public void Rename_to_a_new_username_renames_it()
    {
        var account = DiscordUser.FromDiscord(42, "alice");

        Assert.Multiple(() =>
        {
            Assert.That(account.Rename("alice_2"), Is.EqualTo(DiscordRename.Renamed));
            Assert.That(account.DiscordUsername, Is.EqualTo("alice_2"));
        });
    }

    [Test]
    public void FromDiscord_and_Rename_trim_the_username()
    {
        var account = DiscordUser.FromDiscord(42, "  alice ");

        Assert.Multiple(() =>
        {
            Assert.That(account.DiscordUsername, Is.EqualTo("alice"));
            Assert.That(account.Rename(" alice  "), Is.EqualTo(DiscordRename.Unchanged));
            Assert.That(account.Rename(" bob "), Is.EqualTo(DiscordRename.Renamed));
            Assert.That(account.DiscordUsername, Is.EqualTo("bob"));
        });
    }

    [Test]
    public void Rename_to_the_same_username_changes_nothing()
    {
        var account = DiscordUser.FromDiscord(42, "alice");

        Assert.Multiple(() =>
        {
            Assert.That(account.Rename("alice"), Is.EqualTo(DiscordRename.Unchanged));
            Assert.That(account.DiscordUsername, Is.EqualTo("alice"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Rename_without_a_username_keeps_its_own(string? username)
    {
        var account = DiscordUser.FromDiscord(42, "alice");

        Assert.Multiple(() =>
        {
            Assert.That(account.Rename(username), Is.EqualTo(DiscordRename.NoUsername));
            Assert.That(account.DiscordUsername, Is.EqualTo("alice"));
        });
    }

    [Test]
    public void IsUsername_refuses_a_blank_one()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DiscordUser.IsUsername("alice"), Is.True);
            Assert.That(DiscordUser.IsUsername(null), Is.False);
            Assert.That(DiscordUser.IsUsername(""), Is.False);
            Assert.That(DiscordUser.IsUsername(" "), Is.False);
        });
    }

    [Test]
    public void Linking_a_user_sets_the_account_s_user()
    {
        var account = DiscordUser.FromDiscord(42, "alice");
        var user = User.Seed(userName: "alice");

        Assert.Multiple(() =>
        {
            Assert.That(user.LinkDiscord(account), Is.EqualTo(DiscordLinking.Linked));
            Assert.That(user.DiscordUser, Is.SameAs(account));
            Assert.That(user.DiscordUserID, Is.EqualTo(42));
        });
    }
}
