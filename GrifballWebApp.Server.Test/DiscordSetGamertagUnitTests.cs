using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordSetGamertag's unit tests, on a real GrifballContext (temp-file SQLite, #402). The "linked elsewhere"
/// refusal substitutes UserManager, as the Identity controller tests already do; the account-creation path needs a
/// real one (<see cref="RealUsers"/>) so CreateAsync's save really links the new user to the Discord row the handler
/// goes on to read (discordUser.User), which only EF's own relationship fixup sets. TempResponse/ModifyTempResponse's
/// 5-second wait is zeroed assembly-wide (AssemblySetUp).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordSetGamertagUnitTests : SqliteGrifballContextFixture
{
    private static UserManager<User> Users() => Substitute.For<UserManager<User>>(
        Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);

    /// <summary>Identity's own EF store, over this test's SQLite context: CreateAsync, AddToRoleAsync, AddLoginAsync
    /// and AddClaimsAsync really save through it, no new test infrastructure.</summary>
    private UserManager<User> RealUsers() => new(
        new UserStore<User, Role, GrifballContext, int, UserClaim, UserRole, UserLogin, IdentityUserToken<int>, IdentityRoleClaim<int>>(Db),
        Options.Create(new IdentityOptions()),
        new PasswordHasher<User>(),
        [],
        [],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        null!,
        NullLogger<UserManager<User>>.Instance);

    [Test]
    public async Task SetGamertag_RepliesLinkedToOtherDiscord_WhenTheSignedInAccountIsLinkedElsewhere()
    {
        var users = Users();
        var elsewhere = User.Register("someoneElse");
        elsewhere.LinkDiscord(DiscordUser.FromDiscord(111, "other"));
        users.FindByLoginAsync("Discord", "909090").Returns(elsewhere);
        var handler = new DiscordSetGamertag(Db, users, Substitute.For<ISetGamertagService>());
        var discord = new CommandContextRecorder(discordUserId: 909090);

        await handler.SetGamertag(discord.Context, "NewGamertag");

        Assert.That(discord.Content(), Is.EqualTo(DiscordSetGamertag.LinkedToOtherDiscord));
    }

    [Test]
    public async Task SetGamertag_CreatesANewAccount_ForAnUnknownDiscordUser()
    {
        var setGamertag = Substitute.For<ISetGamertagService>();
        setGamertag.SetGamertag(Arg.Any<int>(), "NewGamertag").Returns((string?)null);
        var handler = new DiscordSetGamertag(Db, RealUsers(), setGamertag);
        var discord = new CommandContextRecorder(discordUserId: 555555);
        discord.Context.Interaction.User.Username.Returns("NewPlayer");

        await handler.SetGamertag(discord.Context, "NewGamertag");

        var created = await Db.Users.SingleAsync(u => u.DiscordUserID == 555555);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("Gamertag has been set"));
            Assert.That(created.UserName, Is.EqualTo("NewPlayer"));
        }
        // Not just that something returned null (a substitute does that on its own): that SetGamertag was actually
        // called, for the user just created.
        await setGamertag.Received(1).SetGamertag(created.Id, "NewGamertag", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_LinksTheAccountSignedInOnTheSiteWithThisDiscord_AndSaysWhyTheGamertagWasRefused()
    {
        var siteAccount = User.Register("SiteUser");
        Db.Users.Add(siteAccount);
        await Db.SaveChangesAsync();
        var users = Users();
        users.FindByLoginAsync("Discord", "555555").Returns(siteAccount);
        var setGamertag = Substitute.For<ISetGamertagService>();
        setGamertag.SetGamertag(siteAccount.Id, "Taken", Arg.Any<CancellationToken>()).Returns("That gamertag is someone else's");
        var discord = new CommandContextRecorder(discordUserId: 555555);
        discord.Context.Interaction.User.Username.Returns("NewPlayer");

        await new DiscordSetGamertag(Db, users, setGamertag).SetGamertag(discord.Context, "Taken");

        Db.ChangeTracker.Clear();
        var linked = await Db.Users.SingleAsync(u => u.Id == siteAccount.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("That gamertag is someone else's"));
            Assert.That(linked.DiscordUserID, Is.EqualTo(555555));
        }
        await users.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Test]
    public async Task SetGamertag_WhenTheAccountCantBeMade_SaysSo_AndSetsNothing()
    {
        var users = Users();
        users.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(new IdentityError { Description = "Bad name" }));
        var setGamertag = Substitute.For<ISetGamertagService>();
        var discord = new CommandContextRecorder(discordUserId: 555555);
        discord.Context.Interaction.User.Username.Returns("NewPlayer");

        await new DiscordSetGamertag(Db, users, setGamertag).SetGamertag(discord.Context, "NewGamertag");

        Assert.That(discord.Content(), Is.EqualTo("Failed to create user account"));
        await setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!);
    }

    /// <summary>Discord user 555555 linked to an archive placeholder, and signed in on the site as SiteUser.</summary>
    private async Task<(User Placeholder, User SiteAccount, UserManager<User> Users)> PlaceholderAndSiteAccount()
    {
        var placeholder = User.ImportPlaceholder("historical:OldTag", "OldTag", null);
        placeholder.LinkDiscord(DiscordUser.FromDiscord(555555, "NewPlayer"));
        var siteAccount = User.Register("SiteUser");
        Db.Users.AddRange(placeholder, siteAccount);
        await Db.SaveChangesAsync();
        var users = Users();
        users.FindByLoginAsync("Discord", "555555").Returns(siteAccount);
        return (placeholder, siteAccount, users);
    }

    [Test]
    public async Task SetGamertag_ForADiscordUserOnAPlaceholder_MergesItIntoTheirSiteAccount()
    {
        var (placeholder, siteAccount, users) = await PlaceholderAndSiteAccount();
        var merges = Substitute.For<IUserMergeService>();
        // Null is "set": a substitute's own default for a string is "", which the handler would reply as a refusal. The gamertag
        // goes to whoever holds the Discord link after the merge (the real merge moves it to the site account; this one is
        // substituted, so the placeholder still holds it): any user here.
        var setGamertag = Substitute.For<ISetGamertagService>();
        setGamertag.SetGamertag(Arg.Any<int>(), "NewGamertag", Arg.Any<CancellationToken>()).Returns((string?)null);
        var discord = new CommandContextRecorder(discordUserId: 555555);

        await new DiscordSetGamertag(Db, users, setGamertag, merges).SetGamertag(discord.Context, "NewGamertag");

        Assert.That(discord.Content(), Is.EqualTo("Gamertag has been set"));
        await setGamertag.Received(1).SetGamertag(placeholder.Id, "NewGamertag", Arg.Any<CancellationToken>());
        await merges.Received(1).Merge(siteAccount.Id, placeholder.Id, Arg.Is<MergeOptions?>(o => o != null && o.Trigger == MergeTrigger.DiscordMatch), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertag_WhenThePlaceholderCantBeMerged_SaysWhy_AndSetsNothing()
    {
        var (_, _, users) = await PlaceholderAndSiteAccount();
        var merges = Substitute.For<IUserMergeService>();
        merges.Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new MergeConflictException("Both played in Winter."));
        var setGamertag = Substitute.For<ISetGamertagService>();
        var discord = new CommandContextRecorder(discordUserId: 555555);

        await new DiscordSetGamertag(Db, users, setGamertag, merges).SetGamertag(discord.Context, "NewGamertag");

        Assert.That(discord.Content(), Is.EqualTo("Your account was made, but your past records could not be joined to it: Both played in Winter. Contact a commissioner."));
        await setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!);
    }
}
