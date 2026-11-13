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
}
